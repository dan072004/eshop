using System.Net;

namespace eShop.Policy.FunctionalTests;

public sealed class PolicySearchTests(PolicyApiFixture fixture) : IClassFixture<PolicyApiFixture>
{
    private readonly HttpClient _client = fixture.CreateClient();

    [Fact]
    public async Task KeywordSearchReturnsTheMatchingPolicySectionFirstWithItsCitation()
    {
        var response = await _client.SearchPoliciesAsync("can I return my GPS watch?");

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
        var response = await _client.SearchPoliciesAsync("shipping helmets");

        Assert.Equal("Shipping Destinations › Helmets", response.Results[0].Citation);
    }

    [Fact]
    public async Task AnIntroSectionIsCitedByThePolicyTitleAlone()
    {
        var response = await _client.SearchPoliciesAsync("which rules override the standard policy");

        Assert.Equal("Returns Exceptions", response.Results[0].Citation);
        Assert.Equal("Returns Exceptions", response.Results[0].Section);
    }

    [Fact]
    public async Task AnEditedPolicyIsIndexedAgainAndChangesWhatSearchReturns()
    {
        fixture.WritePolicy("gift-cards", Policy("Gift Cards", "Expiry", "Gift cards never expire."));
        await fixture.IngestAsync();
        Assert.Equal("Gift Cards › Expiry", (await _client.SearchPoliciesAsync("gift cards expire")).Results[0].Citation);

        fixture.WritePolicy("gift-cards", Policy("Gift Cards", "Balance", "Check a gift card balance on the account page."));
        await fixture.IngestAsync();

        Assert.Empty((await _client.SearchPoliciesAsync("expire")).Results);
        var result = (await _client.SearchPoliciesAsync("gift card balance")).Results[0];
        Assert.Equal("Gift Cards › Balance", result.Citation);
        Assert.Equal("Check a gift card balance on the account page.", result.Text);
    }

    [Fact]
    public async Task ADeletedPolicyFilesSectionsDisappearFromResults()
    {
        fixture.WritePolicy("price-match", Policy("Price Match", "Competitors", "We match a competitor's advertised price."));
        await fixture.IngestAsync();
        Assert.Equal("Price Match › Competitors", (await _client.SearchPoliciesAsync("competitor price")).Results[0].Citation);

        fixture.DeletePolicy("price-match");
        await fixture.IngestAsync();

        Assert.Empty((await _client.SearchPoliciesAsync("competitor price")).Results);
        Assert.Equal("Returns Exceptions › Helmets", (await _client.SearchPoliciesAsync("helmets worn outside")).Results[0].Citation);
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

        Assert.Equal(5, (await _client.SearchPoliciesAsync("kayak paddles")).Results.Count);
        Assert.Equal(10, (await _client.SearchPoliciesAsync("kayak paddles", top: 50)).Results.Count);
        Assert.Equal(3, (await _client.SearchPoliciesAsync("kayak paddles", top: 3)).Results.Count);
    }

    [Fact]
    public async Task AQuestionNoPolicyMatchesReturnsAnEmptyResultList()
    {
        var body = await _client.SearchPoliciesAsync("quantum teleportation");

        Assert.Equal("keyword", body.Mode);
        Assert.Empty(body.Results);
    }

    private static string Policy(string title, string heading, string body) =>
        $"---\ntitle: {title}\n---\n\n# {title}\n\n## {heading}\n{body}\n";
}
