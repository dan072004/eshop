using Microsoft.Extensions.AI;
using Pgvector;

namespace eShop.Policy.API.Services;

/// <summary>
/// The embedding generator chosen by the AppHost's <c>EmbeddingProvider</c>, if any, with its vectors
/// cut to the 384 dimensions policy-api stores (as Catalog.API does).
/// </summary>
public sealed class PolicyEmbeddings(IEmbeddingGenerator<string, Embedding<float>>? generator = null)
{
    public const int Dimensions = 384;

    /// <summary>The model id stored when no embedding provider is configured.</summary>
    public const string NoEmbeddingModel = "none";

    // Ollama and OpenAI accept many inputs per request; this keeps each request a modest size.
    private const int BatchSize = 32;

    public bool IsEnabled => generator is not null;

    /// <summary>
    /// The generator's model id (e.g. <c>all-minilm</c> or <c>text-embedding-3-small</c>), or <see cref="NoEmbeddingModel"/>.
    /// A Policy indexed with another model id is indexed again.
    /// </summary>
    public string ModelId
    {
        get
        {
            if (generator is null)
            {
                return NoEmbeddingModel;
            }

            var metadata = generator.GetService<EmbeddingGeneratorMetadata>();
            return metadata?.DefaultModelId ?? metadata?.ProviderName ?? generator.GetType().Name;
        }
    }

    public async Task<IReadOnlyList<Vector>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        var generatorOrThrow = generator ?? throw new InvalidOperationException("No embedding provider is configured.");
        var vectors = new List<Vector>(texts.Count);
        foreach (var batch in texts.Chunk(BatchSize))
        {
            var embeddings = await generatorOrThrow.GenerateAsync(batch, cancellationToken: cancellationToken);
            vectors.AddRange(embeddings.Select(embedding => new Vector(embedding.Vector[..Dimensions])));
        }

        return vectors;
    }

    public async Task<Vector> EmbedAsync(string text, CancellationToken cancellationToken) =>
        (await EmbedAsync([text], cancellationToken))[0];
}
