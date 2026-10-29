using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using eShop.Assistant.Tools.Catalog;
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
/// eShop.Mcp in a <see cref="WebApplicationFactory{TEntryPoint}"/>. Identity.API is not started: JWT validation trusts
/// a test signing key and tests mint their own Buyer tokens. The outbound handler behind the Catalog client is faked.
/// </summary>
public sealed class McpServerFixture : WebApplicationFactory<Program>
{
    public const string IdentityUrl = "http://identity.test";
    public const string CatalogUrl = "http://catalog.test";

    private readonly RsaSecurityKey _signingKey = new(RSA.Create(2048)) { KeyId = "test-key" };

    public RecordingHandler Catalog { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Identity:Url", IdentityUrl);
        builder.UseSetting("services:catalog-api:http:0", CatalogUrl);

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

            services.AddHttpClient<CatalogClient>().ConfigurePrimaryHttpMessageHandler(() => Catalog);
        });
    }

    /// <summary>Mints a token as Identity.API would issue it, signed by the test key.</summary>
    public string MintToken(string subject, string[] audiences, DateTime? expires = null, string issuer = IdentityUrl)
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
                ["aud"] = audiences,
                ["scope"] = new[] { "openid", "profile", "eshop-mcp", "orders" },
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
