using eShop.Assistant.Tools;
using Microsoft.Extensions.AI.Evaluation;
using ShoppingAssistant.Evals.Cases;

namespace ShoppingAssistant.Evals.Checks;

/// <summary>
/// Every Citation in the final answer appeared as a <c>citation</c> value in a <c>search_policies</c> result in the
/// same run, so the assistant can't name a source it never received.
/// </summary>
/// <remarks>
/// A Citation is <c>"{Policy title} › {heading}"</c>, so each <c>›</c> in the answer marks one. It is grounded when a
/// returned Citation sits exactly around it. An intro section's Citation is the bare Policy title, which has no
/// <c>›</c> to find; <c>mustCite</c> and the judge cover those.
/// </remarks>
public sealed class CitationsGroundedEvaluator : DeterministicCheck
{
    private const char Separator = '›';

    // What usually wraps a Citation in an answer; colons and commas appear inside Policy titles and headings.
    private static readonly char[] Delimiters = ['\n', '\r', '(', ')', '[', ']', '*', '_', '"', '“', '”', '«', '»', '`'];

    public override string MetricName => "Citations grounded";

    protected override EvaluationMetric Check(EvalConversation conversation, EvalCase evalCase)
    {
        var returned = conversation.ResultsOf(AssistantToolNames.SearchPolicies)
            .SelectMany(Citations)
            .Where(c => c.Contains(Separator))
            .Distinct()
            .ToList();

        var answer = conversation.FinalAnswer;
        List<string> grounded = [];
        List<string> ungrounded = [];
        for (var at = answer.IndexOf(Separator); at >= 0; at = answer.IndexOf(Separator, at + 1))
        {
            var match = returned.FirstOrDefault(c => SitsAround(answer, at, c));
            (match is null ? ungrounded : grounded).Add(match ?? Fragment(answer, at));
        }

        if (ungrounded.Count > 0)
        {
            return Verdict(false, $"Not returned by search_policies in this run: {Quoted(ungrounded)}.");
        }

        return Verdict(true, grounded.Count == 0 ? "No Citation in the answer." : $"Every Citation came from search_policies: {Quoted(grounded)}.");
    }

    private static bool SitsAround(string answer, int separatorAt, string citation)
    {
        var start = separatorAt - citation.IndexOf(Separator);
        return start >= 0 && string.CompareOrdinal(answer, start, citation, 0, citation.Length) == 0;
    }

    // The text around an unmatched separator, up to whatever wraps it, for the reason.
    private static string Fragment(string answer, int separatorAt)
    {
        var start = answer.LastIndexOfAny(Delimiters, separatorAt) + 1;
        var end = answer.IndexOfAny(Delimiters, separatorAt);
        return answer[start..(end < 0 ? answer.Length : end)].Trim();
    }

    private static IEnumerable<string> Citations(JsonElement result) =>
        result.ValueKind == JsonValueKind.Object
            && result.TryGetProperty("results", out var results)
            && results.ValueKind == JsonValueKind.Array
            ? results.EnumerateArray()
                .Where(r => r.ValueKind == JsonValueKind.Object && r.TryGetProperty("citation", out var c) && c.ValueKind == JsonValueKind.String)
                .Select(r => r.GetProperty("citation").GetString()!)
            : [];
}
