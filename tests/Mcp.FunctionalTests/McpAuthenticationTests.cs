using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace eShop.Mcp.FunctionalTests;

public sealed class McpAuthenticationTests(McpServerFixture fixture) : IClassFixture<McpServerFixture>
{
    private const string InitializeRequest = """
        {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}
        """;

    // The advertised resource is the configured URL whatever host form the request used, so a client that connected
    // another way (127.0.0.1 instead of localhost) sees the mismatch and refuses before sign-in (ADR 0005).
    [Theory]
    [InlineData("http://localhost")]
    [InlineData("http://127.0.0.1")]
    public async Task RequestWithoutTokenIsRejectedAndPointsAtMetadataNamingTheConfiguredUrl(string baseAddress)
    {
        using var client = fixture.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri(baseAddress) });

        using var response = await client.SendAsync(McpRequest(token: null), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var challenge = Assert.Single(response.Headers.WwwAuthenticate);
        Assert.Equal("Bearer", challenge.Scheme);
        var metadataUrl = Regex.Match(challenge.Parameter ?? "", "resource_metadata=\"([^\"]+)\"").Groups[1].Value;
        Assert.StartsWith($"{baseAddress}/.well-known/oauth-protected-resource", metadataUrl);

        using var metadataResponse = await client.GetAsync(metadataUrl, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, metadataResponse.StatusCode);
        using var metadata = JsonDocument.Parse(await metadataResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("http://mcp.test/mcp", metadata.RootElement.GetProperty("resource").GetString());
        Assert.Equal(
            ["http://identity.test"],
            metadata.RootElement.GetProperty("authorization_servers").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(
            ["openid", "profile", "offline_access", "eshop-mcp"],
            metadata.RootElement.GetProperty("scopes_supported").EnumerateArray().Select(e => e.GetString()));
    }

    // "eshop-mcp" was the resource's name before it was named by its URL; "orders" is Ordering.API's audience, which
    // Ordering.API accepts without validating it. Neither may be replayed through eShop.Mcp.
    [Theory]
    [InlineData("eshop-mcp")]
    [InlineData("orders")]
    public async Task TokenWithoutTheConfiguredUrlAsAudienceIsRejected(string audience)
    {
        var token = fixture.MintToken("alice", audiences: [audience]);
        using var client = fixture.CreateClient();

        using var response = await client.SendAsync(McpRequest(token), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("resource_metadata=", Assert.Single(response.Headers.WwwAuthenticate).Parameter);
    }

    [Fact]
    public async Task ExpiredTokenIsRejected()
    {
        var token = fixture.MintToken("alice", expires: DateTime.UtcNow.AddMinutes(-10));
        using var client = fixture.CreateClient();

        using var response = await client.SendAsync(McpRequest(token), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TokenFromAnotherIssuerIsRejected()
    {
        var token = fixture.MintToken("alice", issuer: "http://elsewhere.test");
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
