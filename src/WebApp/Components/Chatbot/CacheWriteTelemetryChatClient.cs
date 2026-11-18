using System.Diagnostics;
using Microsoft.Extensions.AI;

namespace eShop.WebApp.Chatbot;

/// <summary>
/// Adds a model call's cache-write tokens to its <c>chat</c> span, which the OpenTelemetry step doesn't write (it does
/// write cache reads). Sits directly inside that step, so the current span is the model call's.
/// </summary>
public sealed class CacheWriteTelemetryChatClient(IChatClient inner) : DelegatingChatClient(inner)
{
    private const string AdapterCountName = "CacheCreationInputTokens"; // The Anthropic adapter's additional count.
    private const string TagName = "gen_ai.usage.cache_creation.input_tokens";
    private const string OperationTagName = "gen_ai.operation.name";
    private const string ChatOperation = "chat";

    // shortcut: streaming responses aren't tagged; the Shopping Assistant doesn't stream. Override streaming if it does.
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var response = await base.GetResponseAsync(messages, options, cancellationToken);
        TagCacheWrites(response.Usage);
        return response;
    }

    private static void TagCacheWrites(UsageDetails? usage)
    {
        // Without a listener there is no chat span, and the current span belongs to someone else.
        if (Activity.Current is { } span && span.GetTagItem(OperationTagName) is ChatOperation
            && usage?.AdditionalCounts?.TryGetValue(AdapterCountName, out var cacheWrites) == true)
        {
            span.SetTag(TagName, cacheWrites);
        }
    }
}
