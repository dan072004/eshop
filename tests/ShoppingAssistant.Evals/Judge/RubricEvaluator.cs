using System.Text;
using eShop.Assistant.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using ShoppingAssistant.Evals.Cases;
using ShoppingAssistant.Evals.Checks;
using ShoppingAssistant.Evals.Gate;

namespace ShoppingAssistant.Evals.Judge;

/// <summary>
/// Grades the final answer against the case's <c>reference</c> and <c>rubric</c> with one judge call per sample. The
/// judge sees the question, the final answer, the reference, the rubric and the Policy Sections <c>search_policies</c>
/// returned in that run. It returns one <see cref="BooleanMetric"/> per rubric item, named
/// <c>"Rubric: {item}"</c>, with the judge's reason.
/// </summary>
/// <remarks>
/// The judge comes from the <see cref="ChatConfiguration"/> (<see cref="JudgeClient"/>), so Reporting can cache its
/// calls. When the judge call fails, Reporting records the exception as an error diagnostic on a metric named
/// <see cref="MetricName"/> instead; the run reports that as an infrastructure error, not a verdict.
/// </remarks>
public sealed class RubricEvaluator : IEvaluator
{
    public const string MetricName = "Rubric";

    public const string ItemPrefix = "Rubric: ";

    public const string Instructions = """
        You grade one answer given by the AdventureWorks Shopping Assistant, an online outdoor-gear store's chat
        assistant, against a rubric.

        You are given the Buyer's question, the Policy Sections the assistant's search_policies tool returned in that
        conversation, a reference answer written from the Policies and the store's data, the assistant's final answer,
        and numbered rubric items.

        Grade each rubric item on its own, as a yes/no question about the final answer:
        - pass is true only when the final answer meets the item as written.
        - Treat the reference answer and the Policy Sections as the truth. Facts the reference states (order numbers,
          dates, day counts, product names) are correct; an answer that contradicts them fails the item that covers them.
        - Wording, order and formatting don't matter, and dates may be written in any format, as long as the
          content the item asks for is right.
        - Grade only what the item asks. Don't fail an item for a problem that another item, or no item, covers.

        For each item give a reason of one or two sentences that names what in the answer decided the verdict, then
        the verdict. Return one verdict per rubric item, numbered as the items are.
        """;

    public IReadOnlyCollection<string> EvaluationMetricNames => [MetricName];

    public async ValueTask<EvaluationResult> EvaluateAsync(
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration = null,
        IEnumerable<EvaluationContext>? additionalContext = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chatConfiguration, nameof(chatConfiguration));
        var evalCase = EvalCaseContext.From(additionalContext);
        var conversation = new EvalConversation(messages, modelResponse);

        var response = await chatConfiguration.ChatClient.GetResponseAsync<JudgeVerdicts>(
            [new ChatMessage(ChatRole.User, Prompt(evalCase, conversation))],
            new ChatOptions { Instructions = Instructions },
            cancellationToken: cancellationToken);

        var verdicts = response.TryGetResult(out var result) ? result.Verdicts : null;
        var byItem = verdicts?.GroupBy(v => v.Item).ToDictionary(g => g.Key, g => g.First()) ?? [];
        if (verdicts is null || byItem.Count != evalCase.Rubric.Count || !Enumerable.Range(1, evalCase.Rubric.Count).All(byItem.ContainsKey))
        {
            throw new JudgeReplyException(evalCase.Id, evalCase.Rubric.Count, response.Text);
        }

        var evaluation = new EvaluationResult(evalCase.Rubric.Select((item, i) =>
        {
            var verdict = byItem[i + 1];
            var metric = new BooleanMetric(ItemPrefix + item, verdict.Pass, verdict.Reason)
            {
                Interpretation = new EvaluationMetricInterpretation(
                    verdict.Pass ? EvaluationRating.Good : EvaluationRating.Unacceptable, failed: !verdict.Pass, verdict.Reason),
            };
            metric.AddOrUpdateChatMetadata(response);
            return (EvaluationMetric)metric;
        }));
        return evaluation;
    }

    private static string Prompt(EvalCase evalCase, EvalConversation conversation)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine("<question>");
        foreach (var turn in evalCase.Turns)
        {
            prompt.AppendLine(turn);
        }
        prompt.AppendLine("</question>");

        prompt.AppendLine("<policy_sections>");
        var sections = PolicySections(conversation).ToList();
        if (sections.Count == 0)
        {
            prompt.AppendLine("(search_policies returned no Policy Sections in this conversation)");
        }
        foreach (var (citation, text) in sections)
        {
            prompt.AppendLine($"<section citation=\"{citation}\">").AppendLine(text).AppendLine("</section>");
        }
        prompt.AppendLine("</policy_sections>");

        prompt.AppendLine("<reference>").AppendLine(evalCase.Reference).AppendLine("</reference>");
        prompt.AppendLine("<answer>").AppendLine(conversation.FinalAnswer).AppendLine("</answer>");

        prompt.AppendLine("<rubric>");
        for (var i = 0; i < evalCase.Rubric.Count; i++)
        {
            prompt.AppendLine($"{i + 1}. {evalCase.Rubric[i]}");
        }
        prompt.AppendLine("</rubric>");
        return prompt.ToString();
    }

    private static IEnumerable<(string Citation, string Text)> PolicySections(EvalConversation conversation) =>
        conversation.ResultsOf(AssistantToolNames.SearchPolicies)
            .Where(r => r.ValueKind == JsonValueKind.Object && r.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
            .SelectMany(r => r.GetProperty("results").EnumerateArray())
            .Where(s => s.ValueKind == JsonValueKind.Object
                && s.TryGetProperty("citation", out var c) && c.ValueKind == JsonValueKind.String
                && s.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
            .Select(s => (s.GetProperty("citation").GetString()!, s.GetProperty("text").GetString()!))
            .DistinctBy(s => s.Item1);

    /// <summary>The judge's structured output.</summary>
    public sealed record JudgeVerdicts(IReadOnlyList<JudgeVerdict> Verdicts);

    /// <param name="Item">The rubric item's number, from 1.</param>
    /// <param name="Reason">What in the answer decided the verdict.</param>
    /// <param name="Pass">Whether the answer meets the item.</param>
    public sealed record JudgeVerdict(int Item, string Reason, bool Pass);
}

/// <summary>The judge replied, but not with one verdict per rubric item. Like a failed call, it is an infrastructure error.</summary>
public sealed class JudgeReplyException(string caseId, int rubricItems, string reply)
    : Exception($"Case '{caseId}': the judge did not return one verdict for each of the {rubricItems} rubric items. It replied: {reply}");

/// <summary>
/// The judge call failed (a rate limit that survived the SDK's retries, a model error, a reply without one verdict per
/// item), so there is no verdict. That is an infrastructure error, never a failed rubric item or a judge error.
/// </summary>
public sealed class JudgeFailedException(string id, string error)
    : Exception($"'{id}': the judge call failed, so there is no verdict: {error}")
{
    /// <summary>
    /// Throws when <paramref name="result"/> holds the error Reporting records in place of the rubric metrics when
    /// <see cref="RubricEvaluator"/> throws.
    /// </summary>
    public static void ThrowIfAny(string id, EvaluationResult result)
    {
        if (result.Metrics.TryGetValue(RubricEvaluator.MetricName, out var failed)
            && failed.Diagnostics?.FirstOrDefault(d => d.Severity == EvaluationDiagnosticSeverity.Error) is { } error)
        {
            throw new JudgeFailedException(id, error.Message);
        }
    }
}

/// <summary>What one judge call cost, as Reporting records it on each rubric metric (model, tokens, duration).</summary>
public static class JudgeUsage
{
    public static string Of(EvaluationResult result) =>
        result.Metrics.Values.FirstOrDefault(m => m.Name.StartsWith(RubricEvaluator.ItemPrefix, StringComparison.Ordinal))?.Metadata is { Count: > 0 } metadata
            ? "judge " + string.Join(", ", metadata.OrderBy(m => m.Key, StringComparer.Ordinal).Select(m => $"{m.Key}={m.Value}"))
            : "judge: no usage recorded";

    /// <summary>The judge call's tokens, as Reporting records them on each rubric metric; none when it recorded none.</summary>
    public static IReadOnlyList<TokenUsage> Tokens(EvaluationResult result) =>
        result.Metrics.Values.FirstOrDefault(m => m.Name.StartsWith(RubricEvaluator.ItemPrefix, StringComparison.Ordinal))?.Metadata is { } metadata
            && metadata.TryGetValue("eval-model", out var model)
            && metadata.TryGetValue("eval-input-tokens", out var inputTokens)
            && metadata.TryGetValue("eval-output-tokens", out var outputTokens)
            && long.TryParse(inputTokens, System.Globalization.CultureInfo.InvariantCulture, out var input)
            && long.TryParse(outputTokens, System.Globalization.CultureInfo.InvariantCulture, out var output)
            ? [new TokenUsage(model, input, output)]
            : [];
}
