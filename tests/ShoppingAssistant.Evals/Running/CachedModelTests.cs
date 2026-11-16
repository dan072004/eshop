using Microsoft.Extensions.AI;

namespace ShoppingAssistant.Evals.Running;

/// <summary>
/// The chat model behind Reporting's disk cache in a temporary results store, with only the model faked (a boundary we
/// don't own): what each sample answers, and how many times it had to call the model for it.
/// </summary>
public sealed class CachedModelTests : IDisposable
{
    private readonly string _store = Path.Combine(Path.GetTempPath(), "evals-cache-" + Guid.NewGuid().ToString("N"));
    private readonly NumberedAnswers _model = new();

    public void Dispose()
    {
        if (Directory.Exists(_store))
        {
            Directory.Delete(_store, recursive: true);
        }
    }

    [Fact]
    public async Task AnUnchangedSampleIsReplayedFromTheCacheWithoutCallingTheModel()
    {
        var first = await AskAsync(fresh: false);
        var second = await AskAsync(fresh: false);

        Assert.Equal([("answer 1", 1), ("answer 1", 0)], [first, second]);
    }

    [Fact]
    public async Task AChangedCachingKeyCallsTheModelAgain()
    {
        var first = await AskAsync(fresh: false, cachingKey: "case:a");
        var second = await AskAsync(fresh: false, cachingKey: "case:b");

        Assert.Equal([("answer 1", 1), ("answer 2", 1)], [first, second]);
    }

    [Fact]
    public async Task AFreshSampleCallsTheModelEvenWithAWarmCacheAndReplacesTheCachedAnswer()
    {
        var cached = await AskAsync(fresh: false);
        var fresh = await AskAsync(fresh: true);
        var afterwards = await AskAsync(fresh: false);

        Assert.Equal([("answer 1", 1), ("answer 2", 1), ("answer 2", 0)], [cached, fresh, afterwards]);
    }

    [Fact]
    public async Task OnlyTheTurnsThatReachTheModelReportTheirTokens()
    {
        await TokensOfAsync("what's the return window?");

        // The first turn is replayed, so only the second, new turn is billed.
        var tokens = await TokensOfAsync("what's the return window?", "and for the jacket?");

        Assert.Equal([("claude-haiku-4-5", 100L, 20L)], tokens);
    }

    private async Task<(string Answer, int ModelCalls)> AskAsync(bool fresh, string cachingKey = "case:a")
    {
        var cachedModel = new CachedModel(_model, ResponseCache.Provider(_store, fresh));
        var sample = await cachedModel.OpenAsync("returns-gps-watch-last-order", "1", [cachingKey], TestContext.Current.CancellationToken);

        var response = await sample.ChatClient.GetResponseAsync("what's the return window for my last order?", cancellationToken: TestContext.Current.CancellationToken);
        return (response.Text, sample.ModelCalls);
    }

    private async Task<IReadOnlyList<(string Model, long InputTokens, long OutputTokens)>> TokensOfAsync(params string[] turns)
    {
        var cachedModel = new CachedModel(_model, ResponseCache.Provider(_store, fresh: false));
        var sample = await cachedModel.OpenAsync("returns-gps-watch-last-order", "1", ["case:a"], TestContext.Current.CancellationToken);

        List<ChatMessage> conversation = [];
        foreach (var turn in turns)
        {
            conversation.Add(new(ChatRole.User, turn));
            var response = await sample.ChatClient.GetResponseAsync(conversation, cancellationToken: TestContext.Current.CancellationToken);
            conversation.AddRange(response.Messages);
        }
        return [.. sample.Usage.Select(u => (u.Model, u.InputTokens, u.OutputTokens))];
    }

    // Answers "answer 1", "answer 2", ... in the order it is called, reporting 100 input and 20 output tokens each.
    private sealed class NumberedAnswers : IChatClient
    {
        private int _calls;

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, $"answer {Interlocked.Increment(ref _calls)}"))
            {
                ModelId = "claude-haiku-4-5",
                Usage = new UsageDetails { InputTokenCount = 100, OutputTokenCount = 20 },
            });

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
