using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace eShop.OrderProcessor.FunctionalTests;

/// <summary>
/// Starts Postgres and applies the real Ordering migrations, so the grace-period SQL
/// runs against the schema Ordering.API creates in production.
/// </summary>
public sealed class OrderingDatabaseFixture : IAsyncLifetime
{
    private readonly IHost _app;
    private readonly IResourceBuilder<PostgresDatabaseResource> _database;
    private string? _connectionString;
    private NpgsqlDataSource? _dataSource;

    public string ConnectionString =>
        _connectionString ?? throw new InvalidOperationException("The Ordering database fixture has not been initialized.");

    public NpgsqlDataSource DataSource =>
        _dataSource ?? throw new InvalidOperationException("The Ordering database fixture has not been initialized.");

    public OrderingDatabaseFixture()
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            AssemblyName = typeof(OrderingDatabaseFixture).Assembly.FullName,
            DisableDashboard = true
        });
        _database = builder.AddPostgres("postgres").AddDatabase("orderingdb");
        _app = builder.Build();
    }

    public async ValueTask InitializeAsync()
    {
        await _app.StartAsync();
        var notifications = _app.Services.GetRequiredService<ResourceNotificationService>();
        await notifications.WaitForResourceHealthyAsync(_database.Resource.Name);

        _connectionString = await _database.Resource.ConnectionStringExpression.GetValueAsync(CancellationToken.None);

        await using (var context = CreateContext())
        {
            await context.Database.MigrateAsync();
        }

        _dataSource = NpgsqlDataSource.Create(ConnectionString);
    }

    public OrderingContext CreateContext() =>
        new(new DbContextOptionsBuilder<OrderingContext>().UseNpgsql(ConnectionString).Options);

    public async ValueTask DisposeAsync()
    {
        if (_dataSource is not null)
        {
            await _dataSource.DisposeAsync();
        }

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
}
