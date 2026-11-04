using System.Net;
using System.Net.Http.Json;
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

    // What Ordering.API v1 returns from orders/{orderId}. No items, so get_order makes no Catalog lookups.
    private const string OrderWithoutItems = """
        { "orderNumber": 17, "date": "2026-10-01T09:30:00Z", "status": "Paid", "orderItems": [] }
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
    public async Task SearchCatalogReachesCatalogWithoutTheBuyersTokenOrAnExchangeAndLeavesPictureUrlOut()
    {
        ResetBackends();
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
        Assert.Empty(fixture.Identity.Requests);

        Assert.NotEqual(true, result.IsError);
        Assert.Null(result.StructuredContent);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        using var json = JsonDocument.Parse(text);
        var item = Assert.Single(json.RootElement.GetProperty("data").EnumerateArray());
        Assert.Equal(42, item.GetProperty("id").GetInt32());
        Assert.Equal("Trailblazer Tent", item.GetProperty("name").GetString());
        Assert.False(item.TryGetProperty("pictureUrl", out _));
    }

    // No passthrough (ADR 0005): Ordering only ever sees the token Identity.API issued for it in exchange.
    [Fact]
    public async Task GetOrderExchangesTheBuyersTokenAndReachesOrderingWithOnlyTheExchangedToken()
    {
        ResetBackends();
        fixture.Ordering.Respond = _ => RecordingHandler.Json(OrderWithoutItems);
        var aliceToken = fixture.MintToken("alice");
        await using var client = await ConnectAsync(new BearerHandler { Token = aliceToken });

        var result = await client.CallToolAsync(
            "get_order",
            new Dictionary<string, object?> { ["orderNumber"] = 17 },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotEqual(true, result.IsError);
        var exchange = Assert.Single(fixture.Identity.Requests);
        Assert.Equal((HttpMethod.Post, "http://identity.test/connect/token"), (exchange.Method, exchange.Uri.ToString()));
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:token-exchange",
                ["subject_token"] = aliceToken,
                ["subject_token_type"] = "urn:ietf:params:oauth:token-type:access_token",
                ["scope"] = "orders",
                ["client_id"] = "eshop-mcp-server",
                ["client_secret"] = McpServerFixture.EshopMcpServerSecret,
            },
            exchange.Form);
        Assert.Null(exchange.Authorization);
        var request = Assert.Single(fixture.Ordering.Requests);
        Assert.Equal("http://ordering.test/api/orders/17", request.Uri.GetLeftPart(UriPartial.Path));
        Assert.Equal($"Bearer {McpServerFixture.ExchangedToken(aliceToken)}", request.Authorization);
    }

    [Fact]
    public async Task CallsWithTwoBuyersTokensOnOneClientEachReachOrderingWithTheirOwnExchangedToken()
    {
        ResetBackends();
        fixture.Ordering.Respond = _ => RecordingHandler.Json("[]");
        var aliceToken = fixture.MintToken("alice");
        var bobToken = fixture.MintToken("bob");
        var bearer = new BearerHandler { Token = aliceToken };
        await using var client = await ConnectAsync(bearer);

        var asAlice = await client.CallToolAsync("list_my_orders", cancellationToken: TestContext.Current.CancellationToken);
        bearer.Token = bobToken;
        var asBob = await client.CallToolAsync("list_my_orders", cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotEqual(true, asAlice.IsError);
        Assert.NotEqual(true, asBob.IsError);
        Assert.Equal([aliceToken, bobToken], fixture.Identity.Requests.Select(r => r.Form["subject_token"]));
        Assert.Equal(
            [$"Bearer {McpServerFixture.ExchangedToken(aliceToken)}", $"Bearer {McpServerFixture.ExchangedToken(bobToken)}"],
            fixture.Ordering.Requests.Select(r => r.Authorization));
    }

    // Nothing is cached (ADR 0005): no exchanged token outlives the tool call that asked for it.
    [Fact]
    public async Task TwoOrderToolCallsWithTheSameTokenExchangeItTwice()
    {
        ResetBackends();
        fixture.Ordering.Respond = _ => RecordingHandler.Json("[]");
        var aliceToken = fixture.MintToken("alice");
        await using var client = await ConnectAsync(new BearerHandler { Token = aliceToken });

        await client.CallToolAsync("list_my_orders", cancellationToken: TestContext.Current.CancellationToken);
        await client.CallToolAsync("list_my_orders", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal([aliceToken, aliceToken], fixture.Identity.Requests.Select(r => r.Form["subject_token"]));
        Assert.Equal(2, fixture.Ordering.Requests.Count);
    }

    [Fact]
    public async Task ARefusedExchangeMakesTheOrderToolsAskTheBuyerToSignInAndNothingReachesOrdering()
    {
        ResetBackends();
        fixture.Identity.Respond = _ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = JsonContent.Create(new { error = "invalid_grant" }),
        };
        fixture.Ordering.Respond = _ => RecordingHandler.Json("[]");
        await using var client = await ConnectAsBuyerAsync("alice");

        var errors = await CallOrderToolsAsync(client);

        Assert.Equal(["sign_in_required", "sign_in_required"], errors);
        Assert.Equal(2, fixture.Identity.Requests.Count);
        Assert.Empty(fixture.Ordering.Requests);
    }

    public static TheoryData<string> IdentityFailures => ["unreachable", "5xx"];

    [Theory]
    [MemberData(nameof(IdentityFailures))]
    public async Task AFailingTokenEndpointMakesTheOrderToolsUnavailableAndNothingReachesOrdering(string failure)
    {
        ResetBackends();
        fixture.Identity.Respond = failure == "unreachable"
            ? _ => throw new HttpRequestException("Connection refused")
            : _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        fixture.Ordering.Respond = _ => RecordingHandler.Json("[]");
        await using var client = await ConnectAsBuyerAsync("alice");

        var errors = await CallOrderToolsAsync(client);

        Assert.Equal(["unavailable", "unavailable"], errors);
        Assert.NotEmpty(fixture.Identity.Requests);
        Assert.Empty(fixture.Ordering.Requests);
    }

    [Fact]
    public async Task OrderingsNotFoundComesBackAsTheNotFoundErrorResult()
    {
        ResetBackends();
        fixture.Ordering.Respond = _ => new HttpResponseMessage(HttpStatusCode.NotFound);
        await using var client = await ConnectAsBuyerAsync("alice");

        var result = await client.CallToolAsync(
            "get_order",
            new Dictionary<string, object?> { ["orderNumber"] = 99 },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(fixture.Ordering.Requests);
        Assert.Equal("not_found", ErrorOf(result));
    }

    [Fact]
    public async Task SearchPoliciesReachesPolicyApiWithoutTheBuyersTokenOrAnExchange()
    {
        ResetBackends();
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
        Assert.Empty(fixture.Identity.Requests);
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

    private void ResetBackends()
    {
        fixture.Policy.Reset();
        fixture.Catalog.Reset();
        fixture.Ordering.Reset();
        fixture.Identity.Reset();
        fixture.Identity.Respond = McpServerFixture.GrantExchange;
    }

    // The error code each order tool returns, list_my_orders first.
    private static async Task<string[]> CallOrderToolsAsync(McpClient client)
    {
        var listed = await client.CallToolAsync("list_my_orders", cancellationToken: TestContext.Current.CancellationToken);
        var got = await client.CallToolAsync(
            "get_order",
            new Dictionary<string, object?> { ["orderNumber"] = 17 },
            cancellationToken: TestContext.Current.CancellationToken);
        return [ErrorOf(listed), ErrorOf(got)];
    }

    private static string ErrorOf(CallToolResult result)
    {
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        using var json = JsonDocument.Parse(text);
        return json.RootElement.GetProperty("error").GetString()!;
    }

    private Task<McpClient> ConnectAsBuyerAsync(string subject) =>
        ConnectAsync(new BearerHandler { Token = fixture.MintToken(subject) });
}
