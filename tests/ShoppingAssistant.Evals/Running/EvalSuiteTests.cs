using System.Collections.Concurrent;
using Anthropic;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using Microsoft.Extensions.Configuration;
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
        var cancellationToken = TestContext.Current.CancellationToken;
        var settings = SuiteSettings.Read();
        var cases = Selected(EvalCase.LoadAll(), settings.Filter);
        await policyApi.StartAsync(cancellationToken);

        var anthropic = new AnthropicClient(new() { ApiKey = settings.ApiKey });
        using var model = ChatModel(anthropic, settings.Configuration);
        var scorer = CreateScorer(anthropic, model, settings);

        // The calibration set feeds the gate, so a filtered run, which skips the gate, doesn't pay for it.
        var calibration = settings.IsFiltered ? null : CalibrationRun.OnceAsync(settings.ApiKey, cancellationToken);
        var samples = await scorer.ScoreAllAsync(cases, cancellationToken);

        var outcome = RunGate.Decide(
            samples,
            calibration is null ? null : await calibration,
            new GateSettings(Filtered: settings.IsFiltered));
        await PublishAsync(outcome, cancellationToken);
        AssertPassed(outcome);
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

    // The model and output limit WebApp would use, from the same configuration keys.
    private static IChatClient ChatModel(AnthropicClient anthropic, IConfiguration configuration) =>
        anthropic.AsIChatClient(ClaudeChatPipeline.GetModelId(configuration), ClaudeChatPipeline.GetMaxOutputTokens(configuration));

    private Scorer CreateScorer(AnthropicClient anthropic, IChatClient model, SuiteSettings settings)
    {
        var world = EvalWorld.Load();
        var runner = new CaseRunner(world, policyApi, new CachedModel(model, EvalReporting.ResponseCacheProvider), settings.Configuration);
        var reporting = EvalReporting.Create(world, JudgeClient.Configuration(anthropic));
        return new Scorer(runner, reporting, settings.Parallelism);
    }

    private static async Task PublishAsync(GateOutcome outcome, CancellationToken cancellationToken)
    {
        await outcome.WriteToAsync(EvalReporting.StorePath, cancellationToken);
        TestContext.Current.TestOutputHelper?.WriteLine(outcome.Summary);
    }

    private static void AssertPassed(GateOutcome outcome) =>
        Assert.True(
            outcome.Verdict is GateVerdict.Passed or GateVerdict.NotGated,
            $"Evals {outcome.Verdict} (summary.md and evals.json in {EvalReporting.StorePath}):{Environment.NewLine}{outcome.Summary}");

    /// <summary>The run's settings: the API key, the case filter (null runs every case) and how many cases run at once.</summary>
    private sealed record SuiteSettings(IConfiguration Configuration, string ApiKey, IReadOnlyList<string>? Filter, int Parallelism)
    {
        public bool IsFiltered => Filter is not null;

        public static SuiteSettings Read()
        {
            var configuration = EvalSettings.Configuration;
            return new(
                configuration,
                EvalSettings.AnthropicApiKey(configuration),
                EvalSettings.CaseFilter(configuration[EvalSettings.CaseVariable]),
                EvalSettings.Parallelism(configuration[EvalSettings.ParallelismVariable]));
        }
    }

    /// <summary>Asks every case <see cref="SampleScoring.SamplesPerCase"/> times, several cases at once.</summary>
    private sealed record Scorer(CaseRunner Runner, ReportingConfiguration Reporting, int Parallelism)
    {
        public async Task<IReadOnlyList<SampleResult>> ScoreAllAsync(List<EvalCase> cases, CancellationToken cancellationToken)
        {
            ConcurrentBag<SampleResult> samples = [];
            await Parallel.ForEachAsync(
                cases,
                new ParallelOptions { MaxDegreeOfParallelism = Parallelism, CancellationToken = cancellationToken },
                async (evalCase, ct) =>
                {
                    for (var i = 1; i <= SampleScoring.SamplesPerCase; i++)
                    {
                        samples.Add(await SampleScoring.RunAsync(Runner, Reporting, evalCase, $"{i}", ct));
                    }
                });
            return InCaseOrder(samples, cases);
        }

        // Parallel runs finish in any order; the summary lists samples as cases.json lists cases.
        private static List<SampleResult> InCaseOrder(IEnumerable<SampleResult> samples, List<EvalCase> cases) =>
            [.. samples.OrderBy(s => cases.FindIndex(c => c.Id == s.CaseId)).ThenBy(s => s.Iteration, StringComparer.Ordinal)];
    }
}
