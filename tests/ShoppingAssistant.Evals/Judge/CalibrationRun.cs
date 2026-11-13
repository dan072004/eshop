using Anthropic;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Reporting;
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
            var graded = await GradeAsync(reporting, answer, cancellationToken);
            judgeErrors.AddRange(graded.JudgeErrors);
            usage.AddRange(graded.Usage);
            if (graded.InfrastructureError is not null)
            {
                return new CalibrationOutcome(judgeErrors, usage, graded.InfrastructureError);
            }
        }

        return new CalibrationOutcome(judgeErrors, usage);
    }

    // One answer's outcome: its judge errors, or why it has none, and the tokens of its judge call.
    private static async Task<CalibrationOutcome> GradeAsync(
        ReportingConfiguration reporting, CalibrationAnswer answer, CancellationToken cancellationToken)
    {
        var scenarioName = JudgeCalibration.ScenarioPrefix + answer.Id;
        var (compared, executionName, iterationName) = await EvaluateAsync(reporting, scenarioName, answer, cancellationToken);

        // The stored scenario records each judge call and whether the cache answered it; only misses cost tokens.
        var stored = await reporting.ResultStore.ReadResultsAsync(executionName, scenarioName, iterationName, cancellationToken)
            .SingleAsync(cancellationToken);
        return compared with { Usage = JudgeUsage.Tokens(stored) };
    }

    // The scenario is stored when it is disposed, as this method returns.
    private static async Task<(CalibrationOutcome Compared, string ExecutionName, string IterationName)> EvaluateAsync(
        ReportingConfiguration reporting, string scenarioName, CalibrationAnswer answer, CancellationToken cancellationToken)
    {
        await using var scenario = await reporting.CreateScenarioRunAsync(scenarioName, cancellationToken: cancellationToken);
        var result = await scenario.EvaluateAsync(answer.Prompt, answer.Reply, [new EvalCaseContext(answer.Case)], cancellationToken);

        // Compare re-interprets the rubric metrics before the scenario is stored, so the report shows judge errors.
        return (Compare(answer, result), scenario.ExecutionName, scenario.IterationName);
    }

    private static CalibrationOutcome Compare(CalibrationAnswer answer, EvaluationResult result)
    {
        try
        {
            return new CalibrationOutcome(JudgeCalibration.Compare(answer, result), Usage: []);
        }
        catch (JudgeFailedException e)
        {
            return new CalibrationOutcome(JudgeErrors: [], Usage: [], e.Message);
        }
    }
}
