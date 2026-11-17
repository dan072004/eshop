using System.Diagnostics;
using System.Security.Claims;
using eShop.Assistant.Tools;
using eShop.WebApp.Chatbot;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace eShop.WebApp.UnitTests;

[TestClass]
public class ShoppingAssistantTests
{
    private const string Unavailable = "The Shopping Assistant isn't available right now. Please try again in a moment.";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task OffersThePolicyAndOrderToolsAlongsideItsOtherTools()
    {
        var chatClient = new RecordingChatClient();
        var chat = CreateChat(chatClient);

        await chat.AddUserMessageAsync("can I return a GPS watch after 45 days?", () => { });

        var toolNames = chatClient.Options!.Tools!.Select(tool => tool.Name).ToArray();
        CollectionAssert.IsSubsetOf(
            new[] { "search_policies", "list_my_orders", "get_order", "search_catalog", "GetUserInfo", "AddToCart", "GetCartContents" },
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
    public void SystemPromptRepeatsTheCitationRule()
    {
        var chat = CreateChat(new RecordingChatClient());

        var prompt = chat.Messages.Single(message => message.Role == ChatRole.System).Text;
        Assert.Contains("cite each rule with its full citation value, word for word", prompt);
    }

    [TestMethod]
    public async Task AFailingModelShowsTheUnavailableNoticeAndKeepsTheBuyersMessageLast()
    {
        var chat = CreateChat(ClaudeChatPipeline.Build(new FailingChatClient(), new ConfigurationBuilder().Build()));

        await chat.AddUserMessageAsync("is the trail helmet in stock?", () => { });

        Assert.AreEqual(Unavailable, chat.UnavailableNotice);
        Assert.AreEqual((ChatRole.User, "is the trail helmet in stock?"), (chat.Messages[^1].Role, chat.Messages[^1].Text));
        Assert.AreEqual(1, chat.Messages.Count(message => message.Role == ChatRole.Assistant), "only the greeting");
    }

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task AModelThatNeverAnswersShowsTheUnavailableNoticeAtTheTurnDeadline()
    {
        var pipeline = ClaudeChatPipeline.Build(
            new SilentChatClient(), new ConfigurationBuilder().Build(), turnDeadline: TimeSpan.FromMilliseconds(50));
        var chat = CreateChat(pipeline);

        await chat.AddUserMessageAsync("is the trail helmet in stock?", () => { });

        Assert.AreEqual(Unavailable, chat.UnavailableNotice);
        Assert.AreEqual(ChatRole.User, chat.Messages[^1].Role);
    }

    [TestMethod]
    public async Task SendingAgainAfterAFailureClearsTheNoticeAndKeepsTheEarlierQuestion()
    {
        var model = new FailsOnceChatClient();
        var chat = CreateChat(ClaudeChatPipeline.Build(model, new ConfigurationBuilder().Build()));
        await chat.AddUserMessageAsync("is the trail helmet in stock?", () => { });

        await chat.AddUserMessageAsync("hello?", () => { });

        Assert.AreEqual("", chat.UnavailableNotice);
        CollectionAssert.AreEqual(
            new[] { "is the trail helmet in stock?", "hello?" },
            model.LastRequest.Where(message => message.Role == ChatRole.User).Select(message => message.Text).ToArray());
        Assert.AreEqual((ChatRole.Assistant, "Yes, it is."), (chat.Messages[^1].Role, chat.Messages[^1].Text));
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

    // The model is a boundary we don't own; this one fails every request, as an overloaded provider would.
    private sealed class FailingChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("Overloaded");

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    // The model is a boundary we don't own; this one hangs like a stalled provider until it is cancelled.
    private sealed class SilentChatClient : IChatClient
    {
        public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new UnreachableException();
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    // The model is a boundary we don't own; this one fails its first request, then answers and records what it was sent.
    private sealed class FailsOnceChatClient : IChatClient
    {
        private bool _failed;

        public IReadOnlyList<ChatMessage> LastRequest { get; private set; } = [];

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            if (!_failed)
            {
                _failed = true;
                throw new HttpRequestException("Overloaded");
            }

            LastRequest = [.. messages];
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Yes, it is.")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
