using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using ShoppingAssistant.Evals.Cases;
using ShoppingAssistant.Evals.Checks;
using ShoppingAssistant.Evals.Gate;
using ShoppingAssistant.Evals.Judge;

namespace ShoppingAssistant.Evals.Running;

/// <summary>
/// One sample of one case: the case asked of the assistant, the answer scored into the results store as iteration
/// 1, 2 or 3, and the outcome turned into the <see cref="SampleResult"/> the gate reads. A failed model or judge call
/// becomes an infrastructure error, never a failed check.
/// </summary>
public static class SampleScoring
{
    public const int SamplesPerCase = 3;

    public static async Task<SampleResult> RunAsync(
        CaseRunner runner, ReportingConfiguration reporting, EvalCase evalCase, string iteration, CancellationToken cancellationToken)
    {
        try
        {
            // Each iteration has its own cache, so the 3 samples are 3 answers, and each is replayed on its own.
            var transcript = await runner.RunAsync(evalCase, iteration, cancellationToken);

            await using var scenario = await reporting.CreateScenarioRunAsync(
                evalCase.Id, iteration, transcript.CachingKeys, [transcript.OriginTag], cancellationToken);
            var result = await scenario.EvaluateAsync(
                transcript.Prompt, transcript.Reply, [new EvalCaseContext(evalCase)], cancellationToken);
            JudgeFailedException.ThrowIfAny(evalCase.Id, result);

            return new SampleResult(
                evalCase.Id,
                evalCase.Category,
                iteration,
                Failures: [.. result.Metrics.Values
                    .Where(m => m.Interpretation?.Failed == true)
                    .Select(m => new CheckFailure(m.Name, m.Reason ?? m.Interpretation?.Reason ?? ""))],
                Usage: [.. transcript.Usage, .. JudgeUsage.Tokens(result)],
                Origin: transcript.Origin,
                KnownMiss: evalCase.KnownMiss);
        }
        catch (Exception e) when (e is ModelCallFailedException or JudgeFailedException)
        {
            return new SampleResult(evalCase.Id, evalCase.Category, iteration, Failures: [], Usage: [], e.Message, KnownMiss: evalCase.KnownMiss);
        }
    }
}
