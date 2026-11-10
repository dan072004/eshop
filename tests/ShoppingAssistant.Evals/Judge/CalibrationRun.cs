using Anthropic;
using ShoppingAssistant.Evals.Checks;
using ShoppingAssistant.Evals.Gate;
using ShoppingAssistant.Evals.Running;

namespace ShoppingAssistant.Evals.Judge;

/// <summary>
/// The calibration set graded by the real judge, once per test run: the calibration fact asserts on it and the suite's
/// gate reads its judge errors, so a full run pays for the judge calls once.
/// </summary>
public static class CalibrationRun
{
    private static readonly Lock Gate = new();
    private static Task<CalibrationOutcome>? _run;

    public static Task<CalibrationOutcome> OnceAsync(string apiKey, CancellationToken cancellationToken)
    {
        lock (Gate)
        {
            return _run ??= RunAsync(apiKey, cancellationToken);
        }
    }

    private static async Task<CalibrationOutcome> RunAsync(string apiKey, CancellationToken cancellationToken)
    {
        var reporting = EvalReporting.CreateCalibration(JudgeClient.Configuration(new AnthropicClient(new() { ApiKey = apiKey })));

        List<JudgeError> judgeErrors = [];
        List<TokenUsage> usage = [];
        foreach (var answer in JudgeCalibration.LoadAll())
        {
            var scenarioName = JudgeCalibration.ScenarioPrefix + answer.Id;
            string executionName, iterationName;
            JudgeFailedException? failed = null;
            await using (var scenario = await reporting.CreateScenarioRunAsync(scenarioName, cancellationToken: cancellationToken))
            {
                (executionName, iterationName) = (scenario.ExecutionName, scenario.IterationName);
                var result = await scenario.EvaluateAsync(
                    answer.Prompt, answer.Reply, [new EvalCaseContext(answer.Case)], cancellationToken);

                // Compare re-interprets the rubric metrics before the scenario is stored, so the report shows judge errors.
                try
                {
                    judgeErrors.AddRange(JudgeCalibration.Compare(answer, result));
                }
                catch (JudgeFailedException e)
                {
                    failed = e;
                }
            }

            // The stored scenario records each judge call and whether the cache answered it; only misses cost tokens.
            var stored = await reporting.ResultStore.ReadResultsAsync(executionName, scenarioName, iterationName, cancellationToken)
                .SingleAsync(cancellationToken);
            usage.AddRange(JudgeUsage.Tokens(stored));
            if (failed is not null)
            {
                return new CalibrationOutcome(judgeErrors, usage, failed.Message);
            }
        }

        return new CalibrationOutcome(judgeErrors, usage);
    }
}
