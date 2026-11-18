using System.Net;
using System.Text;
using System.Text.Json;
using Anthropic;

namespace eShop.WebApp.UnitTests;

/// <summary>
/// Fakes the Anthropic Messages API, a boundary we don't own: answers each request with the next of
/// <paramref name="messages"/>, written in the API's own snake_case JSON.
/// </summary>
internal sealed class FakeMessagesApi(params object[] messages) : HttpMessageHandler
{
    private int _next;

    /// <summary>A real Anthropic client whose requests go to this fake.</summary>
    public AnthropicClient Client() => new(new() { ApiKey = "sk-ant-test", HttpClient = new HttpClient(this), MaxRetries = 0 });

    /// <summary>An assistant message asking for <paramref name="toolName"/>, with the given usage.</summary>
    public static object ToolUse(string toolName, object usage) => Message(
        new { type = "tool_use", id = "toolu_1", name = toolName, input = new { } }, "tool_use", usage);

    /// <summary>An assistant message answering with <paramref name="text"/>, with the given usage.</summary>
    public static object Answer(string text, object usage) => Message(new { type = "text", text }, "end_turn", usage);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var message = messages[Interlocked.Increment(ref _next) - 1];
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(message), Encoding.UTF8, "application/json"),
        });
    }

    private static object Message(object content, string stopReason, object usage) => new
    {
        id = "msg_test",
        type = "message",
        role = "assistant",
        model = "claude-haiku-4-5",
        content = new[] { content },
        stop_reason = stopReason,
        stop_sequence = (string?)null,
        usage,
    };
}
