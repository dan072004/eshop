using System.Diagnostics;
using System.Globalization;
using Anthropic;
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

    [TestMethod]
    public async Task ATurnShowsEachModelCallWithItsTokensAndCacheTokensUnderOneSpanWithTheTurnsTotals()
    {
        var messagesApi = new FakeMessagesApi(
            FakeMessagesApi.ToolUse("lookup", new { InputTokens = 100, OutputTokens = 20, CacheReadInputTokens = 1000, CacheCreationInputTokens = 200 }),
            FakeMessagesApi.Answer("Yes, it is.", new { InputTokens = 150, OutputTokens = 10, CacheReadInputTokens = 1200, CacheCreationInputTokens = 50 }));
        var lookup = AIFunctionFactory.Create(() => "in stock", "lookup");
        var client = ClaudeChatPipeline.Build(messagesApi.Client().AsIChatClient(), new ConfigurationBuilder().Build());
        using var spans = new SpanRecorder();

        await spans.RecordAsync(() => client.GetResponseAsync(
            "Is the helmet in stock?", new ChatOptions { Tools = [lookup] }, TestContext.CancellationToken));

        // Anthropic's input_tokens excludes cached tokens; the adapter adds reads and writes back in (100 + 1000 + 200).
        CollectionAssert.AreEqual(
            new (string, string?, long?, long?, long?, long?)[]
            {
                ("chat claude-haiku-4-5", "orchestrate_tools", 1300, 20, 1000, 200),
                ("execute_tool lookup", "orchestrate_tools", null, null, null, null),
                ("chat claude-haiku-4-5", "orchestrate_tools", 1400, 10, 1200, 50),
                ("orchestrate_tools", "test", 2700, 30, null, null),
            },
            spans.Stopped.Select(span => (span.DisplayName, span.Parent?.DisplayName,
                Count(span, "gen_ai.usage.input_tokens"), Count(span, "gen_ai.usage.output_tokens"),
                Count(span, "gen_ai.usage.cache_read.input_tokens"), Count(span, "gen_ai.usage.cache_creation.input_tokens")))
                .ToArray());
    }

    private static long? Count(Activity span, string tag) =>
        span.GetTagItem(tag) is { } value ? Convert.ToInt64(value, CultureInfo.InvariantCulture) : null;

    // Records the Microsoft.Extensions.AI spans of one Turn; tests run in parallel, so it keeps only its own trace.
    private sealed class SpanRecorder : IDisposable
    {
        private const string TestSourceName = nameof(ClaudeChatPipelineTests);
        private static readonly ActivitySource TestSource = new(TestSourceName);
        private readonly ActivityListener _listener;
        private readonly List<Activity> _stopped = [];
        private ActivityTraceId _traceId;

        public SpanRecorder()
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name is "Experimental.Microsoft.Extensions.AI" or TestSourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = Record,
            };
            ActivitySource.AddActivityListener(_listener);
        }

        public IReadOnlyList<Activity> Stopped
        {
            get { lock (_stopped) { return [.. _stopped]; } }
        }

        public async Task RecordAsync(Func<Task> turn)
        {
            using (var test = TestSource.StartActivity("test")!)
            {
                _traceId = test.TraceId;
                await turn();
            }
        }

        public void Dispose() => _listener.Dispose();

        private void Record(Activity span)
        {
            if (span.TraceId != _traceId || span.Source.Name == TestSourceName)
            {
                return;
            }

            lock (_stopped)
            {
                _stopped.Add(span);
            }
        }
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
