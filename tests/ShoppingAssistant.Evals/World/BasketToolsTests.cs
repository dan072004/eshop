using eShop.Assistant.Tools;
using eShop.WebApp.Chatbot;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ShoppingAssistant.Evals.World;

/// <summary>
/// Basket tools are out of the evals' scope: their stub throws, and the conversation class turns that into an
/// "Unable to…" tool result the model reads. The model is faked here, since it is a system we don't own.
/// </summary>
public sealed class BasketToolsTests
{
    [Fact]
    public async Task ABasketToolCallGetsUnableToFromTheConversation()
    {
        var world = EvalWorld.Load();
        var services = new ServiceCollection().AddLogging();
        services.AddEvalWorld(world, "alice");
        var provider = services.BuildServiceProvider();
        var basket = new OutOfScopeBasket();
        var model = new ScriptedModel(new FunctionCallContent("call-1", "AddToCart", new Dictionary<string, object?> { ["itemId"] = 9 }));
        var chat = new ChatState(
            basket, basket, world.Buyer("alice").ToPrincipal(), provider.GetRequiredService<AssistantTools>(),
            provider.GetRequiredService<ILoggerFactory>(), new FunctionInvokingChatClient(model));

        await chat.AddUserMessageAsync("add the GPS watch to my cart", () => { });

        var results = chat.Messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>();
        Assert.Equal(["Unable to add the item to the cart."], results.Select(r => r.Result?.ToString()));
    }

    // Asks for one tool call, then answers with text once it has the tool's result.
    private sealed class ScriptedModel(FunctionCallContent call) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(messages.Any(m => m.Contents.OfType<FunctionResultContent>().Any())
                ? new ChatResponse(new ChatMessage(ChatRole.Assistant, "Sorry, I couldn't add it."))
                : new ChatResponse(new ChatMessage(ChatRole.Assistant, [call])));

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
