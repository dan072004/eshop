using System.Text.Json;
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
    public async Task ToolsListOffersSearchCatalogWithTheContractParameter()
    {
        await using var client = await ConnectAsBuyerAsync("alice");

        var tools = await client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        var tool = Assert.Single(tools);
        Assert.Equal("search_catalog", tool.Name);
        Assert.Equal("Searches the AdventureWorks catalog for a provided product description", tool.Description);
        var parameter = Assert.Single(tool.JsonSchema.GetProperty("properties").EnumerateObject());
        Assert.Equal("productDescription", parameter.Name);
        Assert.Equal("productDescription", Assert.Single(tool.JsonSchema.GetProperty("required").EnumerateArray()).GetString());
        Assert.Null(tool.ProtocolTool.OutputSchema);
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

    private async Task<McpClient> ConnectAsBuyerAsync(string subject)
    {
        var token = fixture.MintToken(subject, audiences: ["eshop-mcp", "orders"]);
        var httpClient = fixture.CreateClient();
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(httpClient.BaseAddress!, "/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp,
                AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" },
            },
            httpClient,
            ownsHttpClient: true);

        return await McpClient.CreateAsync(transport, cancellationToken: TestContext.Current.CancellationToken);
    }
}
