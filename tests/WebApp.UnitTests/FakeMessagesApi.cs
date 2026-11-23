using System.Net;
using System.Text;
using System.Text.Json;
using Anthropic;

namespace eShop.WebApp.UnitTests;

/// <summary>
/// Fakes the Anthropic Messages API, a boundary we don't own: answers each request with the next of
/// <paramref name="messages"/>: PascalCase anonymous types, sent in the API's own snake_case JSON.
/// </summary>
internal sealed class FakeMessagesApi(params object[] messages) : HttpMessageHandler
{
    private static readonly JsonSerializerOptions SnakeCase = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    private readonly List<JsonElement> _requests = [];
    private int _next;

    /// <summary>The JSON body of each request sent to this fake, in order.</summary>
    public IReadOnlyList<JsonElement> Requests
    {
        get { lock (_requests) { return [.. _requests]; } }
    }

    /// <summary>A real Anthropic client whose requests go to this fake.</summary>
    public AnthropicClient Client() => new(new() { ApiKey = "sk-ant-test", HttpClient = new HttpClient(this), MaxRetries = 0 });

    /// <summary>An assistant message asking for <paramref name="toolName"/>, with the given usage.</summary>
    public static object ToolUse(string toolName, object usage) => Message(
        new { Type = "tool_use", Id = "toolu_1", Name = toolName, Input = new { } }, "tool_use", usage);

    /// <summary>An assistant message answering with <paramref name="text"/>, with the given usage.</summary>
    public static object Answer(string text, object usage) => Message(new { Type = "text", Text = text }, "end_turn", usage);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content is not null)
        {
            var body = JsonDocument.Parse(await request.Content.ReadAsStringAsync(cancellationToken)).RootElement;
            lock (_requests) { _requests.Add(body); }
        }

        var message = messages[Interlocked.Increment(ref _next) - 1];
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(message, SnakeCase), Encoding.UTF8, "application/json"),
        };
    }

    private static object Message(object content, string stopReason, object usage) => new
    {
        Id = "msg_test",
        Type = "message",
        Role = "assistant",
        Model = "claude-haiku-4-5",
        Content = new[] { content },
        StopReason = stopReason,
        StopSequence = (string?)null,
        Usage = usage,
    };
}
