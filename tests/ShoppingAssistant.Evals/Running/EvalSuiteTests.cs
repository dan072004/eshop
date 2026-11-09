using Anthropic;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using eShop.WebApp.Chatbot;
using ShoppingAssistant.Evals.Cases;
using ShoppingAssistant.Evals.Checks;
using ShoppingAssistant.Evals.OptIn;
using ShoppingAssistant.Evals.World;

namespace ShoppingAssistant.Evals.Running;

/// <summary>
/// The evals: every case in <c>Cases/cases.json</c> asked of the Shopping Assistant with a real Claude model, each
/// answer scored by the deterministic checks and written to the results store for <c>dotnet aieval report</c>.
/// </summary>
/// <remarks>
/// Opt-in (<see cref="EvalFactAttribute"/>): <c>$env:ESHOP_EVALS=1; dotnet test --project tests/ShoppingAssistant.Evals</c>,
/// with Docker running and <c>ANTHROPIC_API_KEY</c> set. Then
/// <c>dotnet aieval report -p artifacts/evals-store -o artifacts/evals-report.html --open</c>.
/// </remarks>
public sealed class EvalSuiteTests(PolicyApiFixture policyApi) : IClassFixture<PolicyApiFixture>
{
    [EvalFact]
    public async Task EveryCasePassesItsDeterministicChecks()
    {
        var configuration = EvalSettings.Configuration;
        var apiKey = EvalSettings.AnthropicApiKey(configuration);
        var cancellationToken = TestContext.Current.CancellationToken;
        await policyApi.StartAsync(cancellationToken);

        var world = EvalWorld.Load();
        using var model = new AnthropicClient(new() { ApiKey = apiKey })
            .AsIChatClient(ClaudeChatPipeline.GetModelId(configuration), ClaudeChatPipeline.GetMaxOutputTokens(configuration));
        var runner = new CaseRunner(world, policyApi, model, configuration);
        var reporting = EvalReporting.Create(world);

        List<string> failures = [];
        foreach (var evalCase in EvalCase.LoadAll())
        {
            var transcript = await runner.RunAsync(evalCase);

            await using var scenario = await reporting.CreateScenarioRunAsync(evalCase.Id, cancellationToken: cancellationToken);
            var result = await scenario.EvaluateAsync(
                transcript.Prompt, transcript.Reply, [new EvalCaseContext(evalCase)], cancellationToken);

            failures.AddRange(result.Metrics.Values
                .Where(m => m.Interpretation?.Failed == true)
                .Select(m => $"{evalCase.Id}: {m.Name}: {m.Reason}"));
            TestContext.Current.TestOutputHelper?.WriteLine($"{evalCase.Id}:{Environment.NewLine}{transcript.Reply.Text}");
        }

        Assert.True(failures.Count == 0, $"Results in {EvalReporting.StorePath}{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    }
}
