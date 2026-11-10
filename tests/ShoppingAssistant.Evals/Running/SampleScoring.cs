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
        CaseTranscript transcript;
        try
        {
            // Each iteration has its own cache, so the 3 samples are 3 answers, and each is replayed on its own.
            transcript = await runner.RunAsync(evalCase, iteration, cancellationToken);
        }
        catch (ModelCallFailedException e)
        {
            return new SampleResult(evalCase.Id, evalCase.Category, iteration, Failures: [], Usage: [], e.Message, KnownMiss: evalCase.KnownMiss);
        }

        // The scenario is stored when it is disposed, with each judge call and whether it hit the cache.
        string executionName;
        await using (var scenario = await reporting.CreateScenarioRunAsync(
            evalCase.Id, iteration, transcript.CachingKeys, [transcript.OriginTag], cancellationToken))
        {
            executionName = scenario.ExecutionName;
            await scenario.EvaluateAsync(transcript.Prompt, transcript.Reply, [new EvalCaseContext(evalCase)], cancellationToken);
        }

        var stored = await reporting.ResultStore.ReadResultsAsync(executionName, evalCase.Id, iteration, cancellationToken)
            .SingleAsync(cancellationToken);
        return Score(evalCase, transcript, stored);
    }

    /// <summary>
    /// The gate's view of one stored sample: every failed check and rubric item, or, when an evaluator threw, an
    /// infrastructure error instead of a verdict.
    /// </summary>
    public static SampleResult Score(EvalCase evalCase, CaseTranscript transcript, ScenarioRunResult stored)
    {
        var result = stored.EvaluationResult;
        try
        {
            JudgeFailedException.ThrowIfAny(evalCase.Id, result);
        }
        catch (JudgeFailedException e)
        {
            return new SampleResult(evalCase.Id, evalCase.Category, stored.IterationName, Failures: [], Usage: [], e.Message, KnownMiss: evalCase.KnownMiss);
        }

        // Reporting records a throwing evaluator as an error diagnostic on a metric with no interpretation, which
        // would otherwise read as a pass.
        if (result.Metrics.Values
            .Select(m => (m.Name, Error: m.Diagnostics?.FirstOrDefault(d => d.Severity == EvaluationDiagnosticSeverity.Error)))
            .FirstOrDefault(m => m.Error is not null) is { Error: { } error } errored)
        {
            return new SampleResult(
                evalCase.Id, evalCase.Category, stored.IterationName, Failures: [], Usage: [],
                $"Case '{evalCase.Id}': the {errored.Name} check errored, so the sample has no verdict: {FirstLine(error.Message)}",
                KnownMiss: evalCase.KnownMiss);
        }

        return new SampleResult(
            evalCase.Id,
            evalCase.Category,
            stored.IterationName,
            Failures: [.. result.Metrics.Values
                .Where(m => m.Interpretation?.Failed == true)
                .Select(m => new CheckFailure(m.Name, m.Reason ?? m.Interpretation?.Reason ?? ""))],
            Usage: [.. transcript.Usage, .. JudgeUsage.Tokens(stored)],
            Origin: transcript.Origin,
            KnownMiss: evalCase.KnownMiss);
    }

    // The diagnostic holds the whole exception; the summary needs its type and message, the report keeps the trace.
    private static string FirstLine(string text) => text.Split('\n', 2)[0].TrimEnd('\r');
}
