using System.Net;
using System.Net.Http.Headers;
using Asp.Versioning;
using Asp.Versioning.Http;

namespace eShop.Ordering.FunctionalTests;

public sealed class OrderingApiAuthenticationTests : IClassFixture<JwtOrderingApiFixture>
{
    private readonly JwtOrderingApiFixture _fixture;
    private readonly HttpClient _httpClient;

    public OrderingApiAuthenticationTests(JwtOrderingApiFixture fixture)
    {
        _fixture = fixture;
        _httpClient = fixture.CreateDefaultClient(new ApiVersionHandler(new QueryStringApiVersionWriter(), new ApiVersion(1.0)));
    }

    // Without a token, or with one Ordering refuses, the endpoint answers 401, so 200 means the token was accepted.
    [Fact]
    public async Task AnOrdersTokenIsAccepted()
    {
        var response = await GetOrdersAsync(_fixture.MintToken(audience: "orders", scope: "orders"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // The token Claude Code and VS Code hold (ADR 0005). eShop.Mcp exchanges it rather than passing it on, and
    // Ordering must refuse it if anything else sends it.
    [Fact]
    public async Task AnEshopMcpTokenIsRefused()
    {
        var response = await GetOrdersAsync(_fixture.MintToken(audience: "http://localhost:5229/mcp", scope: "eshop-mcp"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ABasketTokenIsRefused()
    {
        var response = await GetOrdersAsync(_fixture.MintToken(audience: "basket", scope: "basket"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private Task<HttpResponseMessage> GetOrdersAsync(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "api/orders");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _httpClient.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
