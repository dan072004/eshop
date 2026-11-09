using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using ShoppingAssistant.Evals.Cases;
using ShoppingAssistant.Evals.Running;

namespace ShoppingAssistant.Evals.Checks;

/// <summary>
/// A conversation written by hand, shaped like the one the Shopping Assistant's conversation class leaves behind: the
/// system prompt and greeting, then user turns, tool calls with their results, and answers. No model is involved.
/// </summary>
internal sealed class HandBuiltConversation
{
    private readonly List<ChatMessage> _messages =
    [
        new(ChatRole.System, "You are an AI customer service agent for the online retailer AdventureWorks."),
        new(ChatRole.Assistant, "Hi! I'm the AdventureWorks Shopping Assistant. How can I help?"),
    ];

    private int _calls;

    public HandBuiltConversation User(string text)
    {
        _messages.Add(new(ChatRole.User, text));
        return this;
    }

    /// <summary>The model calls <paramref name="tool"/> and gets <paramref name="resultJson"/> back, as a tool function returns it.</summary>
    public HandBuiltConversation Tool(string tool, string resultJson, Dictionary<string, object?>? arguments = null)
    {
        var callId = $"call-{++_calls}";
        _messages.Add(new(ChatRole.Assistant, [new FunctionCallContent(callId, tool, arguments ?? [])]));
        _messages.Add(new(ChatRole.Tool, [new FunctionResultContent(callId, JsonElement.Parse(resultJson))]));
        return this;
    }

    public HandBuiltConversation Answer(string text)
    {
        _messages.Add(new(ChatRole.Assistant, text));
        return this;
    }

    /// <summary>Evaluates the conversation as the runner does: everything up to the last user turn, then the reply to it.</summary>
    public async Task<EvaluationMetric> EvaluateAsync(IEvaluator evaluator, EvalCase evalCase)
    {
        var transcript = new CaseTranscript(_messages);
        var result = await evaluator.EvaluateAsync(
            transcript.Prompt,
            transcript.Reply,
            additionalContext: [new EvalCaseContext(evalCase)],
            cancellationToken: TestContext.Current.CancellationToken);

        return Assert.Single(result.Metrics.Values);
    }
}
