using System.Security.Claims;
using eShop.Assistant.Tools;
using eShop.WebApp.Chatbot;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace eShop.WebApp.UnitTests;

[TestClass]
public class ShoppingAssistantTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task OffersSearchPoliciesAlongsideItsOtherTools()
    {
        var chatClient = new RecordingChatClient();
        var chat = CreateChat(chatClient);

        await chat.AddUserMessageAsync("can I return a GPS watch after 45 days?", () => { });

        var toolNames = chatClient.Options!.Tools!.Select(tool => tool.Name).ToArray();
        CollectionAssert.IsSubsetOf(
            new[] { "search_policies", "search_catalog", "GetUserInfo", "AddToCart", "GetCartContents" },
            toolNames);
    }

    [TestMethod]
    public void GreetsAsTheShoppingAssistant()
    {
        var chat = CreateChat(new RecordingChatClient());

        var greeting = chat.Messages.Single(message => message.Role == ChatRole.Assistant).Text;
        Assert.Contains("Shopping Assistant", greeting);
        Assert.DoesNotContain("Concierge", greeting);
    }

    [TestMethod]
    public void SystemPromptCoversPoliciesAndOrdersAndRepeatsTheCitationRule()
    {
        var chat = CreateChat(new RecordingChatClient());

        var prompt = chat.Messages.Single(message => message.Role == ChatRole.System).Text;
        Assert.Contains("Policies", prompt);
        Assert.Contains("orders", prompt);
        Assert.Contains("cite each rule with its citation value", prompt);
    }

    private static ChatState CreateChat(IChatClient chatClient)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddAssistantTools();
        var assistantTools = services.BuildServiceProvider().GetRequiredService<AssistantTools>();

        // Catalog and basket aren't touched unless the model calls the WebApp-only tools, which the fake never does.
        return new ChatState(null!, null!, new ClaimsPrincipal(), assistantTools, NullLoggerFactory.Instance, chatClient);
    }

    // The model is a boundary we don't own; this fake records what the Shopping Assistant offers it.
    private sealed class RecordingChatClient : IChatClient
    {
        public ChatOptions? Options { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Options = options;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Done.")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
