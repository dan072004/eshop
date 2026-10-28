using System.Runtime.CompilerServices;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Dotnet;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using eShop.AppHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace eShop.AppHost.UnitTests;

[TestClass]
public class AppHostConfigurationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ForwardedHeadersExtensionConfiguresDotnetProjectsOnly()
    {
        var builder = CreateBuilder();
        builder.Services.RemoveAll<IDistributedApplicationEventingSubscriber>();
        builder.AddForwardedHeaders();
        var catalog = builder.AddDotnetProject("catalog-api", ProjectPath("Catalog.API", "Catalog.API.csproj"));
        var webApp = builder.AddDotnetProject("webapp", ProjectPath("WebApp", "WebApp.csproj"));
        var redis = builder.AddRedis("redis");
        var annotationCounts = builder.Resources.ToDictionary(resource => resource, resource => resource.Annotations.Count);

        using var services = builder.Services.BuildServiceProvider();
        var eventing = new DistributedApplicationEventing();
        foreach (var subscriber in services.GetServices<IDistributedApplicationEventingSubscriber>())
        {
            await subscriber.SubscribeAsync(eventing, builder.ExecutionContext, TestContext.CancellationToken);
        }

        var model = services.GetRequiredService<DistributedApplicationModel>();
        await eventing.PublishAsync(new BeforeStartEvent(services, model), TestContext.CancellationToken);

        foreach (var project in new[] { catalog.Resource, webApp.Resource })
        {
            var annotation = project.Annotations.Skip(annotationCounts[project])
                .OfType<EnvironmentCallbackAnnotation>().Single();
            var context = new EnvironmentCallbackContext(builder.ExecutionContext, project, cancellationToken: TestContext.CancellationToken);
            await annotation.Callback(context);

            Assert.AreEqual("true", context.EnvironmentVariables["ASPNETCORE_FORWARDEDHEADERS_ENABLED"]);
        }

        Assert.HasCount(annotationCounts[redis.Resource], redis.Resource.Annotations);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "missing")]
    [DataRow("", DisplayName = "empty")]
    [DataRow("None")]
    [DataRow("none")]
    public async Task NoChatOrEmbeddingProviderAddsNoAIResources(string? configuredValue)
    {
        var app = CreateAppModel(chatProvider: configuredValue, embeddingProvider: configuredValue);

        Assert.IsEmpty(app.AddedResources);

        var webAppEnvironment = await EnvironmentOf(app, app.WebApp);
        Assert.AreEqual("None", webAppEnvironment["ChatProvider"]);
        Assert.IsFalse(HasConnectionString(webAppEnvironment));

        var catalogEnvironment = await EnvironmentOf(app, app.Catalog);
        Assert.IsFalse(catalogEnvironment.ContainsKey("OllamaEnabled"));
        Assert.IsFalse(HasConnectionString(catalogEnvironment));
    }

    [TestMethod]
    [DataRow("Ollama", "Ollama", "ollama", "chat")]
    [DataRow("OLLAMA", "Ollama", "ollama", "chat")]
    [DataRow("Foundry", "Foundry", "foundry", "chatModel")]
    [DataRow("foundry", "Foundry", "foundry", "chatModel")]
    public async Task ChatProviderAddsOnlyItsChatModelAndTellsWebApp(
        string configuredValue, string expectedChatProvider, string providerResource, string chatResource)
    {
        var app = CreateAppModel(chatProvider: configuredValue, embeddingProvider: null);

        CollectionAssert.AreEquivalent(
            new[] { providerResource, chatResource },
            app.AddedResources);

        var webAppEnvironment = await EnvironmentOf(app, app.WebApp);
        Assert.AreEqual(expectedChatProvider, webAppEnvironment["ChatProvider"]);
        Assert.IsTrue(webAppEnvironment.ContainsKey($"ConnectionStrings__{chatResource}"));

        var catalogEnvironment = await EnvironmentOf(app, app.Catalog);
        Assert.IsFalse(catalogEnvironment.ContainsKey("OllamaEnabled"));
        Assert.IsFalse(HasConnectionString(catalogEnvironment));
    }

    [TestMethod]
    [DataRow("Ollama", "ollama", "embedding", "true")]
    [DataRow("ollama", "ollama", "embedding", "true")]
    [DataRow("Foundry", "foundry", "textEmbeddingModel", null)]
    [DataRow("FOUNDRY", "foundry", "textEmbeddingModel", null)]
    public async Task EmbeddingProviderAddsOnlyItsEmbeddingModelAndGivesItToCatalog(
        string configuredValue, string providerResource, string embeddingResource, string? expectedOllamaEnabled)
    {
        var app = CreateAppModel(chatProvider: null, embeddingProvider: configuredValue);

        CollectionAssert.AreEquivalent(
            new[] { providerResource, embeddingResource },
            app.AddedResources);

        var catalogEnvironment = await EnvironmentOf(app, app.Catalog);
        Assert.IsTrue(catalogEnvironment.ContainsKey($"ConnectionStrings__{embeddingResource}"));
        Assert.AreEqual(expectedOllamaEnabled, catalogEnvironment.GetValueOrDefault("OllamaEnabled"));

        var webAppEnvironment = await EnvironmentOf(app, app.WebApp);
        Assert.AreEqual("None", webAppEnvironment["ChatProvider"]);
        Assert.IsFalse(HasConnectionString(webAppEnvironment));
    }

    [TestMethod]
    public async Task FoundryChatWithOllamaEmbeddingsUsesEachProviderForItsOwnPart()
    {
        var app = CreateAppModel(chatProvider: "Foundry", embeddingProvider: "Ollama");

        CollectionAssert.AreEquivalent(
            new[] { "foundry", "chatModel", "ollama", "embedding" },
            app.AddedResources);

        var webAppEnvironment = await EnvironmentOf(app, app.WebApp);
        Assert.AreEqual("Foundry", webAppEnvironment["ChatProvider"]);
        Assert.IsTrue(webAppEnvironment.ContainsKey("ConnectionStrings__chatModel"));

        var catalogEnvironment = await EnvironmentOf(app, app.Catalog);
        Assert.AreEqual("true", catalogEnvironment["OllamaEnabled"]);
        Assert.IsTrue(catalogEnvironment.ContainsKey("ConnectionStrings__embedding"));
    }

    [TestMethod]
    public async Task OllamaChatWithFoundryEmbeddingsUsesEachProviderForItsOwnPart()
    {
        var app = CreateAppModel(chatProvider: "Ollama", embeddingProvider: "Foundry");

        CollectionAssert.AreEquivalent(
            new[] { "ollama", "chat", "foundry", "textEmbeddingModel" },
            app.AddedResources);

        var webAppEnvironment = await EnvironmentOf(app, app.WebApp);
        Assert.AreEqual("Ollama", webAppEnvironment["ChatProvider"]);
        Assert.IsTrue(webAppEnvironment.ContainsKey("ConnectionStrings__chat"));

        var catalogEnvironment = await EnvironmentOf(app, app.Catalog);
        Assert.IsFalse(catalogEnvironment.ContainsKey("OllamaEnabled"));
        Assert.IsTrue(catalogEnvironment.ContainsKey("ConnectionStrings__textEmbeddingModel"));
    }

    [TestMethod]
    [DataRow("Foundry", "foundry", "chatModel", "textEmbeddingModel")]
    [DataRow("Ollama", "ollama", "chat", "embedding")]
    public void ProviderUsedForChatAndEmbeddingsIsCreatedOnce(
        string provider, string providerResource, string chatResource, string embeddingResource)
    {
        var app = CreateAppModel(chatProvider: provider, embeddingProvider: provider);

        CollectionAssert.AreEquivalent(
            new[] { providerResource, chatResource, embeddingResource },
            app.AddedResources);
    }

    [TestMethod]
    [DataRow("ChatProvider", "OpenAI")]
    [DataRow("ChatProvider", "1")]
    [DataRow("EmbeddingProvider", "Claude")]
    [DataRow("EmbeddingProvider", "true")]
    public void UnknownSelectorValueFailsNamingTheSettingAndAllowedValues(string setting, string value)
    {
        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => CreateAppModel(
            chatProvider: setting == "ChatProvider" ? value : null,
            embeddingProvider: setting == "EmbeddingProvider" ? value : null));

        Assert.Contains(setting, exception.Message);
        Assert.Contains($"'{value}'", exception.Message);
        Assert.Contains("None, Ollama, Foundry", exception.Message);
    }

    private sealed record AppModel(
        IDistributedApplicationBuilder Builder,
        string[] AddedResources,
        IResourceBuilder<DotnetProjectResource> Catalog,
        IResourceBuilder<DotnetProjectResource> WebApp);

    private static AppModel CreateAppModel(string? chatProvider, string? embeddingProvider)
    {
        var builder = CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ChatProvider"] = chatProvider,
            ["EmbeddingProvider"] = embeddingProvider
        });
        var catalog = builder.AddDotnetProject("catalog-api", ProjectPath("Catalog.API", "Catalog.API.csproj"));
        var webApp = builder.AddDotnetProject("webapp", ProjectPath("WebApp", "WebApp.csproj"));

        var resourcesBefore = ResourceNames(builder);

        builder.AddChatAndEmbeddingProviders(catalog, webApp);

        // The Azure environment and Open WebUI come with the Foundry and Ollama integrations; we assert on the resources we choose.
        var added = ResourceNames(builder).Except(resourcesBefore).Except(["azure-environment", "ollama-openwebui"]).ToArray();
        return new AppModel(builder, added, catalog, webApp);
    }

    private static string[] ResourceNames(IDistributedApplicationBuilder builder) =>
        builder.Resources.Select(resource => resource.Name).ToArray();

    private static bool HasConnectionString(Dictionary<string, string> environment) =>
        environment.Keys.Any(key => key.StartsWith("ConnectionStrings__", StringComparison.Ordinal));

    // Publish mode renders references as manifest expressions, so nothing has to be provisioned or started.
    private async Task<Dictionary<string, string>> EnvironmentOf(AppModel app, IResourceBuilder<DotnetProjectResource> project)
    {
        using var services = app.Builder.Services.BuildServiceProvider();
        var executionContext = new DistributedApplicationExecutionContext(
            new DistributedApplicationExecutionContextOptions(DistributedApplicationOperation.Publish) { Services = services });

        var configuration = await ExecutionConfigurationBuilder.Create(project.Resource)
            .WithEnvironmentVariablesConfig()
            .BuildAsync(executionContext,
                cancellationToken: TestContext.CancellationToken);

        Assert.IsNull(configuration.Exception);
        return configuration.EnvironmentVariables.ToDictionary();
    }

    private static IDistributedApplicationBuilder CreateBuilder() =>
        DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            AssemblyName = typeof(AppHostConfigurationTests).Assembly.FullName,
            DisableDashboard = true
        });

    private static string ProjectPath(string directory, string project) =>
        Path.Combine(FindRepositoryRoot(), "src", directory, project);

    // Walks up from the output folder, then from this source file (for builds with an --artifacts-path outside the repo).
    private static string FindRepositoryRoot([CallerFilePath] string sourceFile = "") =>
        FindAncestorWithSolution(AppContext.BaseDirectory)
            ?? FindAncestorWithSolution(Path.GetDirectoryName(sourceFile)!)
            ?? throw new DirectoryNotFoundException("Could not locate the eShop repository root.");

    private static string? FindAncestorWithSolution(string start)
    {
        var directory = new DirectoryInfo(start);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "eShop.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName;
    }
}
