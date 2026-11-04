using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using eShop.Assistant.Tools.Catalog;
using eShop.Assistant.Tools.Ordering;
using eShop.Assistant.Tools.Policy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace eShop.Mcp.FunctionalTests;

/// <summary>
/// eShop.Mcp in a <see cref="WebApplicationFactory{TEntryPoint}"/>, with no containers. Identity.API is not started:
/// a real Buyer token needs the interactive code flow, and a password grant only for tests would change an existing
/// service. So the token issuer is faked, as the spec's Testing Decisions say: JWT validation trusts a test signing key
/// and tests mint their own Buyer tokens. eShop.Mcp has no database or event bus, and the outbound handlers behind the
/// policy-api, Catalog and Ordering clients are faked.
/// </summary>
public sealed class McpServerFixture : WebApplicationFactory<Program>
{
    public const string IdentityUrl = "http://identity.test";
    // Deliberately not the host the test client reaches (localhost), so the advertised resource is shown to be the
    // configured value rather than one derived from the request.
    public const string McpUrl = "http://mcp.test/mcp";
    public const string PolicyUrl = "http://policy.test";
    public const string CatalogUrl = "http://catalog.test";
    public const string OrderingUrl = "http://ordering.test";

    private readonly RsaSecurityKey _signingKey = new(RSA.Create(2048)) { KeyId = "test-key" };

    public RecordingHandler Policy { get; } = new();

    public RecordingHandler Catalog { get; } = new();

    public RecordingHandler Ordering { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Identity:Url", IdentityUrl);
        builder.UseSetting("EshopMcpUrl", McpUrl);
        builder.UseSetting("services:policy-api:http:0", PolicyUrl);
        builder.UseSetting("services:catalog-api:http:0", CatalogUrl);
        builder.UseSetting("services:ordering-api:http:0", OrderingUrl);

        builder.ConfigureTestServices(services =>
        {
            // A static configuration stops JwtBearer fetching discovery from Identity.API; everything else
            // (issuer, audience, lifetime) is validated exactly as eShop.Mcp configures it.
            services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var configuration = new OpenIdConnectConfiguration { Issuer = IdentityUrl };
                configuration.SigningKeys.Add(_signingKey);
                options.Configuration = configuration;
            });

            services.AddHttpClient<PolicyClient>().ConfigurePrimaryHttpMessageHandler(() => Policy);
            services.AddHttpClient<CatalogClient>().ConfigurePrimaryHttpMessageHandler(() => Catalog);
            services.AddHttpClient<OrderingClient>().ConfigurePrimaryHttpMessageHandler(() => Ordering);
        });
    }

    /// <summary>
    /// Mints a token as Identity.API would issue it, signed by the test key. By default its only audience is eShop.Mcp's
    /// URL, as for a client that names it as the resource (ADR 0005).
    /// </summary>
    public string MintToken(string subject, string[]? audiences = null, DateTime? expires = null, string issuer = IdentityUrl)
    {
        var expiry = expires ?? DateTime.UtcNow.AddHours(1);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            IssuedAt = expiry.AddHours(-2),
            NotBefore = expiry.AddHours(-2),
            Expires = expiry,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = subject,
                ["aud"] = audiences ?? [McpUrl],
                ["scope"] = new[] { "openid", "profile", "offline_access", "eshop-mcp" },
            },
            SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}

/// <summary>Fakes a backend: records each request as it was sent and answers with the configured response.</summary>
public sealed class RecordingHandler : HttpMessageHandler
{
    private readonly ConcurrentQueue<RecordedRequest> _requests = new();

    public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } =
        _ => new HttpResponseMessage(HttpStatusCode.NotFound);

    public IReadOnlyList<RecordedRequest> Requests => [.. _requests];

    public void Reset() => _requests.Clear();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _requests.Enqueue(new RecordedRequest(request.RequestUri!, request.Headers.Authorization?.ToString()));
        return Task.FromResult(Respond(request));
    }

    public static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}

public sealed record RecordedRequest(Uri Uri, string? Authorization);
