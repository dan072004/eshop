using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace eShop.WebApp.Chatbot;

/// <summary>
/// Cancels a whole turn, every model request and tool call below it, once <paramref name="deadline"/> has passed, so a
/// slow or failing provider can't keep the Buyer waiting longer than that.
/// </summary>
public sealed class TurnDeadlineChatClient(IChatClient inner, TimeSpan deadline) : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        using var turn = StartTurn(cancellationToken);
        return await base.GetResponseAsync(messages, options, turn.Token);
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var turn = StartTurn(cancellationToken);
        await foreach (var update in base.GetStreamingResponseAsync(messages, options, turn.Token))
        {
            yield return update;
        }
    }

    private CancellationTokenSource StartTurn(CancellationToken cancellationToken)
    {
        var turn = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        turn.CancelAfter(deadline);
        return turn;
    }
}
