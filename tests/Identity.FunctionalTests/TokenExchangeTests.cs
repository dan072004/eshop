using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace eShop.Identity.FunctionalTests;

/// <summary>
/// eShop.Mcp exchanges a Buyer's eShop.Mcp token for an Ordering token (RFC 8693, ADR 0005), through the real
/// <c>/connect/token</c> endpoint, so Duende's pipeline decides what passes.
/// </summary>
[Collection(IdentityApiCollection.Name)]
public sealed class TokenExchangeTests(IdentityApiFixture fixture)
{
    private const string TokenExchangeGrant = "urn:ietf:params:oauth:grant-type:token-exchange";
    private const string AccessTokenType = "urn:ietf:params:oauth:token-type:access_token";

    [Fact]
    public async Task ExchangingAlicesMcpTokenIssuesAFiveMinuteOrdersTokenForAliceActedOnByEshopMcp()
    {
        var aliceId = await fixture.UserIdAsync("alice");

        var response = await ExchangeAsync(await fixture.MintSubjectTokenAsync(aliceId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.False(body.TryGetProperty("refresh_token", out _));
        var token = new JsonWebToken(body.GetProperty("access_token").GetString());
        Assert.Equivalent(
            new
            {
                Subject = aliceId,
                Audiences = new[] { "orders" },
                Scopes = new[] { "orders" },
                Lifetime = TimeSpan.FromMinutes(5)
            },
            new
            {
                token.Subject,
                token.Audiences,
                Scopes = token.Claims.Where(c => c.Type == "scope").Select(c => c.Value),
                Lifetime = token.ValidTo - token.IssuedAt
            });
        Assert.True(JsonElement.DeepEquals(
            JsonDocument.Parse("""{ "client_id": "eshop-mcp-server" }""").RootElement,
            JsonDocument.Parse(token.GetClaim("act").Value).RootElement));
    }

    [Fact]
    public async Task ASubjectTokenForAnotherAudienceIsRefused()
    {
        var subjectToken = await fixture.MintSubjectTokenAsync(await fixture.UserIdAsync("alice"), audiences: ["orders"]);

        await AssertRefusedAsync(await ExchangeAsync(subjectToken), HttpStatusCode.BadRequest, "invalid_grant");
    }

    [Fact]
    public async Task ASubjectTokenWithoutTheEshopMcpScopeIsRefused()
    {
        var subjectToken = await fixture.MintSubjectTokenAsync(
            await fixture.UserIdAsync("alice"), scopes: ["openid", "profile", "orders"]);

        await AssertRefusedAsync(await ExchangeAsync(subjectToken), HttpStatusCode.BadRequest, "invalid_grant");
    }

    [Fact]
    public async Task AnExpiredSubjectTokenIsRefused()
    {
        // Past the 5-minute clock skew Duende allows.
        var subjectToken = await fixture.MintSubjectTokenAsync(
            await fixture.UserIdAsync("alice"), expires: DateTime.UtcNow.AddMinutes(-10));

        await AssertRefusedAsync(await ExchangeAsync(subjectToken), HttpStatusCode.BadRequest, "invalid_grant");
    }

    [Fact]
    public async Task ASubjectTokenSignedByAnotherKeyIsRefused()
    {
        var otherKey = new SigningCredentials(new RsaSecurityKey(RSA.Create(2048)) { KeyId = "other-key" }, SecurityAlgorithms.RsaSha256);
        var subjectToken = await fixture.MintSubjectTokenAsync(await fixture.UserIdAsync("alice"), signingCredentials: otherKey);

        await AssertRefusedAsync(await ExchangeAsync(subjectToken), HttpStatusCode.BadRequest, "invalid_grant");
    }

    [Fact]
    public async Task AnExchangeRequestedByClaudeCodeIsRefused()
    {
        var subjectToken = await fixture.MintSubjectTokenAsync(await fixture.UserIdAsync("alice"));

        // claude-code is a public client, so it sends no secret.
        var response = await ExchangeAsync(subjectToken, clientId: "claude-code", clientSecret: null);

        // Duende answers a grant type the client isn't allowed with unsupported_grant_type.
        await AssertRefusedAsync(response, HttpStatusCode.BadRequest, "unsupported_grant_type");
    }

    [Fact]
    public async Task AnExchangeWithTheWrongClientSecretIsRefused()
    {
        var subjectToken = await fixture.MintSubjectTokenAsync(await fixture.UserIdAsync("alice"));

        var response = await ExchangeAsync(subjectToken, clientSecret: "not-the-secret");

        await AssertRefusedAsync(response, HttpStatusCode.BadRequest, "invalid_client");
    }

    [Fact]
    public async Task AnExchangeForAScopeOtherThanOrdersIsRefused()
    {
        var subjectToken = await fixture.MintSubjectTokenAsync(await fixture.UserIdAsync("alice"));

        await AssertRefusedAsync(await ExchangeAsync(subjectToken, scope: "basket"), HttpStatusCode.BadRequest, "invalid_scope");
    }

    [Fact]
    public async Task AnExchangeForAUserWhoDoesNotExistIsRefused()
    {
        var subjectToken = await fixture.MintSubjectTokenAsync(Guid.NewGuid().ToString());

        await AssertRefusedAsync(await ExchangeAsync(subjectToken), HttpStatusCode.BadRequest, "invalid_grant");
    }

    private static async Task AssertRefusedAsync(HttpResponseMessage response, HttpStatusCode status, string error)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal((status, error), (response.StatusCode, body.GetProperty("error").GetString()));
        Assert.False(body.TryGetProperty("access_token", out _));
    }

    private async Task<HttpResponseMessage> ExchangeAsync(
        string subjectToken,
        string clientId = "eshop-mcp-server",
        string? clientSecret = IdentityApiFixture.EshopMcpServerSecret,
        string scope = "orders")
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = TokenExchangeGrant,
            ["subject_token"] = subjectToken,
            ["subject_token_type"] = AccessTokenType,
            ["scope"] = scope,
            ["client_id"] = clientId
        };
        if (clientSecret is not null)
        {
            form["client_secret"] = clientSecret;
        }

        using var client = fixture.CreateClient();
        return await client.PostAsync("/connect/token", new FormUrlEncodedContent(form), TestContext.Current.CancellationToken);
    }
}
