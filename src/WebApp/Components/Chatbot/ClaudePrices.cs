using Microsoft.Extensions.AI;

namespace eShop.WebApp.Chatbot;

/// <summary>
/// Anthropic's list prices, the one table the Shopping Assistant and the evals estimate Claude costs from.
/// </summary>
public static class ClaudePrices
{
    private const decimal TokensPerMillion = 1_000_000m;

    // Cache reads cost a tenth of the input price; cache writes cost 1.25x, the price for the 5-minute cache lifetime.
    private const decimal CacheReadMultiplier = 0.1m;
    private const decimal CacheWriteMultiplier = 1.25m;

    // US dollars per million tokens, Anthropic's list prices as of 2026-11-18 (carried over from the evals' table).
    // List prices change: check them against Anthropic's pricing page when this date gets old. Keyed by model id
    // prefix, so a dated id such as claude-haiku-4-5-20251001 matches its model.
    private static readonly ModelPrice[] PerMillionTokens =
    [
        new("claude-haiku-4-5", Input: 1m, Output: 5m),
        new("claude-sonnet-5-5", Input: 2m, Output: 10m),
    ];

    /// <summary>
    /// The estimated cost in US dollars of <paramref name="tokens"/> on <paramref name="modelId"/>,
    /// or null for a model with no listed price.
    /// </summary>
    public static decimal? EstimateCost(string modelId, ClaudeTokenCounts tokens)
        => FindPrice(modelId)?.Cost(tokens);

    private static ModelPrice? FindPrice(string modelId)
        => PerMillionTokens.FirstOrDefault(p => modelId.StartsWith(p.ModelPrefix, StringComparison.Ordinal));

    private sealed record ModelPrice(string ModelPrefix, decimal Input, decimal Output)
    {
        public decimal Cost(ClaudeTokenCounts tokens)
            => (tokens.Input * Input
                + tokens.Output * Output
                + tokens.CacheRead * Input * CacheReadMultiplier
                + tokens.CacheWrite * Input * CacheWriteMultiplier) / TokensPerMillion;
    }
}

/// <summary>The tokens a Claude call used, by how they are priced. Input excludes cache reads and writes.</summary>
public sealed record ClaudeTokenCounts(long Input = 0, long Output = 0, long CacheRead = 0, long CacheWrite = 0)
{
    private const string AdapterCacheWriteCountName = "CacheCreationInputTokens"; // The Anthropic adapter's additional count.

    /// <summary>The tokens in the Anthropic adapter's <paramref name="usage"/>, or none when it reported none.</summary>
    public static ClaudeTokenCounts FromAdapterUsage(UsageDetails? usage) => FromAdapterTotals(
        input: usage?.InputTokenCount ?? 0,
        output: usage?.OutputTokenCount ?? 0,
        cacheRead: usage?.CachedInputTokenCount ?? 0,
        cacheWrite: usage?.AdditionalCounts?.TryGetValue(AdapterCacheWriteCountName, out var cacheWrites) == true ? cacheWrites : 0);

    /// <summary>The tokens of totals counted the adapter's way, where <paramref name="input"/> includes the cache reads and writes.</summary>
    public static ClaudeTokenCounts FromAdapterTotals(long input, long output, long cacheRead, long cacheWrite)
        => new(Input: input - cacheRead - cacheWrite, Output: output, CacheRead: cacheRead, CacheWrite: cacheWrite);
}
