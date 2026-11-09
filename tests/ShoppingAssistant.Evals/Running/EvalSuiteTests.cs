using System.Collections.Concurrent;
using Anthropic;
using Microsoft.Extensions.AI;
using eShop.WebApp.Chatbot;
using ShoppingAssistant.Evals.Cases;
using ShoppingAssistant.Evals.Gate;
using ShoppingAssistant.Evals.Judge;
using ShoppingAssistant.Evals.OptIn;
using ShoppingAssistant.Evals.World;

namespace ShoppingAssistant.Evals.Running;

/// <summary>
/// The evals: every case in <c>Cases/cases.json</c> asked of the Shopping Assistant with a real Claude model three
/// times, each answer scored by the deterministic checks and the judge's rubric and written to the results store for
/// <c>dotnet aieval report</c>. The gate then decides the run, and <c>summary.md</c> and <c>evals.json</c> are
/// written next to the store's results.
/// </summary>
/// <remarks>
/// Opt-in (<see cref="EvalFactAttribute"/>): <c>$env:ESHOP_EVALS=1; dotnet test --project tests/ShoppingAssistant.Evals</c>,
/// with Docker running and <c>ANTHROPIC_API_KEY</c> set. <c>ESHOP_EVALS_CASE</c> runs only the listed cases and
/// skips the gate; <c>ESHOP_EVALS_PARALLELISM</c> (default 4) sets how many cases run at once. Then
/// <c>dotnet aieval report -p artifacts/evals-store -o artifacts/evals-report.html --open</c>.
/// </remarks>
public sealed class EvalSuiteTests(PolicyApiFixture policyApi) : IClassFixture<PolicyApiFixture>
{
    [EvalFact]
    public async Task TheRunPassesTheGate()
    {
        var configuration = EvalSettings.Configuration;
        var apiKey = EvalSettings.AnthropicApiKey(configuration);
        var filter = EvalSettings.CaseFilter(configuration[EvalSettings.CaseVariable]);
        var parallelism = EvalSettings.Parallelism(configuration[EvalSettings.ParallelismVariable]);
        var cancellationToken = TestContext.Current.CancellationToken;
        var cases = Selected(EvalCase.LoadAll(), filter);
        await policyApi.StartAsync(cancellationToken);

        var world = EvalWorld.Load();
        var anthropic = new AnthropicClient(new() { ApiKey = apiKey });
        using var model = anthropic.AsIChatClient(ClaudeChatPipeline.GetModelId(configuration), ClaudeChatPipeline.GetMaxOutputTokens(configuration));
        var runner = new CaseRunner(world, policyApi, new CachedModel(model, EvalReporting.ResponseCacheProvider), configuration);
        var reporting = EvalReporting.Create(world, JudgeClient.Configuration(anthropic));

        // The calibration set feeds the gate, so a filtered run, which skips the gate, doesn't pay for it.
        var calibration = filter is null ? CalibrationRun.OnceAsync(apiKey, cancellationToken) : null;

        ConcurrentBag<SampleResult> samples = [];
        await Parallel.ForEachAsync(
            cases,
            new ParallelOptions { MaxDegreeOfParallelism = parallelism, CancellationToken = cancellationToken },
            async (evalCase, ct) =>
            {
                for (var i = 1; i <= SampleScoring.SamplesPerCase; i++)
                {
                    samples.Add(await SampleScoring.RunAsync(runner, reporting, evalCase, $"{i}", ct));
                }
            });

        var outcome = RunGate.Decide(
            [.. samples.OrderBy(s => cases.FindIndex(c => c.Id == s.CaseId)).ThenBy(s => s.Iteration, StringComparer.Ordinal)],
            calibration is null ? null : await calibration,
            new GateSettings(Filtered: filter is not null));
        await outcome.WriteToAsync(EvalReporting.StorePath, cancellationToken);
        TestContext.Current.TestOutputHelper?.WriteLine(outcome.Summary);

        Assert.True(
            outcome.Verdict is GateVerdict.Passed or GateVerdict.NotGated,
            $"Evals {outcome.Verdict} (summary.md and evals.json in {EvalReporting.StorePath}):{Environment.NewLine}{outcome.Summary}");
    }

    private static List<EvalCase> Selected(IReadOnlyList<EvalCase> all, IReadOnlyList<string>? filter)
    {
        if (filter is null)
        {
            return [.. all];
        }

        var unknown = filter.Except(all.Select(c => c.Id)).ToList();
        Assert.True(unknown.Count == 0, $"{EvalSettings.CaseVariable} names no such case: {string.Join(", ", unknown)}.");
        return [.. all.Where(c => filter.Contains(c.Id))];
    }
}
