using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace eShop.Policy.FunctionalTests;

public sealed class PolicySearchTests(PolicyApiFixture fixture) : IClassFixture<PolicyApiFixture>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client = fixture.CreateClient();

    [Fact]
    public async Task KeywordSearchReturnsTheMatchingPolicySectionFirstWithItsCitation()
    {
        var response = await SearchAsync("can I return my GPS watch?");

        Assert.Equal("keyword", response.Mode);
        var first = response.Results[0];
        Assert.Equal("Returns Exceptions › GPS watches, navigators and bike computers", first.Citation);
        Assert.Equal("returns-exceptions", first.PolicySlug);
        Assert.Equal("Returns Exceptions", first.PolicyTitle);
        Assert.Equal("GPS watches, navigators and bike computers", first.Section);
        Assert.Equal("gps-watches-navigators-and-bike-computers", first.Anchor);
        Assert.Equal("Navigation electronics have a 30-day return window from the order date, not 60 days.", first.Text);
        Assert.True(first.Score > 0);
    }

    [Fact]
    public async Task SectionsWithTheSameHeadingAreToldApartByTheirPolicyTitle()
    {
        var response = await SearchAsync("shipping helmets");

        Assert.Equal("Shipping Destinations › Helmets", response.Results[0].Citation);
    }

    [Fact]
    public async Task AnIntroSectionIsCitedByThePolicyTitleAlone()
    {
        var response = await SearchAsync("which rules override the standard policy");

        Assert.Equal("Returns Exceptions", response.Results[0].Citation);
        Assert.Equal("Returns Exceptions", response.Results[0].Section);
    }

    [Fact]
    public async Task AnEditedPolicyIsIndexedAgainAndChangesWhatSearchReturns()
    {
        fixture.WritePolicy("gift-cards", Policy("Gift Cards", "Expiry", "Gift cards never expire."));
        await fixture.IngestAsync();
        Assert.Equal("Gift Cards › Expiry", (await SearchAsync("gift cards expire")).Results[0].Citation);

        fixture.WritePolicy("gift-cards", Policy("Gift Cards", "Balance", "Check a gift card balance on the account page."));
        await fixture.IngestAsync();

        Assert.Empty((await SearchAsync("expire")).Results);
        var result = (await SearchAsync("gift card balance")).Results[0];
        Assert.Equal("Gift Cards › Balance", result.Citation);
        Assert.Equal("Check a gift card balance on the account page.", result.Text);
    }

    [Fact]
    public async Task ADeletedPolicyFilesSectionsDisappearFromResults()
    {
        fixture.WritePolicy("price-match", Policy("Price Match", "Competitors", "We match a competitor's advertised price."));
        await fixture.IngestAsync();
        Assert.Equal("Price Match › Competitors", (await SearchAsync("competitor price")).Results[0].Citation);

        fixture.DeletePolicy("price-match");
        await fixture.IngestAsync();

        Assert.Empty((await SearchAsync("competitor price")).Results);
        Assert.Equal("Returns Exceptions › Helmets", (await SearchAsync("helmets worn outside")).Results[0].Citation);
    }

    [Theory]
    [InlineData("/api/policies/search")]
    [InlineData("/api/policies/search?q=")]
    [InlineData("/api/policies/search?q=%20%20")]
    [InlineData("/api/policies/search?q=helmets&top=0")]
    [InlineData("/api/policies/search?q=helmets&top=-3")]
    public async Task AMissingOrEmptyQuestionOrATopBelowOneIsABadRequest(string url)
    {
        var response = await _client.GetAsync(url, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TopDefaultsToFiveAndIsCappedAtTen()
    {
        fixture.WritePolicy("many-sections", string.Concat(
            "---\ntitle: Many Sections\n---\n\n# Many Sections\n",
            string.Concat(Enumerable.Range(1, 12).Select(i => $"\n## Kayak rule {i}\nKayak paddles rule number {i}.\n"))));
        await fixture.IngestAsync();

        Assert.Equal(5, (await SearchAsync("kayak paddles")).Results.Count);
        Assert.Equal(10, (await SearchAsync("kayak paddles", top: 50)).Results.Count);
        Assert.Equal(3, (await SearchAsync("kayak paddles", top: 3)).Results.Count);
    }

    [Fact]
    public async Task AQuestionNoPolicyMatchesReturnsAnEmptyResultList()
    {
        var response = await _client.GetAsync("/api/policies/search?q=quantum%20teleportation", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SearchResponse>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal("keyword", body.Mode);
        Assert.Empty(body.Results);
    }

    private async Task<SearchResponse> SearchAsync(string question, int? top = null)
    {
        var url = $"/api/policies/search?q={Uri.EscapeDataString(question)}" + (top is null ? "" : $"&top={top}");
        var response = await _client.GetAsync(url, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SearchResponse>(JsonOptions, TestContext.Current.CancellationToken);
    }

    private static string Policy(string title, string heading, string body) =>
        $"---\ntitle: {title}\n---\n\n# {title}\n\n## {heading}\n{body}\n";

    private sealed record SearchResponse(string Mode, List<SearchResult> Results);

    private sealed record SearchResult(
        string Citation, string PolicySlug, string PolicyTitle, string Section, string Anchor, string Text, double Score);
}
