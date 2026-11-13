using eShop.Assistant.Tools;
using Microsoft.Extensions.AI;

namespace ShoppingAssistant.Evals.Checks;

/// <summary>
/// What the checks read from a finished conversation: every tool call and its result, the answers, and the final answer.
/// The runner passes the conversation up to the last user turn as the messages and the reply to it as the response.
/// </summary>
public sealed class EvalConversation
{
    private readonly IReadOnlyList<ChatMessage> _messages;

    public EvalConversation(IEnumerable<ChatMessage> messages, ChatResponse modelResponse)
    {
        _messages = [.. messages, .. modelResponse.Messages];
        Calls = [.. _messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>()];
        FinalAnswer = modelResponse.Messages.LastOrDefault(m => m.Role == ChatRole.Assistant && m.Text.Length > 0)?.Text ?? "";

        // The greeting before the first user turn is the conversation class's, not an answer.
        var firstUserTurn = _messages.ToList().FindIndex(m => m.Role == ChatRole.User);
        Answers = [.. _messages.Skip(firstUserTurn + 1).Where(m => m.Role == ChatRole.Assistant && m.Text.Length > 0).Select(m => m.Text)];
    }

    public IReadOnlyList<FunctionCallContent> Calls { get; }

    /// <summary>Every answer the assistant gave to the case's turns, in order.</summary>
    public IReadOnlyList<string> Answers { get; }

    /// <summary>The text of the assistant's last answer, or empty when it gave none.</summary>
    public string FinalAnswer { get; }

    /// <summary>The results of every call to <paramref name="tool"/>, as JSON.</summary>
    public IEnumerable<JsonElement> ResultsOf(string tool)
    {
        var callIds = Calls.Where(c => c.Name == tool).Select(c => c.CallId).ToHashSet();
        return _messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>()
            .Where(r => callIds.Contains(r.CallId))
            .Select(r => r.Result switch
            {
                JsonElement element => element,
                string text => TryParse(text),
                var other => JsonSerializer.SerializeToElement(other),
            });
    }

    /// <summary>Every Policy Section object in the <c>results</c> of each <c>search_policies</c> call.</summary>
    public IEnumerable<JsonElement> PolicySearchResults() =>
        ResultsOf(AssistantToolNames.SearchPolicies)
            .Where(r => r.ValueKind == JsonValueKind.Object && r.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
            .SelectMany(r => r.GetProperty("results").EnumerateArray())
            .Where(s => s.ValueKind == JsonValueKind.Object);

    // A tool that failed returns plain text such as "Unable to…", which holds no JSON values.
    private static JsonElement TryParse(string text)
    {
        try
        {
            return JsonElement.Parse(text);
        }
        catch (JsonException)
        {
            return JsonSerializer.SerializeToElement(text);
        }
    }
}
