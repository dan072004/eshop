using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using eShop.Policy.API.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ShoppingAssistant.Evals.Running;

/// <summary>
/// The real policy-api over a real Postgres (with pgvector, as the AppHost uses), indexing the real Policies (the copy
/// of <c>policies/</c> in the build output). It has no embedding provider, so search runs in keyword mode, as in CI.
/// Postgres starts through an Aspire <see cref="DistributedApplication"/>, as in the Policy functional tests.
/// </summary>
/// <remarks>
/// Nothing starts until <see cref="StartAsync"/>, so a skipped eval, or one that fails for want of a key, needs no
/// Docker. The type argument only names Policy.API's assembly; WebApp, also referenced, has its own <c>Program</c>.
/// </remarks>
public sealed class PolicyApiFixture : WebApplicationFactory<PolicyIngestion>
{
    private const string PostgresName = "policydb";

    private readonly SemaphoreSlim _starting = new(1, 1);
    private IHost? _app;
    private string? _postgresConnectionString;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _starting.WaitAsync(cancellationToken);
        try
        {
            if (_app is not null)
            {
                return;
            }

            var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
            {
                AssemblyName = typeof(PolicyApiFixture).Assembly.FullName,
                DisableDashboard = true,
            });
            var postgres = builder.AddPostgres(PostgresName).WithImage("ankane/pgvector").WithImageTag("latest");
            _app = builder.Build();

            await _app.StartAsync(cancellationToken);
            _postgresConnectionString = await postgres.Resource.GetConnectionStringAsync(cancellationToken);

            // Building the server runs policy-api's startup ingestion.
            _ = Server;
        }
        finally
        {
            _starting.Release();
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting($"ConnectionStrings:{PostgresName}", _postgresConnectionString
            ?? throw new InvalidOperationException($"Call {nameof(StartAsync)} before using policy-api."));

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (_app is not null)
        {
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

        _starting.Dispose();
    }
}
