using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using Microsoft.Extensions.AI.Evaluation.Reporting.Storage;
using Microsoft.Extensions.Caching.Distributed;

namespace ShoppingAssistant.Evals.Running;

/// <summary>
/// Reporting's response cache, in the results store, for both the chat model's turns (<see cref="CachedModel"/>) and
/// the judge's calls (<see cref="EvalReporting"/>). Entries expire after 14 days, so answers are sampled again
/// regularly even when nothing changes. <c>ESHOP_EVALS_FRESH=1</c> ignores cached responses and replaces them with
/// the fresh ones.
/// </summary>
public static class ResponseCache
{
    public const string FreshVariable = "ESHOP_EVALS_FRESH";

    public static TimeSpan TimeToLive { get; } = TimeSpan.FromDays(14);

    public static bool IsFresh => Environment.GetEnvironmentVariable(FreshVariable) == "1";

    /// <summary>The cache under <paramref name="storePath"/>; with <paramref name="fresh"/>, every read misses but writes still land.</summary>
    public static IEvaluationResponseCacheProvider Provider(string storePath, bool fresh)
    {
        var disk = new DiskBasedResponseCacheProvider(storePath, TimeToLive);
        return fresh ? new WriteOnlyProvider(disk) : disk;
    }

    private sealed class WriteOnlyProvider(IEvaluationResponseCacheProvider inner) : IEvaluationResponseCacheProvider
    {
        public async ValueTask<IDistributedCache> GetCacheAsync(string scenarioName, string iterationName, CancellationToken cancellationToken = default) =>
            new WriteOnlyCache(await inner.GetCacheAsync(scenarioName, iterationName, cancellationToken));

        public ValueTask ResetAsync(CancellationToken cancellationToken = default) => inner.ResetAsync(cancellationToken);

        public ValueTask DeleteExpiredCacheEntriesAsync(CancellationToken cancellationToken = default) =>
            inner.DeleteExpiredCacheEntriesAsync(cancellationToken);
    }

    private sealed class WriteOnlyCache(IDistributedCache inner) : IDistributedCache
    {
        public byte[]? Get(string key) => null;

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult<byte[]?>(null);

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => inner.Set(key, value, options);

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) =>
            inner.SetAsync(key, value, options, token);

        public void Refresh(string key) => inner.Refresh(key);

        public Task RefreshAsync(string key, CancellationToken token = default) => inner.RefreshAsync(key, token);

        public void Remove(string key) => inner.Remove(key);

        public Task RemoveAsync(string key, CancellationToken token = default) => inner.RemoveAsync(key, token);
    }
}

/// <summary>
/// The chat model as the eval calls it: Reporting's caching client around the model, one cache per case and iteration,
/// keyed by Reporting's key plus <see cref="CachingKeys"/>. It goes below WebApp's function invocation, so each model
/// turn is cached on its own.
/// </summary>
public sealed class CachedModel(IChatClient model, IEvaluationResponseCacheProvider cache)
{
    /// <summary>A cached client for one sample, which counts the calls that reach the model.</summary>
    public async Task<CachedSample> OpenAsync(
        string caseId, string iterationName, IEnumerable<string> cachingKeys, CancellationToken cancellationToken)
    {
        var calls = new ModelCallCounter(model);

        // Only the run's chat client is used: the run is never evaluated, so it writes no result to the store.
        var answers = new ReportingConfiguration([], NoResults.Instance, new ChatConfiguration(calls), cache);
        var run = await answers.CreateScenarioRunAsync(caseId, iterationName, cachingKeys, cancellationToken: cancellationToken);
        return new CachedSample(run.ChatConfiguration!.ChatClient, () => calls.Calls);
    }

    private sealed class ModelCallCounter(IChatClient inner) : DelegatingChatClient(inner)
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public override Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);
            return base.GetResponseAsync(messages, options, cancellationToken);
        }

        public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);
            return base.GetStreamingResponseAsync(messages, options, cancellationToken);
        }

        // The model's lifetime belongs to whoever passed it in.
        protected override void Dispose(bool disposing) { }
    }

    private sealed class NoResults : IEvaluationResultStore
    {
        public static NoResults Instance { get; } = new();

        public IAsyncEnumerable<ScenarioRunResult> ReadResultsAsync(
            string? executionName = null, string? scenarioName = null, string? iterationName = null, CancellationToken cancellationToken = default) =>
            AsyncEnumerable.Empty<ScenarioRunResult>();

        public ValueTask WriteResultsAsync(IEnumerable<ScenarioRunResult> results, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The chat model's cache run is never evaluated, so it has no result to write.");

        public ValueTask DeleteResultsAsync(
            string? executionName = null, string? scenarioName = null, string? iterationName = null, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public IAsyncEnumerable<string> GetLatestExecutionNamesAsync(int? count = null, CancellationToken cancellationToken = default) =>
            AsyncEnumerable.Empty<string>();

        public IAsyncEnumerable<string> GetScenarioNamesAsync(string executionName, CancellationToken cancellationToken = default) =>
            AsyncEnumerable.Empty<string>();

        public IAsyncEnumerable<string> GetIterationNamesAsync(string executionName, string scenarioName, CancellationToken cancellationToken = default) =>
            AsyncEnumerable.Empty<string>();
    }
}

/// <summary>One sample's cached chat client, and how many of its calls reached the model so far.</summary>
public sealed class CachedSample(IChatClient chatClient, Func<int> modelCalls)
{
    public IChatClient ChatClient { get; } = chatClient;

    public int ModelCalls => modelCalls();
}
