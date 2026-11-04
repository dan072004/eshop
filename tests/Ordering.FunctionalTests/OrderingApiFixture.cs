using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace eShop.Ordering.FunctionalTests;

public class OrderingApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string IdentityUrl = "http://identity-api.invalid";

    private readonly IHost _app;

    public IResourceBuilder<PostgresServerResource> Postgres { get; private set; }

    private string _postgresConnectionString;

    public OrderingApiFixture()
    {
        var options = new DistributedApplicationOptions { AssemblyName = typeof(OrderingApiFixture).Assembly.FullName, DisableDashboard = true };
        var appBuilder = DistributedApplication.CreateBuilder(options);
        Postgres = appBuilder.AddPostgres("OrderingDB");
        _app = appBuilder.Build();
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string>
            {
                { $"ConnectionStrings:{Postgres.Resource.Name}", _postgresConnectionString },
                // Ordering.API needs an Identity URL to configure JWT bearer auth. The URL is never called and the
                // fixture runs no Identity API (see ConfigureAuthentication).
                { "Identity:Url", IdentityUrl }
            });
        });
        builder.ConfigureServices(ConfigureAuthentication);
        return base.CreateHost(builder);
    }

    /// <summary>AutoAuthorizeMiddleware signs every request in as <see cref="AutoAuthorizeMiddleware.IDENTITY_ID"/>.</summary>
    protected virtual void ConfigureAuthentication(IServiceCollection services)
    {
        services.AddSingleton<IStartupFilter>(new AutoAuthorizeStartupFilter());
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
    }

    public async ValueTask InitializeAsync()
    {
        await _app.StartAsync();
        _postgresConnectionString = await Postgres.Resource.GetConnectionStringAsync();
    }

    private class AutoAuthorizeStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return builder =>
            {
                builder.UseMiddleware<AutoAuthorizeMiddleware>();
                next(builder);
            };
        }
    }
}
