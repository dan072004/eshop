using System.Threading;
using eShop.Catalog.API;
using eShop.Catalog.API.Infrastructure;
using eShop.Catalog.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace eShop.Catalog.FunctionalTests;

public sealed class CatalogSeedTests(CatalogApiFixture fixture) : IClassFixture<CatalogApiFixture>
{
    [Fact]
    public async Task TurningOnEmbeddingsLaterFillsInTheCatalogSeededWithoutThem()
    {
        // The fixture's app has no embedding generator, so startup seeded the catalog without embeddings.
        Assert.Equal(0, await CountItemsAsync(withEmbedding: true));
        var itemCount = await CountItemsAsync(withEmbedding: false);

        var embeddingGenerator = new CountingEmbeddingGenerator();
        await SeedWithEmbeddingsAsync(embeddingGenerator);

        Assert.Equal(itemCount, await CountItemsAsync(withEmbedding: true));
        Assert.Equal(itemCount, embeddingGenerator.InputCount);

        // Once every item has one, a restart generates nothing.
        await SeedWithEmbeddingsAsync(embeddingGenerator);
        Assert.Equal(itemCount, embeddingGenerator.InputCount);
    }

    private async Task SeedWithEmbeddingsAsync(IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
    {
        using var scope = fixture.Services.CreateScope();
        var services = scope.ServiceProvider;
        var environment = services.GetRequiredService<IWebHostEnvironment>();
        var catalogAI = new CatalogAI(environment, services.GetRequiredService<ILogger<CatalogAI>>(), embeddingGenerator);
        var seeder = new CatalogContextSeed(
            environment,
            services.GetRequiredService<IOptions<CatalogOptions>>(),
            catalogAI,
            services.GetRequiredService<ILogger<CatalogContextSeed>>());

        await seeder.SeedAsync(services.GetRequiredService<CatalogContext>());
    }

    private async Task<int> CountItemsAsync(bool withEmbedding)
    {
        using var scope = fixture.Services.CreateScope();
        var items = scope.ServiceProvider.GetRequiredService<CatalogContext>().CatalogItems;
        return withEmbedding
            ? await items.CountAsync(item => item.Embedding != null, TestContext.Current.CancellationToken)
            : await items.CountAsync(TestContext.Current.CancellationToken);
    }

    // The embedding model is a system we don't own; this stands in for it at that boundary.
    private sealed class CountingEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        public int InputCount { get; private set; }

        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values, EmbeddingGenerationOptions options = null, CancellationToken cancellationToken = default)
        {
            var inputs = values.ToList();
            InputCount += inputs.Count;
            return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(
                inputs.Select(_ => new Embedding<float>(Enumerable.Repeat(0.1f, 384).ToArray()))));
        }

        public object GetService(Type serviceType, object serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
