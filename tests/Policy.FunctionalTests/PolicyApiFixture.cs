using System.IO;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using eShop.Policy.API.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace eShop.Policy.FunctionalTests;

/// <summary>
/// policy-api against a real Postgres (with pgvector), reading its Policy files from a temporary folder
/// that tests write to, edit and delete from before running ingestion again.
/// </summary>
public class PolicyApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly IHost _app;
    private string _postgresConnectionString;

    public IResourceBuilder<PostgresServerResource> Postgres { get; }

    public string PoliciesFolder { get; } = Directory.CreateTempSubdirectory("policies-").FullName;

    public PolicyApiFixture()
    {
        var options = new DistributedApplicationOptions { AssemblyName = typeof(PolicyApiFixture).Assembly.FullName, DisableDashboard = true };
        var appBuilder = DistributedApplication.CreateBuilder(options);
        Postgres = appBuilder.AddPostgres("policydb")
            .WithImage("ankane/pgvector")
            .WithImageTag("latest");
        _app = appBuilder.Build();
    }

    public void WritePolicy(string slug, string content) =>
        File.WriteAllText(Path.Combine(PoliciesFolder, $"{slug}.md"), content);

    public void DeletePolicy(string slug) =>
        File.Delete(Path.Combine(PoliciesFolder, $"{slug}.md"));

    /// <summary>Runs ingestion again, as a restart of policy-api would.</summary>
    public async Task IngestAsync()
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PolicyIngestion>().IngestAsync(TestContext.Current.CancellationToken);
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string>
            {
                { $"ConnectionStrings:{Postgres.Resource.Name}", _postgresConnectionString },
                { "Policies:Folder", PoliciesFolder },
            });
        });
        builder.ConfigureServices(ConfigureTestServices);
        return base.CreateHost(builder);
    }

    /// <summary>Replaces services after the app has registered its own; with none replaced, there is no embedding generator.</summary>
    protected virtual void ConfigureTestServices(IServiceCollection services)
    {
    }

    public async ValueTask InitializeAsync()
    {
        // Startup ingestion indexes whatever is in the folder when the host starts.
        WritePolicy("returns-exceptions", """
            ---
            title: Returns Exceptions
            category: returns
            last_updated: 2026-10-28
            ---

            # Returns Exceptions

            Some items have stricter return rules. These override the standard Returns Policy.

            ## Helmets
            Bike helmets cannot be returned once worn outside, or after any impact.

            ## GPS watches, navigators and bike computers
            Navigation electronics have a 30-day return window from the order date, not 60 days.
            """);
        WritePolicy("shipping-destinations", """
            ---
            title: Shipping Destinations
            category: shipping
            ---

            # Shipping Destinations

            ## Canada
            We ship to every Canadian province. Duties and taxes are collected at checkout.

            ## Helmets
            Helmets ship to all destinations in their original box.
            """);

        await _app.StartAsync();
        _postgresConnectionString = await Postgres.Resource.GetConnectionStringAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _app.StopAsync();
        if (_app is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            _app.Dispose();
        }

        Directory.Delete(PoliciesFolder, recursive: true);
    }
}
