using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace eShop.Policy.FunctionalTests;

/// <summary>
/// Stands in for the embedding model, a system we don't own: each word of a text adds to one of 384
/// dimensions chosen by a stable hash, so texts that share words are close and ranking is predictable.
/// </summary>
/// <remarks>
/// Its vectors are 768 long with the words in the first 384, like a real model that returns more
/// dimensions than policy-api stores. It records every text it embeds, and its model id can be changed.
/// </remarks>
public sealed partial class HashedBagOfWordsEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private const int WordDimensions = 384;
    private const int ReturnedDimensions = 768;

    private static readonly HashSet<string> StopWords =
        ["a", "an", "and", "are", "can", "do", "for", "from", "have", "how", "i", "in", "is", "it", "my", "not", "of", "on", "or", "the", "to", "we", "what", "with", "you", "your"];

    private readonly ConcurrentQueue<string> _embeddedTexts = new();

    public string ModelId { get; set; } = "hashed-bag-of-words-v1";

    public IReadOnlyCollection<string> EmbeddedTexts => _embeddedTexts;

    public void ClearEmbeddedTexts() => _embeddedTexts.Clear();

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values, EmbeddingGenerationOptions options = null, CancellationToken cancellationToken = default)
    {
        var embeddings = new GeneratedEmbeddings<Embedding<float>>();
        foreach (var value in values)
        {
            _embeddedTexts.Enqueue(value);
            embeddings.Add(new Embedding<float>(Embed(value)) { ModelId = ModelId });
        }

        return Task.FromResult(embeddings);
    }

    public object GetService(Type serviceType, object serviceKey = null) =>
        serviceKey is null && serviceType == typeof(EmbeddingGeneratorMetadata)
            ? new EmbeddingGeneratorMetadata("fake", defaultModelId: ModelId, defaultModelDimensions: ReturnedDimensions)
            : serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
    }

    private static float[] Embed(string text)
    {
        var vector = new float[ReturnedDimensions];
        foreach (Match word in Word().Matches(text.ToLowerInvariant()))
        {
            if (!StopWords.Contains(word.Value))
            {
                vector[Fnv1a(word.Value) % WordDimensions] += 1;
            }
        }

        var length = MathF.Sqrt(vector.Sum(x => x * x));
        return length == 0 ? vector : vector.Select(x => x / length).ToArray();
    }

    private static uint Fnv1a(string word)
    {
        var hash = 2166136261;
        foreach (var c in word)
        {
            hash = (hash ^ c) * 16777619;
        }

        return hash;
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex Word();
}

/// <summary>policy-api with <see cref="HashedBagOfWordsEmbeddingGenerator"/> as its embedding generator.</summary>
public sealed class SemanticPolicyApiFixture : PolicyApiFixture
{
    public HashedBagOfWordsEmbeddingGenerator Embeddings { get; } = new();

    protected override void ConfigureTestServices(IServiceCollection services) =>
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(Embeddings);
}
