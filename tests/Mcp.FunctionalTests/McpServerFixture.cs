using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using eShop.Assistant.Tools.Catalog;
using eShop.Assistant.Tools.Ordering;
using eShop.Assistant.Tools.Policy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace eShop.Mcp.FunctionalTests;

/// <summary>
/// eShop.Mcp in a <see cref="WebApplicationFactory{TEntryPoint}"/>, with no containers. Identity.API is not started:
/// a real Buyer token needs the interactive code flow, and a password grant only for tests would change an existing
/// service. So the token issuer is faked, as the spec's Testing Decisions say: JWT validation trusts a test signing key
/// and tests mint their own Buyer tokens. eShop.Mcp has no database or event bus, and the outbound handlers behind the
/// policy-api, Catalog and Ordering clients are faked. So is Identity.API's token endpoint, which eShop.Mcp calls to
/// exchange the Buyer's token for an Ordering token; the real grant is tested in Identity.FunctionalTests.
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
    public const string EshopMcpServerSecret = "test-eshop-mcp-server-secret";

    /// <summary>How long one attempt of any outbound call may take before its resilience pipeline cancels it.</summary>
    public static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(1);

    private readonly RsaSecurityKey _signingKey = new(RSA.Create(2048)) { KeyId = "test-key" };

    public RecordingHandler Policy { get; } = new();

    public RecordingHandler Catalog { get; } = new();

    public RecordingHandler Ordering { get; } = new();

    /// <summary>
    /// Identity.API's token endpoint. By default it grants every exchange, issuing <see cref="ExchangedToken"/> for
    /// the subject token.
    /// </summary>
    public RecordingHandler Identity { get; } = new() { Respond = GrantExchange };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Identity:Url", IdentityUrl);
        builder.UseSetting("EshopMcpUrl", McpUrl);
        builder.UseSetting("EshopMcpServerSecret", EshopMcpServerSecret);
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
            services.AddHttpClient<TokenExchangeClient>().ConfigurePrimaryHttpMessageHandler(() => Identity);
            // ServiceDefaults adds one standard resilience handler to every client, under the options name "-standard".
            // Its retries and attempt timeout stay on, so a test sees how often a failing backend is called. Only the
            // back-off is removed and the attempt timeout shortened (the fakes answer at once), to keep the suite fast.
            services.Configure<HttpStandardResilienceOptions>("-standard", options =>
            {
                options.Retry.Delay = TimeSpan.Zero;
                options.AttemptTimeout.Timeout = AttemptTimeout;
            });
        });
    }

    /// <summary>The Ordering token the fake token endpoint issues for a subject token, a different one for each.</summary>
    public static string ExchangedToken(string subjectToken) =>
        "orders-token-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(subjectToken)))[..16];

    public static HttpResponseMessage GrantExchange(HttpRequestMessage request)
    {
        var subjectToken = RecordingHandler.ParseForm(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult())["subject_token"];
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                access_token = ExchangedToken(subjectToken),
                issued_token_type = "urn:ietf:params:oauth:token-type:access_token",
                token_type = "Bearer",
                expires_in = 300,
                scope = "orders",
            }),
        };
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

    /// <summary>When set, the backend never answers: each request waits until it is cancelled.</summary>
    public bool Hangs { get; set; }

    public IReadOnlyList<RecordedRequest> Requests => [.. _requests];

    public void Reset() => _requests.Clear();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Buffered, so the body can be both recorded and read again by Respond.
        string? body = null;
        if (request.Content is not null)
        {
            await request.Content.LoadIntoBufferAsync(cancellationToken);
            body = await request.Content.ReadAsStringAsync(cancellationToken);
        }

        _requests.Enqueue(new RecordedRequest(request.Method, request.RequestUri!, request.Headers.Authorization?.ToString(), body));
        if (Hangs)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        return Respond(request);
    }

    public static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    /// <summary>An <c>application/x-www-form-urlencoded</c> body as name/value pairs.</summary>
    public static Dictionary<string, string> ParseForm(string body) =>
        QueryHelpers.ParseQuery(body).ToDictionary(p => p.Key, p => p.Value.ToString());
}

public sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? Authorization, string? Body)
{
    public Dictionary<string, string> Form => RecordingHandler.ParseForm(Body ?? "");
}
