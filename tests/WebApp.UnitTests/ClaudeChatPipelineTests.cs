using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Text.Json;
using Anthropic;
using MessageCreateParams = Anthropic.Models.Messages.MessageCreateParams;
using eShop.WebApp.Chatbot;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

namespace eShop.WebApp.UnitTests;

[TestClass]
public class ClaudeChatPipelineTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task WrapsAnyInnerClientWithFunctionInvocation()
    {
        var lookups = 0;
        var lookup = AIFunctionFactory.Create(() => { lookups++; return "in stock"; }, "lookup");
        var client = ClaudeChatPipeline.Build(new ToolCallingChatClient(), new ConfigurationBuilder().Build());

        var response = await client.GetResponseAsync(
            "Is the helmet in stock?", new ChatOptions { Tools = [lookup] }, TestContext.CancellationToken);

        Assert.AreEqual("Yes, it is.", response.Text);
        Assert.AreEqual(1, lookups);
    }

    [TestMethod]
    public async Task ATurnShowsEachModelCallWithItsTokensCacheTokensAndCostUnderOneSpanWithTheTurnsTotals()
    {
        var client = ClaudeChatPipeline.Build(TwoCallTurn().Client().AsIChatClient(), new ConfigurationBuilder().Build());
        using var telemetry = new TelemetryRecorder();

        await telemetry.RecordAsync(() => AskAboutTheHelmet(client));

        // Anthropic's input_tokens excludes cached tokens; the adapter adds reads and writes back in (100 + 1000 + 200).
        // Haiku is $1 in and $5 out per million: 100 + 20 x 5 + 1000 x 0.1 + 200 x 1.25 = 550 millionths of a dollar.
        CollectionAssert.AreEqual(
            new (string, string?, long?, long?, long?, long?, double?)[]
            {
                ("chat claude-haiku-4-5", "orchestrate_tools", 1300, 20, 1000, 200, 0.00055),
                ("execute_tool lookup", "orchestrate_tools", null, null, null, null, null),
                ("chat claude-haiku-4-5", "orchestrate_tools", 1400, 10, 1200, 50, 0.0003825),
                ("orchestrate_tools", "test", 2700, 30, null, null, 0.0009325),
            },
            telemetry.Spans.Select(span => (span.DisplayName, span.Parent?.DisplayName,
                Count(span, "gen_ai.usage.input_tokens"), Count(span, "gen_ai.usage.output_tokens"),
                Count(span, "gen_ai.usage.cache_read.input_tokens"), Count(span, "gen_ai.usage.cache_creation.input_tokens"),
                span.GetTagItem("eshop.assistant.estimated_cost_usd") as double?))
                .ToArray());
        CollectionAssert.AreEqual(
            new[] { (0.00055, "claude-haiku-4-5"), (0.0003825, "claude-haiku-4-5") },
            telemetry.CostIncreases.ToArray());
        Assert.AreEqual(0.0009325m, telemetry.CostIncreases.Sum(increase => (decimal)increase.Cost));
    }

    [TestMethod]
    public async Task AModelWithNoListedPriceShowsNoCostOnItsSpansAndAddsNothingToTheCostCounter()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Claude:ModelId"] = "claude-unlisted-1" })
            .Build();
        var client = ClaudeChatPipeline.Build(TwoCallTurn().Client().AsIChatClient(), configuration);
        using var telemetry = new TelemetryRecorder();

        await telemetry.RecordAsync(() => AskAboutTheHelmet(client));

        CollectionAssert.AreEqual(
            new (string, long?, object?)[]
            {
                ("chat claude-unlisted-1", 1300, null),
                ("execute_tool lookup", null, null),
                ("chat claude-unlisted-1", 1400, null),
                ("orchestrate_tools", 2700, null),
            },
            telemetry.Spans.Select(span => (span.DisplayName, Count(span, "gen_ai.usage.input_tokens"),
                span.GetTagItem("eshop.assistant.estimated_cost_usd"))).ToArray());
        Assert.IsEmpty(telemetry.CostIncreases);
    }

    [TestMethod]
    public async Task EveryRequestToAnthropicAsksForPromptCachingWithTheDefaultModelOutputLimitAndThinkingDisabled()
    {
        var anthropic = TwoCallTurn();
        var client = ClaudeChatPipeline.Build(anthropic.Client().AsIChatClient(), new ConfigurationBuilder().Build());

        await AskAboutTheHelmet(client);

        var ephemeral = JsonDocument.Parse("""{ "type": "ephemeral" }""").RootElement;
        var disabled = JsonDocument.Parse("""{ "type": "disabled" }""").RootElement;
        Assert.HasCount(2, anthropic.Requests);
        foreach (var request in anthropic.Requests)
        {
            Assert.AreEqual("claude-haiku-4-5", request.GetProperty("model").GetString());
            Assert.AreEqual(1024, request.GetProperty("max_tokens").GetInt32());
            Assert.IsTrue(request.TryGetProperty("thinking", out var thinking), request.ToString());
            Assert.IsTrue(JsonElement.DeepEquals(disabled, thinking), request.ToString());
            Assert.IsTrue(request.TryGetProperty("cache_control", out var cacheControl), request.ToString());
            Assert.IsTrue(JsonElement.DeepEquals(ephemeral, cacheControl), request.ToString());
        }
    }

    [TestMethod]
    public async Task ARawRepresentationFactoryTheCallerSetTakesPrecedenceOverPromptCaching()
    {
        var anthropic = new FakeMessagesApi(FakeMessagesApi.Answer("Yes, it is.", new { InputTokens = 10, OutputTokens = 5 }));
        var client = ClaudeChatPipeline.Build(anthropic.Client().AsIChatClient(), new ConfigurationBuilder().Build());
        var options = new ChatOptions
        {
            RawRepresentationFactory = _ => new MessageCreateParams { Model = "claude-haiku-4-5", MaxTokens = 256, Messages = [] },
        };

        await client.GetResponseAsync("Is the helmet in stock?", options, TestContext.CancellationToken);

        var request = anthropic.Requests.Single();
        Assert.AreEqual(256, request.GetProperty("max_tokens").GetInt32());
        Assert.IsFalse(request.TryGetProperty("cache_control", out _), request.ToString());
    }

    // The model asks for the lookup tool, then answers; each call has its own uncached, output, cache-read and cache-write tokens.
    private static FakeMessagesApi TwoCallTurn() => new(
        FakeMessagesApi.ToolUse("lookup", new { InputTokens = 100, OutputTokens = 20, CacheReadInputTokens = 1000, CacheCreationInputTokens = 200 }),
        FakeMessagesApi.Answer("Yes, it is.", new { InputTokens = 150, OutputTokens = 10, CacheReadInputTokens = 1200, CacheCreationInputTokens = 50 }));

    private Task<ChatResponse> AskAboutTheHelmet(IChatClient client) => client.GetResponseAsync(
        "Is the helmet in stock?",
        new ChatOptions { Tools = [AIFunctionFactory.Create(() => "in stock", "lookup")] },
        TestContext.CancellationToken);

    private static long? Count(Activity span, string tag) =>
        span.GetTagItem(tag) is { } value ? Convert.ToInt64(value, CultureInfo.InvariantCulture) : null;

    // Records one Turn's Microsoft.Extensions.AI spans and cost counter increases. Tests run in parallel, so it keeps only
    // its own trace; a counter increase is recorded while its model call's span is current, so it belongs to that trace too.
    private sealed class TelemetryRecorder : IDisposable
    {
        private const string TestSourceName = nameof(ClaudeChatPipelineTests);
        private static readonly ActivitySource TestSource = new(TestSourceName);
        private readonly ActivityListener _spanListener;
        private readonly MeterListener _meterListener = new();
        private readonly List<Activity> _spans = [];
        private readonly List<(double Cost, string? Model)> _costIncreases = [];
        private ActivityTraceId _traceId;

        public TelemetryRecorder()
        {
            _spanListener = new ActivityListener
            {
                ShouldListenTo = source => source.Name is "Experimental.Microsoft.Extensions.AI" or TestSourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = RecordSpan,
            };
            ActivitySource.AddActivityListener(_spanListener);

            _meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name == "eshop.assistant.estimated_cost")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _meterListener.SetMeasurementEventCallback<double>(RecordCostIncrease);
            _meterListener.Start();
        }

        public IReadOnlyList<Activity> Spans
        {
            get { lock (_spans) { return [.. _spans]; } }
        }

        public IReadOnlyList<(double Cost, string? Model)> CostIncreases
        {
            get { lock (_costIncreases) { return [.. _costIncreases]; } }
        }

        public async Task RecordAsync(Func<Task> turn)
        {
            using (var test = TestSource.StartActivity("test")!)
            {
                _traceId = test.TraceId;
                await turn();
            }
        }

        public void Dispose()
        {
            _meterListener.Dispose();
            _spanListener.Dispose();
        }

        private void RecordSpan(Activity span)
        {
            if (span.TraceId != _traceId || span.Source.Name == TestSourceName)
            {
                return;
            }

            lock (_spans)
            {
                _spans.Add(span);
            }
        }

        private void RecordCostIncrease(
            Instrument instrument, double cost, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        {
            if (Activity.Current?.TraceId != _traceId)
            {
                return;
            }

            string? model = null;
            foreach (var tag in tags)
            {
                if (tag.Key == "gen_ai.request.model")
                {
                    model = tag.Value as string;
                }
            }

            lock (_costIncreases)
            {
                _costIncreases.Add((cost, model));
            }
        }
    }

    // The model is a boundary we don't own: it asks for the lookup tool once, then answers.
    private sealed class ToolCallingChatClient : IChatClient
    {
        private int _calls;

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
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
