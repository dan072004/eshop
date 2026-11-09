using Anthropic;
using ShoppingAssistant.Evals.Checks;
using ShoppingAssistant.Evals.OptIn;
using ShoppingAssistant.Evals.Running;

namespace ShoppingAssistant.Evals.Judge;

/// <summary>
/// The judge, checked against its calibration set with the real Sonnet 5.5: every labelled answer must get the
/// verdicts it is labelled with. It costs judge calls only, and needs no Docker.
/// </summary>
/// <remarks>Opt-in, like the suite (<see cref="EvalFactAttribute"/>).</remarks>
public sealed class JudgeCalibrationEvalTests
{
    [EvalFact]
    public async Task JudgeGradesTheCalibrationSetAsLabelled()
    {
        var apiKey = EvalSettings.AnthropicApiKey(EvalSettings.Configuration);
        var cancellationToken = TestContext.Current.CancellationToken;
        var reporting = EvalReporting.CreateCalibration(JudgeClient.Configuration(new AnthropicClient(new() { ApiKey = apiKey })));

        List<JudgeError> judgeErrors = [];
        foreach (var answer in JudgeCalibration.LoadAll())
        {
            await using var scenario = await reporting.CreateScenarioRunAsync(
                JudgeCalibration.ScenarioPrefix + answer.Id, cancellationToken: cancellationToken);
            var result = await scenario.EvaluateAsync(
                answer.Prompt, answer.Reply, [new EvalCaseContext(answer.Case)], cancellationToken);

            // Compare re-interprets the rubric metrics before the scenario is stored, so the report shows judge errors.
            judgeErrors.AddRange(JudgeCalibration.Compare(answer, result));
            TestContext.Current.TestOutputHelper?.WriteLine($"{answer.Id}: {JudgeUsage.Of(result)}");
        }

        Assert.True(
            judgeErrors.Count == 0,
            $"Judge errors (results in {EvalReporting.StorePath}):{Environment.NewLine}{string.Join(Environment.NewLine, judgeErrors)}");
    }
}
