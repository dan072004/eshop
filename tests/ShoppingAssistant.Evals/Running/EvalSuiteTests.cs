using Anthropic;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using eShop.WebApp.Chatbot;
using ShoppingAssistant.Evals.Cases;
using ShoppingAssistant.Evals.Checks;
using ShoppingAssistant.Evals.Judge;
using ShoppingAssistant.Evals.OptIn;
using ShoppingAssistant.Evals.World;

namespace ShoppingAssistant.Evals.Running;

/// <summary>
/// The evals: every case in <c>Cases/cases.json</c> asked of the Shopping Assistant with a real Claude model, each
/// answer scored by the deterministic checks and the judge's rubric, and written to the results store for
/// <c>dotnet aieval report</c>.
/// </summary>
/// <remarks>
/// Opt-in (<see cref="EvalFactAttribute"/>): <c>$env:ESHOP_EVALS=1; dotnet test --project tests/ShoppingAssistant.Evals</c>,
/// with Docker running and <c>ANTHROPIC_API_KEY</c> set. Then
/// <c>dotnet aieval report -p artifacts/evals-store -o artifacts/evals-report.html --open</c>.
/// </remarks>
public sealed class EvalSuiteTests(PolicyApiFixture policyApi) : IClassFixture<PolicyApiFixture>
{
    /// <summary>A sample passes only when every deterministic check and every rubric item pass.</summary>
    [EvalFact]
    public async Task EveryCasePassesItsChecksAndItsRubric()
    {
        var configuration = EvalSettings.Configuration;
        var apiKey = EvalSettings.AnthropicApiKey(configuration);
        var cancellationToken = TestContext.Current.CancellationToken;
        await policyApi.StartAsync(cancellationToken);

        var world = EvalWorld.Load();
        var anthropic = new AnthropicClient(new() { ApiKey = apiKey });
        using var model = anthropic.AsIChatClient(ClaudeChatPipeline.GetModelId(configuration), ClaudeChatPipeline.GetMaxOutputTokens(configuration));
        var runner = new CaseRunner(world, policyApi, new CachedModel(model, EvalReporting.ResponseCacheProvider), configuration);
        var reporting = EvalReporting.Create(world, JudgeClient.Configuration(anthropic));

        List<string> failures = [];
        foreach (var evalCase in EvalCase.LoadAll())
        {
            var transcript = await runner.RunAsync(evalCase, cancellationToken: cancellationToken);

            await using var scenario = await reporting.CreateScenarioRunAsync(
                evalCase.Id, additionalCachingKeys: transcript.CachingKeys, additionalTags: [transcript.OriginTag], cancellationToken: cancellationToken);
            var result = await scenario.EvaluateAsync(
                transcript.Prompt, transcript.Reply, [new EvalCaseContext(evalCase)], cancellationToken);
            JudgeFailedException.ThrowIfAny(evalCase.Id, result);

            failures.AddRange(result.Metrics.Values
                .Where(m => m.Interpretation?.Failed == true)
                .Select(m => $"{evalCase.Id}: {m.Name}: {m.Reason}"));
            TestContext.Current.TestOutputHelper?.WriteLine(
                $"{evalCase.Id} ({transcript.OriginTag}, {transcript.ModelCalls} model calls):{Environment.NewLine}{transcript.Reply.Text}{Environment.NewLine}{JudgeUsage.Of(result)}");
        }

        Assert.True(failures.Count == 0, $"Results in {EvalReporting.StorePath}{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    }
}
