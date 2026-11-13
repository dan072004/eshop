using ShoppingAssistant.Evals.OptIn;
using ShoppingAssistant.Evals.Running;

namespace ShoppingAssistant.Evals.Judge;

/// <summary>
/// The judge, checked against its calibration set with the real Sonnet 5.5: every labelled answer must get the
/// verdicts it is labelled with. It costs judge calls only, and needs no Docker. The suite's gate reads the same run
/// (<see cref="CalibrationRun"/>).
/// </summary>
/// <remarks>Opt-in, like the suite (<see cref="EvalFactAttribute"/>).</remarks>
public sealed class JudgeCalibrationEvalTests
{
    [EvalFact]
    public async Task JudgeGradesTheCalibrationSetAsLabelled()
    {
        var apiKey = EvalSettings.AnthropicApiKey(EvalSettings.Configuration);

        var calibration = await CalibrationRun.OnceAsync(apiKey, TestContext.Current.CancellationToken);

        Assert.Null(calibration.InfrastructureError);
        Assert.True(
            calibration.JudgeErrors.Count == 0,
            $"Judge errors (results in {EvalReporting.StorePath}):{Environment.NewLine}{string.Join(Environment.NewLine, calibration.JudgeErrors)}");
    }
}
