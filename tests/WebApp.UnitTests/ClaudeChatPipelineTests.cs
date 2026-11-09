using eShop.WebApp.Chatbot;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

namespace eShop.WebApp.UnitTests;

[TestClass]
public class ClaudeChatPipelineTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task WrapsAnyInnerClientWithFunctionInvocationThinkingOffAndTheDefaultModel()
    {
        var model = new ToolCallingChatClient();
        var lookups = 0;
        var lookup = AIFunctionFactory.Create(() => { lookups++; return "in stock"; }, "lookup");
        var client = ClaudeChatPipeline.Build(model, new ConfigurationBuilder().Build());

        var response = await client.GetResponseAsync(
            "Is the helmet in stock?", new ChatOptions { Tools = [lookup] }, TestContext.CancellationToken);

        Assert.AreEqual("Yes, it is.", response.Text);
        Assert.AreEqual(1, lookups);
        Assert.AreEqual(
            ("claude-haiku-4-5", (int?)1024, (ReasoningEffort?)ReasoningEffort.None),
            (model.FirstOptions!.ModelId, model.FirstOptions.MaxOutputTokens, model.FirstOptions.Reasoning?.Effort));
    }

    // The model is a boundary we don't own: it asks for the lookup tool once, then answers.
    private sealed class ToolCallingChatClient : IChatClient
    {
        private int _calls;

        public ChatOptions? FirstOptions { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            FirstOptions ??= options?.Clone();
            var reply = _calls++ == 0
                ? new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("call-1", "lookup")])
                : new ChatMessage(ChatRole.Assistant, "Yes, it is.");
            return Task.FromResult(new ChatResponse(reply));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
