using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace eShop.Policy.FunctionalTests;

/// <summary>policy-api with <see cref="HashedBagOfWordsEmbeddingGenerator"/> as its embedding generator.</summary>
public sealed class SemanticPolicyApiFixture : PolicyApiFixture
{
    public HashedBagOfWordsEmbeddingGenerator Embeddings { get; } = new();

    protected override void ConfigureTestServices(IServiceCollection services) =>
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(Embeddings);
}
