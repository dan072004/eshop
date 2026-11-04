using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Duende.IdentityServer.Services;
using eShop.Identity.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace eShop.Identity.FunctionalTests;

/// <summary>
/// The real Identity.API in a <see cref="WebApplicationFactory{TEntryPoint}"/>, on a real Postgres container started
/// through Aspire, with its migrations and seeded users (alice, bob). The profile service looks the Buyer up for every
/// issued token, so the database is real. Subject tokens are minted here with Identity.API's own signing key, because
/// a real one needs the interactive sign-in.
/// </summary>
public sealed class IdentityApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string EshopMcpUrl = "http://localhost:5229/mcp";
    public const string EshopMcpServerSecret = "eshop-mcp-server-test-secret";

    // TestServer requests reach Identity.API as http://localhost, so that is the issuer it validates.
    private const string Issuer = "http://localhost";

    private readonly IHost _app;
    private readonly IResourceBuilder<PostgresServerResource> _postgres;
    private string _postgresConnectionString = null!;

    public IdentityApiFixture()
    {
        var options = new DistributedApplicationOptions
        {
            AssemblyName = typeof(IdentityApiFixture).Assembly.FullName,
            DisableDashboard = true
        };
        var appBuilder = DistributedApplication.CreateBuilder(options);
        _postgres = appBuilder.AddPostgres("identitydb");
        _app = appBuilder.Build();
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{_postgres.Resource.Name}"] = _postgresConnectionString,
                ["EshopMcpUrl"] = EshopMcpUrl,
                ["EshopMcpServerSecret"] = EshopMcpServerSecret
            });
        });
        return base.CreateHost(builder);
    }

    public async ValueTask InitializeAsync()
    {
        await _app.StartAsync();
        _postgresConnectionString = (await _postgres.Resource.GetConnectionStringAsync())!;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _app.StopAsync();
        if (_app is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else
        {
            _app.Dispose();
        }
    }

    public async Task<string> UserIdAsync(string userName)
    {
        using var scope = Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByNameAsync(userName);
        return user!.Id;
    }

    /// <summary>
    /// Mints an access token as Identity.API issues one to an MCP client after sign-in (ADR 0005): for
    /// <c>claude-code</c>, with eShop.Mcp's URL as audience and the <c>eshop-mcp</c> scope, signed with Identity.API's
    /// key. Each argument replaces one of those, so a test can get one thing wrong.
    /// </summary>
    public async Task<string> MintSubjectTokenAsync(
        string subject,
        string[]? audiences = null,
        string[]? scopes = null,
        DateTime? expires = null,
        SigningCredentials? signingCredentials = null)
    {
        var expiry = expires ?? DateTime.UtcNow.AddHours(1);
        var credentials = signingCredentials
            ?? await Services.GetRequiredService<IKeyMaterialService>().GetSigningCredentialsAsync();

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            TokenType = "at+jwt",
            Issuer = Issuer,
            IssuedAt = expiry.AddHours(-2),
            NotBefore = expiry.AddHours(-2),
            Expires = expiry,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = subject,
                ["aud"] = audiences ?? [EshopMcpUrl],
                ["scope"] = scopes ?? ["openid", "profile", "eshop-mcp", "offline_access"],
                ["client_id"] = "claude-code",
                ["jti"] = Guid.NewGuid().ToString("N")
            },
            SigningCredentials = credentials
        });
    }
}
