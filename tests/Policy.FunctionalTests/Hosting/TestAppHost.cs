using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace eShop.Policy.FunctionalTests;

/// <summary>The Aspire app the fixtures start for their containers: Postgres with pgvector, as the AppHost uses.</summary>
internal static class TestAppHost
{
    public const string PostgresName = "policydb";

    public static IDistributedApplicationBuilder CreateBuilder() =>
        DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            AssemblyName = typeof(TestAppHost).Assembly.FullName,
            DisableDashboard = true,
        });

    public static IResourceBuilder<PostgresServerResource> AddPolicyPostgres(this IDistributedApplicationBuilder builder) =>
        builder.AddPostgres(PostgresName)
            .WithImage("ankane/pgvector")
            .WithImageTag("latest");

    public static async Task StopAndDisposeAsync(IHost app)
    {
        await app.StopAsync();
        if (app is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            app.Dispose();
        }
    }
}
