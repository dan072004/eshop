using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using ShoppingAssistant.Evals.Cases;
using ShoppingAssistant.Evals.Checks;
using ShoppingAssistant.Evals.Gate;
using ShoppingAssistant.Evals.Judge;

namespace ShoppingAssistant.Evals.Running;

/// <summary>
/// One sample of one case: the case asked of the assistant, the answer scored into the results store as iteration
/// 1, 2 or 3, and the stored result turned into the <see cref="SampleResult"/> the gate reads. A failed model or judge
/// call becomes an infrastructure error, never a failed check.
/// </summary>
public static class SampleScoring
{
    public const int SamplesPerCase = 3;

    public static async Task<SampleResult> RunAsync(
        CaseRunner runner, ReportingConfiguration reporting, EvalCase evalCase, string iteration, CancellationToken cancellationToken)
    {
        try
        {
            return await AskAndScoreAsync(runner, reporting, evalCase, iteration, cancellationToken);
        }
        catch (ModelCallFailedException e)
        {
            return NoVerdict(evalCase, iteration, e.Message);
        }
    }

    private static async Task<SampleResult> AskAndScoreAsync(
        CaseRunner runner, ReportingConfiguration reporting, EvalCase evalCase, string iteration, CancellationToken cancellationToken)
    {
        // Each iteration has its own cache, so the 3 samples are 3 answers, and each is replayed on its own.
        var transcript = await runner.RunAsync(evalCase, iteration, cancellationToken);
        var executionName = await EvaluateAsync(reporting, evalCase, transcript, iteration, cancellationToken);
        var stored = await reporting.ResultStore.ReadResultsAsync(executionName, evalCase.Id, iteration, cancellationToken)
            .SingleAsync(cancellationToken);
        return Score(evalCase, transcript, stored);
    }

    // The scenario is stored when it is disposed, as this method returns, with each judge call and whether it hit the cache.
    private static async Task<string> EvaluateAsync(
        ReportingConfiguration reporting, EvalCase evalCase, CaseTranscript transcript, string iteration, CancellationToken cancellationToken)
    {
        await using var scenario = await reporting.CreateScenarioRunAsync(
            evalCase.Id, iteration, transcript.CachingKeys, [transcript.OriginTag], cancellationToken);
        await scenario.EvaluateAsync(transcript.Prompt, transcript.Reply, [new EvalCaseContext(evalCase)], cancellationToken);
        return scenario.ExecutionName;
    }

    /// <summary>
    /// The gate's view of one stored sample: every failed check and rubric item, or, when an evaluator threw, an
    /// infrastructure error instead of a verdict.
    /// </summary>
    public static SampleResult Score(EvalCase evalCase, CaseTranscript transcript, ScenarioRunResult stored) =>
        (JudgeFailure(evalCase.Id, stored.EvaluationResult) ?? CheckError(evalCase.Id, stored.EvaluationResult)) is { } error
            ? NoVerdict(evalCase, stored.IterationName, error)
            : new SampleResult(
                evalCase.Id,
                evalCase.Category,
                stored.IterationName,
                Failures: [.. stored.EvaluationResult.Metrics.Values
                    .Where(m => m.Interpretation?.Failed == true)
                    .Select(m => new CheckFailure(m.Name, m.Reason ?? m.Interpretation?.Reason ?? ""))],
                Usage: [.. transcript.Usage, .. JudgeUsage.Tokens(stored)],
                Origin: transcript.Origin,
                KnownMiss: evalCase.KnownMiss);

    private static string? JudgeFailure(string caseId, EvaluationResult result)
    {
        try
        {
            JudgeFailedException.ThrowIfAny(caseId, result);
            return null;
        }
        catch (JudgeFailedException e)
        {
            return e.Message;
        }
    }

    // Reporting records a throwing evaluator as an error diagnostic on a metric with no interpretation, which would
    // otherwise read as a pass.
    private static string? CheckError(string caseId, EvaluationResult result) =>
        result.Metrics.Values
            .Select(m => (m.Name, Error: m.Diagnostics?.FirstOrDefault(d => d.Severity == EvaluationDiagnosticSeverity.Error)))
            .FirstOrDefault(m => m.Error is not null) is { Error: { } error } errored
            ? $"Case '{caseId}': the {errored.Name} check errored, so the sample has no verdict: {FirstLine(error.Message)}"
            : null;

    // The diagnostic holds the whole exception; the summary needs its type and message, the report keeps the trace.
    private static string FirstLine(string text) => text.Split('\n', 2)[0].TrimEnd('\r');

    private static SampleResult NoVerdict(EvalCase evalCase, string iteration, string error) =>
        new(evalCase.Id, evalCase.Category, iteration, Failures: [], Usage: [], error, KnownMiss: evalCase.KnownMiss);
}
