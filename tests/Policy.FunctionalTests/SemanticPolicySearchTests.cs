namespace eShop.Policy.FunctionalTests;

/// <summary>Ingestion and search with an embedding generator (a deterministic fake of the model) against real Postgres.</summary>
public sealed class SemanticPolicySearchTests(SemanticPolicyApiFixture fixture) : IClassFixture<SemanticPolicyApiFixture>
{
    private readonly HttpClient _client = fixture.CreateClient();

    [Fact]
    public async Task SemanticSearchReturnsTheClosestPolicySectionFirst()
    {
        var response = await _client.SearchPoliciesAsync("can I return my GPS watch?");

        Assert.Equal("semantic", response.Mode);
        var first = response.Results[0];
        Assert.Equal("Returns Exceptions › GPS watches, navigators and bike computers", first.Citation);
        Assert.Equal("returns-exceptions", first.PolicySlug);
        Assert.Equal("gps-watches-navigators-and-bike-computers", first.Anchor);
        Assert.Equal("Navigation electronics have a 30-day return window from the order date, not 60 days.", first.Text);
        Assert.True(first.Score > response.Results[1].Score);
    }

    [Fact]
    public async Task OnlyANewOrEditedPolicyIsEmbeddedWhenIngestionRunsAgain()
    {
        fixture.Embeddings.ClearEmbeddedTexts();
        await fixture.IngestAsync();
        Assert.Empty(fixture.Embeddings.EmbeddedTexts);

        fixture.WritePolicy("gift-cards", "---\ntitle: Gift Cards\n---\n\n# Gift Cards\n\n## Expiry\nGift cards never expire.\n");
        await fixture.IngestAsync();

        Assert.Equal(["Gift Cards › Expiry\n\nGift cards never expire."], fixture.Embeddings.EmbeddedTexts);
        fixture.Embeddings.ClearEmbeddedTexts();
        await fixture.IngestAsync();
        Assert.Empty(fixture.Embeddings.EmbeddedTexts);

        var result = (await _client.SearchPoliciesAsync("when do gift cards expire?")).Results[0];
        Assert.Equal("Gift Cards › Expiry", result.Citation);
    }

    [Fact]
    public async Task ChangingTheEmbeddingModelIndexesEveryPolicyAgain()
    {
        fixture.Embeddings.ModelId = "hashed-bag-of-words-v2";
        fixture.Embeddings.ClearEmbeddedTexts();

        await fixture.IngestAsync();

        Assert.Contains("Returns Exceptions › Helmets\n\nBike helmets cannot be returned once worn outside, or after any impact.", fixture.Embeddings.EmbeddedTexts);
        Assert.Contains("Returns Exceptions › GPS watches, navigators and bike computers\n\nNavigation electronics have a 30-day return window from the order date, not 60 days.", fixture.Embeddings.EmbeddedTexts);
        Assert.Contains("Shipping Destinations › Canada\n\nWe ship to every Canadian province. Duties and taxes are collected at checkout.", fixture.Embeddings.EmbeddedTexts);
        Assert.Contains("Shipping Destinations › Helmets\n\nHelmets ship to all destinations in their original box.", fixture.Embeddings.EmbeddedTexts);

        fixture.Embeddings.ClearEmbeddedTexts();
        await fixture.IngestAsync();
        Assert.Empty(fixture.Embeddings.EmbeddedTexts);

        Assert.Equal("Shipping Destinations › Canada", (await _client.SearchPoliciesAsync("shipping to Canada")).Results[0].Citation);
    }
}
