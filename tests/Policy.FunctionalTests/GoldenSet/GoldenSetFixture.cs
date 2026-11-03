using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace eShop.Policy.FunctionalTests;

/// <summary>
/// policy-api indexing the real <c>policies/</c> folder with a real Ollama embedding model (all-minilm, as the
/// AppHost uses), wired through the same <c>OllamaEnabled</c> and <c>embedding</c> connection the AppHost gives it.
/// </summary>
/// <remarks>
/// It starts containers only when <see cref="EnvironmentVariable"/> is set to <c>1</c>; CI never sets it.
/// The first run pulls the Ollama image and the model, which takes a few minutes.
/// </remarks>
public sealed class GoldenSetFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string EnvironmentVariable = "ESHOP_POLICY_GOLDEN_SET";

    public const string EmbeddingModel = "all-minilm";

    private IHost _app;
    private string _postgresConnectionString;
    private string _embeddingConnectionString;

    public static bool IsEnabled => Environment.GetEnvironmentVariable(EnvironmentVariable) == "1";

    public async ValueTask InitializeAsync()
    {
        if (!IsEnabled)
        {
            return;
        }

        var appBuilder = TestAppHost.CreateBuilder();
        var postgres = appBuilder.AddPolicyPostgres();
        var embedding = appBuilder.AddOllama("ollama")
            .WithDataVolume()
            .AddModel("embedding", EmbeddingModel);
        _app = appBuilder.Build();

        await _app.StartAsync();

        // The model resource turns healthy once Ollama has pulled the model.
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        await _app.Services.GetRequiredService<ResourceNotificationService>()
            .WaitForResourceHealthyAsync(embedding.Resource.Name, timeout.Token);

        _postgresConnectionString = await postgres.Resource.GetConnectionStringAsync();
        _embeddingConnectionString = await embedding.Resource.ConnectionStringExpression.GetValueAsync(timeout.Token);
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string>
            {
                { $"ConnectionStrings:{TestAppHost.PostgresName}", _postgresConnectionString },
                { "ConnectionStrings:embedding", _embeddingConnectionString },
                { "OllamaEnabled", "true" },
                { "Policies:Folder", Path.Combine(FindRepositoryRoot(), "policies") },
            });
        });
        return base.CreateHost(builder);
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        if (_app is not null)
        {
            await TestAppHost.StopAndDisposeAsync(_app);
        }
    }

    private static string FindRepositoryRoot([CallerFilePath] string sourceFile = "")
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(sourceFile)!);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "eShop.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the eShop repository root.");
    }
}
