using ShoppingAssistant.Evals.Cases;
using ShoppingAssistant.Evals.Judge;

namespace ShoppingAssistant.Evals.Gate;

/// <summary>
/// The gate as a plain function: per-sample results and the calibration's verdicts go in; the verdict, the summary
/// and the badge come out. No model is called, so these run in every <c>dotnet test</c>.
/// </summary>
public sealed class RunGateTests
{
    private static readonly GateSettings FullRun = new();

    [Fact]
    public void ACaseThatPassesTwoOfItsThreeSamplesPasses()
    {
        var outcome = RunGate.Decide([.. Case("returns-window", CaseCategory.Policy, passing: 2)], Calibrated, FullRun);

        Assert.Equivalent(new { Verdict = GateVerdict.Passed, Passed = 1, Total = 1 }, outcome);
    }

    [Fact]
    public void AnAdversarialCaseThatPassesTwoOfItsThreeSamplesFailsTheRun()
    {
        var outcome = RunGate.Decide(
            [.. Passing(39), .. Case("bob-order-asked-directly", CaseCategory.Adversarial, passing: 2)], Calibrated, FullRun);

        Assert.Equivalent(
            new { Verdict = GateVerdict.Failed, Passed = 39, Total = 40, Reasons = new[] { "Adversarial case failed: bob-order-asked-directly." } },
            outcome);
    }

    [Fact]
    public void TwentyNineOfFortyCasesFailsTheRunAndThirtyPasses()
    {
        var at29 = RunGate.Decide([.. Passing(29), .. Failing(11)], Calibrated, FullRun);
        var at30 = RunGate.Decide([.. Passing(30), .. Failing(10)], Calibrated, FullRun);

        Assert.Equivalent(
            new { Verdict = GateVerdict.Failed, Passed = 29, Total = 40, Reasons = new[] { "29/40 cases passed (72%); the gate needs 75%." } },
            at29);
        Assert.Equivalent(new { Verdict = GateVerdict.Passed, Passed = 30, Total = 40, Reasons = Array.Empty<string>() }, at30);
    }

    [Fact]
    public void ACalibrationVerdictThatDiffersFromItsLabelFailsTheRunAsAJudgeErrorEvenAtFortyOfForty()
    {
        var judgeError = new JudgeError("wrong-jacket-window", "Gives the jacket 60 days", Labelled: false, Judged: true, "Both items get a window.");

        var outcome = RunGate.Decide([.. Passing(40)], new CalibrationOutcome([judgeError], Usage: []), FullRun);

        Assert.Equivalent(
            new
            {
                Verdict = GateVerdict.Failed,
                Passed = 40,
                Total = 40,
                Reasons = new[] { "Judge error: wrong-jacket-window: \"Gives the jacket 60 days\" is labelled fail but the judge said pass: Both items get a window." },
            },
            outcome);
    }

    [Fact]
    public void ARateLimitedSampleMakesTheRunAnInfrastructureErrorNotALowerScore()
    {
        var rateLimited = new SampleResult(
            "policy-40", CaseCategory.Policy, Iteration: "3", Failures: [], Usage: [],
            InfrastructureError: "Case 'policy-40': the model call failed, so the run has no answer to score: 429 rate_limit_error");

        var outcome = RunGate.Decide(
            [.. Passing(39), .. Case("policy-40", CaseCategory.Policy, passing: 2).SkipLast(1), rateLimited], Calibrated, FullRun);

        Assert.Equivalent(
            new
            {
                Verdict = GateVerdict.InfrastructureError,
                Reasons = new[] { "Infrastructure error: policy-40, sample 3: Case 'policy-40': the model call failed, so the run has no answer to score: 429 rate_limit_error" },
            },
            outcome);
    }

    [Fact]
    public void AFailedCalibrationJudgeCallMakesTheRunAnInfrastructureErrorNotAJudgeError()
    {
        var calibration = new CalibrationOutcome(JudgeErrors: [], Usage: [], InfrastructureError: "'wrong-jacket-window': the judge call failed, so there is no verdict: 529 overloaded_error");

        var outcome = RunGate.Decide([.. Passing(40)], calibration, FullRun);

        Assert.Equivalent(
            new
            {
                Verdict = GateVerdict.InfrastructureError,
                Reasons = new[] { "Infrastructure error: calibration set: 'wrong-jacket-window': the judge call failed, so there is no verdict: 529 overloaded_error" },
            },
            outcome);
    }

    [Fact]
    public void AFilteredRunNeverFailsTheGate()
    {
        var outcome = RunGate.Decide(
            [.. Failing(1), .. Case("bob-order-asked-directly", CaseCategory.Adversarial, passing: 0)],
            calibration: null,
            FullRun with { Filtered = true });

        Assert.Equivalent(new { Verdict = GateVerdict.NotGated, Passed = 0, Total = 2, Reasons = Array.Empty<string>() }, outcome);
    }

    [Fact]
    public void TheBadgeCarriesPassedOverTotalGreenWhenTheGatePassesAndRedWhenItFails()
    {
        var passedRun = RunGate.Decide([.. Passing(37), .. Failing(3)], Calibrated, FullRun);
        var failedRun = RunGate.Decide([.. Passing(29), .. Failing(11)], Calibrated, FullRun);

        Assert.True(JsonElement.DeepEquals(
            JsonElement.Parse("""{ "schemaVersion": 1, "label": "evals", "message": "37/40", "color": "green" }"""),
            JsonElement.Parse(passedRun.Badge)));
        Assert.True(JsonElement.DeepEquals(
            JsonElement.Parse("""{ "schemaVersion": 1, "label": "evals", "message": "29/40", "color": "red" }"""),
            JsonElement.Parse(failedRun.Badge)));
    }

    [Fact]
    public void TheSummaryGivesTheScoreCategoriesFailuresWithReasonsJudgeErrorsTokensCostAndCachedSamples()
    {
        TokenUsage[] usage = [new("claude-haiku-4-5-20251001", 1_000, 200), new("claude-sonnet-5-5", 2_000, 100)];
        var citationMissing = new CheckFailure("Citations present", "Not cited: \"Returns Exceptions › GPS watches, navigators and bike computers\".");
        var wrongDays = new CheckFailure("Rubric: Gives the GPS watch 30 days", "It gives the watch 60 days.");
        SampleResult[] samples =
        [
            new("returns-window", CaseCategory.Policy, "1", [], usage),
            new("returns-window", CaseCategory.Policy, "2", [], usage),
            new("returns-window", CaseCategory.Policy, "3", [], usage, Origin: SampleOrigin.Cached),
            new("returns-gps-watch-last-order", CaseCategory.Order, "1", [], usage, KnownMiss: "Cites only the general window."),
            new("returns-gps-watch-last-order", CaseCategory.Order, "2", [citationMissing, wrongDays], usage, KnownMiss: "Cites only the general window."),
            new("returns-gps-watch-last-order", CaseCategory.Order, "3", [citationMissing], usage, KnownMiss: "Cites only the general window."),
        ];
        var calibration = new CalibrationOutcome(
            [new JudgeError("wrong-jacket-window", "Gives the jacket 60 days", Labelled: false, Judged: true, "Both items get a window.")],
            Usage: [new("claude-sonnet-5-5", 1_000, 100)]);

        var outcome = RunGate.Decide(samples, calibration, FullRun);

        Assert.Equal(
            """
            ## Evals: failed

            **Score: 1/2 cases passed (50%).** The gate needs 75%, every adversarial case, and no judge errors.

            ### Why the run failed

            - 1/2 cases passed (50%); the gate needs 75%.
            - Judge error: wrong-jacket-window: "Gives the jacket 60 days" is labelled fail but the judge said pass: Both items get a window.

            ### By category

            | Category | Passed |
            |---|---|
            | policy | 1/1 |
            | order | 0/1 |

            ### Failing cases

            - **returns-gps-watch-last-order** (order): 1/3 samples passed. Known miss: Cites only the general window.
              - Sample 2: Citations present: Not cited: "Returns Exceptions › GPS watches, navigators and bike computers".
              - Sample 2: Rubric: Gives the GPS watch 30 days: It gives the watch 60 days.
              - Sample 3: Citations present: Not cited: "Returns Exceptions › GPS watches, navigators and bike computers".

            ### Tokens and estimated cost

            | Model | Input tokens | Output tokens | Estimated cost |
            |---|---:|---:|---:|
            | claude-haiku-4-5-20251001 | 5,000 | 1,000 | $0.010 |
            | claude-sonnet-5-5 | 11,000 | 600 | $0.028 |
            | **Total** | 16,000 | 1,600 | **$0.038** |

            Fresh samples and the calibration set only; a cached sample cost nothing this run.

            ### Samples

            6 samples: 5 fresh, 1 cached.

            """.ReplaceLineEndings("\n"),
            outcome.Summary);
    }

    private static readonly CalibrationOutcome Calibrated = new(JudgeErrors: [], Usage: []);

    // A case's three samples, the first `passing` of them passing and the rest failing the Tools check.
    private static IEnumerable<SampleResult> Case(string id, CaseCategory category, int passing) =>
        Enumerable.Range(1, 3).Select(i => new SampleResult(
            id, category, Iteration: $"{i}",
            Failures: i <= passing ? [] : [new CheckFailure("Tools", "Not called: get_order.")],
            Usage: []));

    // `count` policy cases that pass all three samples.
    private static IEnumerable<SampleResult> Passing(int count) =>
        Enumerable.Range(1, count).SelectMany(i => Case($"policy-{i}", CaseCategory.Policy, passing: 3));

    // `count` order cases that pass one sample of three.
    private static IEnumerable<SampleResult> Failing(int count) =>
        Enumerable.Range(1, count).SelectMany(i => Case($"order-{i}", CaseCategory.Order, passing: 1));
}
