using Microsoft.Extensions.AI.Evaluation;
using ShoppingAssistant.Evals.Cases;

namespace ShoppingAssistant.Evals.Judge;

/// <summary>
/// The calibration set's own logic, with the Anthropic API faked: a verdict that differs from its label is a judge
/// error, reported apart from case results; a verdict that matches its label, pass or fail, is not.
/// </summary>
public sealed class JudgeCalibrationTests
{
    private static readonly CalibrationAnswer WrongJacketWindow = new(
        Id: "wrong-jacket-window",
        Category: CaseCategory.Order,
        Note: "Gives the jacket 30 days, the GPS watch's exception, instead of the standard 60.",
        Turns: ["what's the return window for my last order?"],
        PolicySections:
        [
            new("Returns Exceptions › GPS watches, navigators and bike computers", "Navigation electronics have a 30-day return window from the order date, not 60 days."),
            new("Returns Policy › Return window", "You can return most items within 60 days of the order date for a full refund."),
        ],
        Reference: "The GPS watch has 30 days, until November 30, 2026; the jacket has 60 days, until December 30, 2026.",
        Answer: "Order 101: the GPS watch and the jacket can both be returned within 30 days, until November 30, 2026.",
        Rubric:
        [
            new("Answers about order 101", Passes: true),
            new("Gives the GPS watch 30 days", Passes: true),
            new("Gives the jacket 60 days", Passes: false),
        ]);

    [Fact]
    public async Task AVerdictThatDiffersFromItsLabelIsAJudgeErrorAndAMatchingFailIsNot()
    {
        var anthropic = new FakeAnthropic(FakeAnthropic.Verdicts(
            (1, "It names order 101.", true),
            (2, "30 days for the watch.", true),
            (3, "Both items get a return window.", true)));

        var result = await WrongJacketWindow.GradeAsync(new RubricEvaluator(), JudgeClient.Configuration(anthropic.Client()), TestContext.Current.CancellationToken);
        var judgeErrors = JudgeCalibration.Compare(WrongJacketWindow, result);

        Assert.Equal(
            [new JudgeError("wrong-jacket-window", "Gives the jacket 60 days", Labelled: false, Judged: true, JudgeReason: "Both items get a return window.")],
            judgeErrors);
    }

    [Fact]
    public async Task TheReportShowsACalibrationVerdictAsFailedOnlyWhenItDiffersFromItsLabel()
    {
        var anthropic = new FakeAnthropic(FakeAnthropic.Verdicts(
            (1, "It names order 101.", true),
            (2, "It gives the watch 60 days.", false),
            (3, "It gives the jacket 30 days.", false)));

        var result = await WrongJacketWindow.GradeAsync(new RubricEvaluator(), JudgeClient.Configuration(anthropic.Client()), TestContext.Current.CancellationToken);
        JudgeCalibration.Compare(WrongJacketWindow, result);

        Assert.Equivalent(
            new[]
            {
                (Name: "Rubric: Answers about order 101", Failed: false, Reason: "As labelled (pass): It names order 101."),
                (Name: "Rubric: Gives the GPS watch 30 days", Failed: true, Reason: "Judge error: labelled pass, judged fail: It gives the watch 60 days."),
                (Name: "Rubric: Gives the jacket 60 days", Failed: false, Reason: "As labelled (fail): It gives the jacket 30 days."),
            },
            result.Metrics.Values.Select(m => (m.Name, m.Interpretation!.Failed, m.Interpretation.Reason)).ToArray());
    }

    [Fact]
    public void EveryCalibrationAnswerLoadsWithAUniqueIdAndAtLeastOneCorrectAndOneWrongAnswer()
    {
        var answers = JudgeCalibration.LoadAll();

        Assert.Equal(answers.Select(a => a.Id).Distinct(), answers.Select(a => a.Id));
        Assert.Contains(answers, a => a.Rubric.All(r => r.Passes));
        Assert.Contains(answers, a => a.Rubric.Any(r => !r.Passes));
    }
}
