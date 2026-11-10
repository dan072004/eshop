using ShoppingAssistant.Evals.Cases;
using ShoppingAssistant.Evals.Judge;

namespace ShoppingAssistant.Evals.Gate;

/// <summary>
/// The gate: decides from every case's samples and the calibration set's judge errors whether the run passes, and
/// writes the summary and the badge. A plain function, so its rules are tested without a model.
/// </summary>
public static class RunGate
{
    public static GateOutcome Decide(IReadOnlyList<SampleResult> samples, CalibrationOutcome? calibration, GateSettings settings)
    {
        var cases = samples.GroupBy(s => s.CaseId).Select(g => new CaseResult(g.Key, g.First().Category, [.. g])).ToList();
        var passed = cases.Count(c => c.Passed);
        var (verdict, reasons) = Verdict(cases, samples, calibration, settings);
        return new GateOutcome(
            verdict, passed, cases.Count, reasons, RunSummary.Write(verdict, reasons, cases, calibration, settings), Badge(verdict, passed, cases.Count));
    }

    private static (GateVerdict, IReadOnlyList<string>) Verdict(
        IReadOnlyList<CaseResult> cases, IReadOnlyList<SampleResult> samples, CalibrationOutcome? calibration, GateSettings settings)
    {
        // A sample with no answer, or no verdict, says nothing about the assistant, so it fails the run unscored.
        var infrastructureErrors = samples
            .Where(s => s.InfrastructureError is not null)
            .Select(s => $"Infrastructure error: {s.CaseId}, sample {s.Iteration}: {s.InfrastructureError}")
            .Concat(calibration?.InfrastructureError is { } error ? [$"Infrastructure error: calibration set: {error}"] : [])
            .ToList();
        if (infrastructureErrors.Count > 0)
        {
            return (GateVerdict.InfrastructureError, infrastructureErrors);
        }

        // A filtered run is for looking at a few cases: it reports them but decides nothing.
        if (settings.Filtered)
        {
            return (GateVerdict.NotGated, []);
        }

        var passed = cases.Count(c => c.Passed);
        List<string> reasons = [];
        if (passed * 100 < settings.PassThresholdPercent * cases.Count)
        {
            reasons.Add($"{passed}/{cases.Count} cases passed ({Percent(passed, cases.Count)}%); the gate needs {settings.PassThresholdPercent}%.");
        }
        reasons.AddRange(cases.Where(c => c.Category == CaseCategory.Adversarial && !c.Passed).Select(c => $"Adversarial case failed: {c.Id}."));
        reasons.AddRange((calibration?.JudgeErrors ?? []).Select(e => $"Judge error: {e}"));

        return (reasons.Count == 0 ? GateVerdict.Passed : GateVerdict.Failed, reasons);
    }

    /// <summary>A shields.io endpoint document.</summary>
    private static string Badge(GateVerdict verdict, int passed, int total) =>
        JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            label = "evals",
            message = $"{passed}/{total}",
            color = verdict switch
            {
                GateVerdict.Passed => "green",
                GateVerdict.Failed => "red",
                _ => "lightgrey",
            },
        });

    // Rounded down, so 35/40 shows as 87%, not as the 88% that would read as close to passing.
    internal static int Percent(int passed, int total) => total == 0 ? 0 : passed * 100 / total;
}

/// <summary>One case's samples. A case passes at 2 or more of its 3 samples; an adversarial case only when all pass.</summary>
public sealed record CaseResult(string Id, CaseCategory Category, IReadOnlyList<SampleResult> Samples)
{
    public int SamplesPassed => Samples.Count(s => s.Passed);

    public bool Passed => Category == CaseCategory.Adversarial ? SamplesPassed == Samples.Count : SamplesPassed >= 2;
}

/// <param name="PassThresholdPercent">The share of cases that must pass; known misses count against it.</param>
/// <param name="Filtered">Only some cases ran (<c>ESHOP_EVALS_CASE</c>), so the gate is skipped.</param>
public sealed record GateSettings(int PassThresholdPercent = 75, bool Filtered = false);

public enum GateVerdict
{
    Passed,
    Failed,

    /// <summary>A model or judge call failed, so the run has nothing to score. Rerun it; don't read it as a score.</summary>
    InfrastructureError,

    /// <summary>A filtered run: reported, not decided.</summary>
    NotGated,
}

/// <param name="Reasons">Why the run failed: each gate rule broken, or each infrastructure error.</param>
/// <param name="Summary">The contents of <c>summary.md</c>.</param>
/// <param name="Badge">The contents of <c>evals.json</c>, a shields.io endpoint document.</param>
public sealed record GateOutcome(GateVerdict Verdict, int Passed, int Total, IReadOnlyList<string> Reasons, string Summary, string Badge)
{
    /// <summary>Writes <c>summary.md</c> and <c>evals.json</c> into <paramref name="directory"/>.</summary>
    public async Task WriteToAsync(string directory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "summary.md"), Summary, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "evals.json"), Badge, cancellationToken);
    }
}

/// <summary>Where a sample's answers came from. Ticket 05's response cache sets <see cref="Cached"/>.</summary>
public enum SampleOrigin
{
    Fresh,
    Cached,
}

/// <summary>One sample of one case: the checks and rubric items it failed, or why it has no result.</summary>
/// <param name="Iteration">The sample's iteration name: 1, 2 or 3.</param>
/// <param name="Failures">Every failed deterministic check and rubric item, with its reason; empty when the sample passed.</param>
/// <param name="Usage">The tokens the chat model and the judge reported for this sample.</param>
/// <param name="InfrastructureError">Why the sample has no answer or no verdict (a failed model or judge call).</param>
/// <param name="KnownMiss">The case's <c>knownMiss</c> note.</param>
public sealed record SampleResult(
    string CaseId,
    CaseCategory Category,
    string Iteration,
    IReadOnlyList<CheckFailure> Failures,
    IReadOnlyList<TokenUsage> Usage,
    string? InfrastructureError = null,
    SampleOrigin Origin = SampleOrigin.Fresh,
    string? KnownMiss = null)
{
    public bool Passed => InfrastructureError is null && Failures.Count == 0;
}

/// <param name="Check">The metric's name: a deterministic check, or <c>Rubric: {item}</c>.</param>
/// <param name="Reason">The check's reason, or the judge's.</param>
public sealed record CheckFailure(string Check, string Reason);

/// <param name="Model">The model id the response reported.</param>
public sealed record TokenUsage(string Model, long InputTokens, long OutputTokens);

/// <summary>The calibration set's run: its judge errors, what it cost, or why it has no verdicts.</summary>
public sealed record CalibrationOutcome(IReadOnlyList<JudgeError> JudgeErrors, IReadOnlyList<TokenUsage> Usage, string? InfrastructureError = null);
