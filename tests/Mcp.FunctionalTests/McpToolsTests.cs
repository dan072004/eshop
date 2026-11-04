using System.Text.Json;
using eShop.Assistant.Tools;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace eShop.Mcp.FunctionalTests;

/// <summary>Drives eShop.Mcp through the real MCP SDK client over Streamable HTTP, as Claude Code and VS Code do.</summary>
public sealed class McpToolsTests(McpServerFixture fixture) : IClassFixture<McpServerFixture>
{
    // What Catalog.API v2 returns from items/withsemanticrelevance (web JSON casing).
    private const string CatalogPage = """
        {
          "pageIndex": 0,
          "pageSize": 8,
          "count": 1,
          "data": [
            {
              "id": 42,
              "name": "Trailblazer Tent",
              "description": "A two-person tent for alpine trekking.",
              "price": 199.5,
              "pictureFileName": "42.webp",
              "catalogTypeId": 3,
              "catalogType": { "id": 3, "type": "Tents" },
              "catalogBrandId": 7,
              "catalogBrand": { "id": 7, "brand": "Alpine Co" }
            }
          ]
        }
        """;

    [Fact]
    public async Task ToolsListOffersExactlyTheFourContractToolsWithTheirParameters()
    {
        await using var client = await ConnectAsBuyerAsync("alice");

        var tools = await client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        // The descriptions are the shared library's, passed through unchanged.
        using var scope = fixture.Services.CreateScope();
        var libraryDescriptions = scope.ServiceProvider.GetRequiredService<AssistantTools>().All
            .ToDictionary(f => f.Name, f => f.Description);

        // The tool contract (spec): names and parameter names, nothing added by eShop.Mcp.
        var expected = new Dictionary<string, string[]>
        {
            ["search_policies"] = ["question"],
            ["search_catalog"] = ["productDescription"],
            ["list_my_orders"] = [],
            ["get_order"] = ["orderNumber"],
        };
        Assert.Equal(expected.Keys.Order(), tools.Select(t => t.Name).Order());
        foreach (var tool in tools)
        {
            var parameters = tool.JsonSchema.TryGetProperty("properties", out var properties)
                ? properties.EnumerateObject().Select(p => p.Name).ToArray()
                : [];
            var required = tool.JsonSchema.TryGetProperty("required", out var requiredNames)
                ? requiredNames.EnumerateArray().Select(n => n.GetString()!).ToArray()
                : [];
            Assert.Equal(expected[tool.Name], parameters);
            Assert.Equal(expected[tool.Name], required);
            Assert.Equal(libraryDescriptions[tool.Name], tool.Description);
            Assert.Null(tool.ProtocolTool.OutputSchema);
        }
    }

    [Fact]
    public async Task SearchCatalogReachesCatalogWithoutTheBuyersTokenAndLeavesPictureUrlOut()
    {
        fixture.Catalog.Reset();
        fixture.Catalog.Respond = _ => RecordingHandler.Json(CatalogPage);
        await using var client = await ConnectAsBuyerAsync("alice");

        var result = await client.CallToolAsync(
            "search_catalog",
            new Dictionary<string, object?> { ["productDescription"] = "tent for trekking" },
            cancellationToken: TestContext.Current.CancellationToken);

        var request = Assert.Single(fixture.Catalog.Requests);
        Assert.Equal("catalog.test", request.Uri.Host);
        Assert.Equal("/api/catalog/items/withsemanticrelevance", request.Uri.AbsolutePath);
        Assert.Contains("text=tent%20for%20trekking", request.Uri.Query);
        Assert.Null(request.Authorization);

        Assert.NotEqual(true, result.IsError);
        Assert.Null(result.StructuredContent);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        using var json = JsonDocument.Parse(text);
        var item = Assert.Single(json.RootElement.GetProperty("data").EnumerateArray());
        Assert.Equal(42, item.GetProperty("id").GetInt32());
        Assert.Equal("Trailblazer Tent", item.GetProperty("name").GetString());
        Assert.False(item.TryGetProperty("pictureUrl", out _));
    }

    // Interim until ticket 07: the Buyer's token names only eShop.Mcp's URL, so the order tools have no token Ordering
    // may receive and ask the Buyer to sign in, without forwarding the caller's token (no passthrough, ADR 0005).
    [Fact]
    public async Task OrderToolsAskTheBuyerToSignInAndNothingReachesOrdering()
    {
        fixture.Ordering.Reset();
        fixture.Ordering.Respond = _ => RecordingHandler.Json("[]");
        await using var client = await ConnectAsBuyerAsync("alice");

        var listed = await client.CallToolAsync("list_my_orders", cancellationToken: TestContext.Current.CancellationToken);
        var got = await client.CallToolAsync(
            "get_order",
            new Dictionary<string, object?> { ["orderNumber"] = 17 },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(fixture.Ordering.Requests);
        foreach (var result in new[] { listed, got })
        {
            var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
            using var json = JsonDocument.Parse(text);
            Assert.Equal("sign_in_required", json.RootElement.GetProperty("error").GetString());
        }
    }

    [Fact]
    public async Task SearchPoliciesReachesPolicyApiWithoutTheBuyersToken()
    {
        fixture.Policy.Reset();
        fixture.Policy.Respond = _ => RecordingHandler.Json("""{ "mode": "keyword", "results": [] }""");
        await using var client = await ConnectAsBuyerAsync("alice");

        var result = await client.CallToolAsync(
            "search_policies",
            new Dictionary<string, object?> { ["question"] = "Can I return a tent?" },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotEqual(true, result.IsError);
        var request = Assert.Single(fixture.Policy.Requests);
        Assert.Equal("policy.test", request.Uri.Host);
        Assert.Equal("/api/policies/search", request.Uri.AbsolutePath);
        Assert.Null(request.Authorization);
    }

    // A client whose Authorization header comes from the handler, so a test can switch Buyers between calls on one
    // client. Stateless sessions allow it: each call is authorised by the token on that request.
    private async Task<McpClient> ConnectAsync(BearerHandler bearer)
    {
        var httpClient = fixture.CreateDefaultClient(bearer);
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(httpClient.BaseAddress!, "/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp,
            },
            httpClient,
            ownsHttpClient: true);

        return await McpClient.CreateAsync(transport, cancellationToken: TestContext.Current.CancellationToken);
    }

    private sealed class BearerHandler : DelegatingHandler
    {
        public required string Token { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Authorization = new("Bearer", Token);
            return base.SendAsync(request, cancellationToken);
        }
    }

    private Task<McpClient> ConnectAsBuyerAsync(string subject) =>
        ConnectAsync(new BearerHandler { Token = fixture.MintToken(subject) });
}
