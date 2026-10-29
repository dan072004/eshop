using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace eShop.Mcp.FunctionalTests;

public sealed class McpAuthenticationTests(McpServerFixture fixture) : IClassFixture<McpServerFixture>
{
    private const string InitializeRequest = """
        {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}
        """;

    [Fact]
    public async Task RequestWithoutTokenIsRejectedAndPointsAtTheProtectedResourceMetadata()
    {
        using var client = fixture.CreateClient();

        using var response = await client.SendAsync(McpRequest(token: null), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var challenge = Assert.Single(response.Headers.WwwAuthenticate);
        Assert.Equal("Bearer", challenge.Scheme);
        var metadataUrl = Regex.Match(challenge.Parameter ?? "", "resource_metadata=\"([^\"]+)\"").Groups[1].Value;
        Assert.StartsWith("http://localhost/.well-known/oauth-protected-resource", metadataUrl);

        using var metadataResponse = await client.GetAsync(metadataUrl, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, metadataResponse.StatusCode);
        using var metadata = JsonDocument.Parse(await metadataResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("http://localhost/mcp", metadata.RootElement.GetProperty("resource").GetString());
        Assert.Equal(
            [McpServerFixture.IdentityUrl],
            metadata.RootElement.GetProperty("authorization_servers").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(
            ["openid", "profile", "offline_access", "eshop-mcp", "orders"],
            metadata.RootElement.GetProperty("scopes_supported").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task TokenWithoutTheEshopMcpAudienceIsRejected()
    {
        // Ordering.API would accept this token (it doesn't validate the audience); eShop.Mcp must not.
        var token = fixture.MintToken("alice", audiences: ["orders"]);
        using var client = fixture.CreateClient();

        using var response = await client.SendAsync(McpRequest(token), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("resource_metadata=", Assert.Single(response.Headers.WwwAuthenticate).Parameter);
    }

    [Fact]
    public async Task ExpiredTokenIsRejected()
    {
        var token = fixture.MintToken("alice", audiences: ["eshop-mcp", "orders"], expires: DateTime.UtcNow.AddMinutes(-10));
        using var client = fixture.CreateClient();

        using var response = await client.SendAsync(McpRequest(token), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TokenFromAnotherIssuerIsRejected()
    {
        var token = fixture.MintToken("alice", audiences: ["eshop-mcp", "orders"], issuer: "http://elsewhere.test");
        using var client = fixture.CreateClient();

        using var response = await client.SendAsync(McpRequest(token), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task HealthEndpointStaysAnonymous()
    {
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static HttpRequestMessage McpRequest(string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(InitializeRequest, Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }
}
