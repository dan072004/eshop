using System.Text;
using Anthropic;

namespace ShoppingAssistant.Evals.Judge;

/// <summary>
/// Fakes the Anthropic Messages API, a boundary we don't own: each request is answered with the next judge reply,
/// sent back as the text of an assistant message, and its JSON body is recorded.
/// </summary>
internal sealed class FakeAnthropic(params string[] replies) : HttpMessageHandler
{
    private int _next;

    public List<JsonElement> Requests { get; } = [];

    /// <summary>A real Anthropic client whose requests go to this fake.</summary>
    public AnthropicClient Client() => new(new() { ApiKey = "sk-ant-test", HttpClient = new HttpClient(this), MaxRetries = 0 });

    /// <summary>The judge's reply: one verdict per rubric item, as the judge's structured output.</summary>
    public static string Verdicts(params (int Item, string Reason, bool Pass)[] verdicts) =>
        JsonSerializer.Serialize(new { Verdicts = verdicts.Select(v => new { v.Item, v.Reason, v.Pass }) }, JsonSerializerOptions.Web);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = await request.Content!.ReadAsStringAsync(cancellationToken);
        string reply;
        lock (Requests)
        {
            Requests.Add(JsonElement.Parse(body));
            reply = replies[Math.Min(_next++, replies.Length - 1)];
        }

        var message = new
        {
            id = "msg_test",
            type = "message",
            role = "assistant",
            model = "claude-sonnet-5-5",
            content = new[] { new { type = "text", text = reply } },
            stop_reason = "end_turn",
            stop_sequence = (string?)null,
            usage = new { input_tokens = 100, output_tokens = 20 },
        };
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(message), Encoding.UTF8, "application/json"),
        };
    }
}
