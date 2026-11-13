using System.Globalization;
using System.Text;
using ShoppingAssistant.Evals.Cases;

namespace ShoppingAssistant.Evals.Gate;

/// <summary><c>summary.md</c>: what the gate decided and why, for the job summary and the PR comment.</summary>
internal static class RunSummary
{
    public static string Write(
        GateVerdict verdict,
        IReadOnlyList<string> reasons,
        IReadOnlyList<CaseResult> cases,
        CalibrationOutcome? calibration,
        GateSettings settings)
    {
        var summary = new StringBuilder();
        AppendScore(summary, verdict, cases, settings);
        AppendReasons(summary, reasons);
        AppendCategories(summary, cases);
        AppendFailingCases(summary, cases);
        AppendCost(summary, cases, calibration);
        AppendSamples(summary, cases);
        return summary.ToString();
    }

    private static void AppendScore(StringBuilder summary, GateVerdict verdict, IReadOnlyList<CaseResult> cases, GateSettings settings)
    {
        var passed = cases.Count(c => c.Passed);
        summary.Append(CultureInfo.InvariantCulture, $"## Evals: {Headline(verdict)}\n\n");
        summary.Append(CultureInfo.InvariantCulture,
            $"**Score: {passed}/{cases.Count} cases passed ({RunGate.Percent(passed, cases.Count)}%).** The gate needs {settings.PassThresholdPercent}%, every adversarial case, and no judge errors.\n\n");
        if (verdict == GateVerdict.NotGated)
        {
            summary.Append("A filtered run (`ESHOP_EVALS_CASE`), so the gate was skipped.\n\n");
        }
    }

    private static string Headline(GateVerdict verdict) => verdict switch
    {
        GateVerdict.Passed => "passed",
        GateVerdict.Failed => "failed",
        GateVerdict.InfrastructureError => "infrastructure error",
        _ => "not gated",
    };

    private static void AppendReasons(StringBuilder summary, IReadOnlyList<string> reasons)
    {
        if (reasons.Count == 0)
        {
            return;
        }

        summary.Append("### Why the run failed\n\n");
        foreach (var reason in reasons)
        {
            summary.Append(CultureInfo.InvariantCulture, $"- {reason}\n");
        }
        summary.Append('\n');
    }

    private static void AppendCategories(StringBuilder summary, IReadOnlyList<CaseResult> cases)
    {
        summary.Append("### By category\n\n| Category | Passed |\n|---|---|\n");
        foreach (var category in cases.GroupBy(c => c.Category).OrderBy(g => g.Key))
        {
            summary.Append(CultureInfo.InvariantCulture, $"| {CategoryName(category.Key)} | {category.Count(c => c.Passed)}/{category.Count()} |\n");
        }
        summary.Append('\n');
    }

    private static void AppendFailingCases(StringBuilder summary, IReadOnlyList<CaseResult> cases)
    {
        var failing = cases.Where(c => !c.Passed).ToList();
        if (failing.Count == 0)
        {
            return;
        }

        summary.Append("### Failing cases\n\n");
        foreach (var evalCase in failing)
        {
            AppendFailingCase(summary, evalCase);
        }
        summary.Append('\n');
    }

    private static void AppendFailingCase(StringBuilder summary, CaseResult evalCase)
    {
        var knownMiss = evalCase.Samples[0].KnownMiss is { } note ? $" Known miss: {note}" : "";
        summary.Append(CultureInfo.InvariantCulture,
            $"- **{evalCase.Id}** ({CategoryName(evalCase.Category)}): {evalCase.SamplesPassed}/{evalCase.Samples.Count} samples passed.{knownMiss}\n");
        foreach (var sample in evalCase.Samples.Where(s => !s.Passed))
        {
            if (sample.InfrastructureError is { } error)
            {
                summary.Append(CultureInfo.InvariantCulture, $"  - Sample {sample.Iteration}: infrastructure error: {error}\n");
            }
            foreach (var failure in sample.Failures)
            {
                summary.Append(CultureInfo.InvariantCulture, $"  - Sample {sample.Iteration}: {failure.Check}: {failure.Reason}\n");
            }
        }
    }

    // What this run spent: each sample and the calibration set carry only the tokens of calls that reached a model.
    private static void AppendCost(StringBuilder summary, IReadOnlyList<CaseResult> cases, CalibrationOutcome? calibration)
    {
        var byModel = cases.SelectMany(c => c.Samples)
            .SelectMany(s => s.Usage)
            .Concat(calibration?.Usage ?? [])
            .GroupBy(u => u.Model)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new TokenUsage(g.Key, g.Sum(u => u.InputTokens), g.Sum(u => u.OutputTokens)))
            .ToList();

        summary.Append("### Tokens and estimated cost\n\n| Model | Input tokens | Output tokens | Estimated cost |\n|---|---:|---:|---:|\n");
        foreach (var usage in byModel)
        {
            summary.Append(CultureInfo.InvariantCulture,
                $"| {usage.Model} | {Tokens(usage.InputTokens)} | {Tokens(usage.OutputTokens)} | {Dollars(ModelPrices.Cost(usage))} |\n");
        }
        var total = byModel.Select(ModelPrices.Cost).Where(c => c is not null).Sum();
        summary.Append(CultureInfo.InvariantCulture,
            $"| **Total** | {Tokens(byModel.Sum(u => u.InputTokens))} | {Tokens(byModel.Sum(u => u.OutputTokens))} | **{Dollars(total)}** |\n\n");
        summary.Append("Only calls that reached a model count; a call replayed from the cache cost nothing this run.\n\n");
    }

    private static void AppendSamples(StringBuilder summary, IReadOnlyList<CaseResult> cases)
    {
        var samples = cases.SelectMany(c => c.Samples).ToList();
        summary.Append(CultureInfo.InvariantCulture,
            $"### Samples\n\n{samples.Count} samples: {samples.Count(s => s.Origin == SampleOrigin.Fresh)} fresh, {samples.Count(s => s.Origin == SampleOrigin.Cached)} cached.\n");
    }

    private static string CategoryName(CaseCategory category) => JsonNamingPolicy.KebabCaseLower.ConvertName(category.ToString());

    private static string Tokens(long count) => count.ToString("N0", CultureInfo.InvariantCulture);

    private static string Dollars(decimal? cost) => cost is { } dollars ? "$" + dollars.ToString("F3", CultureInfo.InvariantCulture) : "unknown";
}

/// <summary>Anthropic's list prices per million tokens, for the models the evals use.</summary>
internal static class ModelPrices
{
    private static readonly (string ModelPrefix, decimal Input, decimal Output)[] PerMillionTokens =
    [
        ("claude-haiku-4-5", 1m, 5m),
        ("claude-sonnet-5-5", 2m, 10m),
    ];

    /// <summary>The cost of <paramref name="usage"/>, or null for a model with no listed price.</summary>
    public static decimal? Cost(TokenUsage usage) =>
        PerMillionTokens.Where(p => usage.Model.StartsWith(p.ModelPrefix, StringComparison.Ordinal))
            .Select(p => (usage.InputTokens * p.Input + usage.OutputTokens * p.Output) / 1_000_000m)
            .Cast<decimal?>()
            .FirstOrDefault();
}
