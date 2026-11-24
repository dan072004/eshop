using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.AI;

namespace eShop.WebApp.Chatbot;

/// <summary>
/// Adds what the OpenTelemetry step doesn't write about a Claude model call: its cache-write tokens and, for a model with
/// a listed price, its estimated cost on its <c>chat</c> span, the Turn's running total on the <c>orchestrate_tools</c>
/// span, and an increase of the <c>eshop.assistant.estimated_cost</c> counter. Sits directly inside that step, so the
/// current span is the model call's.
/// </summary>
public sealed class ClaudeUsageTelemetryChatClient(IChatClient inner) : DelegatingChatClient(inner)
{
    /// <summary>The meter of the estimated cost counter, which WebApp collects.</summary>
    public const string MeterName = "eShop.WebApp.ShoppingAssistant";

    private const string CacheWriteTagName = "gen_ai.usage.cache_creation.input_tokens";
    private const string CostTagName = "eshop.assistant.estimated_cost_usd";
    private const string ModelTagName = "gen_ai.request.model";
    private const string OperationTagName = "gen_ai.operation.name";
    private const string ChatOperation = "chat";
    private const string TurnOperation = "orchestrate_tools";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<double> EstimatedCost = Meter.CreateCounter<double>(
        "eshop.assistant.estimated_cost", "USD", "Estimated cost of the Shopping Assistant's model calls, from list prices.");

    // shortcut: streaming responses aren't tagged; the Shopping Assistant doesn't stream. Override streaming if it does.
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var response = await base.GetResponseAsync(messages, options, cancellationToken);
        var modelCall = CurrentModelCallSpan();
        var tokens = ClaudeTokenCounts.FromAdapterUsage(response.Usage);
        modelCall?.SetTag(CacheWriteTagName, tokens.CacheWrite);
        if (options?.ModelId is { } modelId && ClaudePrices.EstimateCostUsd(modelId, tokens) is { } cost)
        {
            RecordCost(modelCall, modelId, cost);
        }

        return response;
    }

    // Without a listener there is no chat span, and the current span belongs to someone else.
    private static Activity? CurrentModelCallSpan()
        => Activity.Current is { } span && span.GetTagItem(OperationTagName) is ChatOperation ? span : null;

    private static void RecordCost(Activity? modelCall, string modelId, decimal cost)
    {
        modelCall?.SetTag(CostTagName, (double)cost);
        if (modelCall?.Parent is { OperationName: TurnOperation } turn)
        {
            // Added up in decimal, so the Turn's total is the exact sum of its calls' costs.
            var turnCost = turn.GetTagItem(CostTagName) is double soFar ? (decimal)soFar : 0m;
            turn.SetTag(CostTagName, (double)(turnCost + cost));
        }

        EstimatedCost.Add((double)cost, new KeyValuePair<string, object?>(ModelTagName, modelId));
    }
}
