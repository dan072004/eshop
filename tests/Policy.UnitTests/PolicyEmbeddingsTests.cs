using eShop.Policy.API.Services;
using Microsoft.Extensions.AI;

namespace eShop.Policy.UnitTests;

[TestClass]
public class PolicyEmbeddingsTests
{
    [TestMethod]
    public void ModelIdIsTheGeneratorsModelId()
    {
        var embeddings = new PolicyEmbeddings(new MetadataOnlyGenerator(new("ollama", null, "all-minilm")));

        Assert.AreEqual("all-minilm", embeddings.ModelId);
    }

    [TestMethod]
    public void ModelIdIsNoneWithoutAGenerator()
    {
        Assert.AreEqual("none", new PolicyEmbeddings().ModelId);
    }

    [TestMethod]
    public void ModelIdFailsWhenTheGeneratorDoesNotNameItsModel()
    {
        // A provider name alone would give two models from one provider the same id, so a model switch
        // would not index the Policies again.
        var embeddings = new PolicyEmbeddings(new MetadataOnlyGenerator(new("ollama", null, null)));

        var error = Assert.ThrowsExactly<InvalidOperationException>(() => embeddings.ModelId);
        Assert.Contains("model id", error.Message);
    }

    // Stands in for a generator from a package we don't own; only its metadata is used here.
    private sealed class MetadataOnlyGenerator(EmbeddingGeneratorMetadata metadata) : IEmbeddingGenerator<string, Embedding<float>>
    {
        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceType == typeof(EmbeddingGeneratorMetadata) ? metadata : null;

        public void Dispose()
        {
        }
    }
}
