using Aspire.Hosting.Dotnet;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Foundry;
using Aspire.Hosting.Lifecycle;
using Aspire.Hosting.Yarp;
using Aspire.Hosting.Yarp.Transforms;
using Microsoft.Extensions.Configuration;
using Yarp.ReverseProxy.Configuration;

namespace eShop.AppHost;

internal static class Extensions
{
    /// <summary>
    /// Adds a hook to set the ASPNETCORE_FORWARDEDHEADERS_ENABLED environment variable to true for all projects in the application.
    /// </summary>
    public static IDistributedApplicationBuilder AddForwardedHeaders(this IDistributedApplicationBuilder builder)
    {
        builder.Services.TryAddEventingSubscriber<AddForwardHeadersSubscriber>();
        return builder;
    }

    private class AddForwardHeadersSubscriber : IDistributedApplicationEventingSubscriber
    {
        public Task SubscribeAsync(IDistributedApplicationEventing eventing, DistributedApplicationExecutionContext executionContext, CancellationToken cancellationToken)
        {
            eventing.Subscribe<BeforeStartEvent>((@event, ct) =>
            {
                foreach (var p in @event.Model.Resources.OfType<DotnetProjectResource>())
                {
                    p.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
                    {
                        context.EnvironmentVariables["ASPNETCORE_FORWARDEDHEADERS_ENABLED"] = "true";
                    }));
                }

                return Task.CompletedTask;
            });

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Provisions the AI services chosen by the <c>ChatProvider</c> and <c>EmbeddingProvider</c> settings
    /// (each <c>None</c>, <c>Ollama</c> or <c>Foundry</c>, plus <c>Claude</c> for chat only; case-insensitive; default <c>None</c>).
    /// WebApp is told the chosen Chat Provider and gets its chat model, or for Claude the <c>anthropic-api-key</c> secret
    /// as <c>ANTHROPIC_API_KEY</c>; Catalog.API gets the embedding model.
    /// A provider chosen for both is created once.
    /// </summary>
    public static IDistributedApplicationBuilder AddChatAndEmbeddingProviders(this IDistributedApplicationBuilder builder,
        IResourceBuilder<DotnetProjectResource> catalogApi,
        IResourceBuilder<DotnetProjectResource> webApp)
    {
        var chatProvider = ParseSelector<ChatProvider>(builder.Configuration, "ChatProvider");
        var embeddingProvider = ParseSelector<EmbeddingProvider>(builder.Configuration, "EmbeddingProvider");

        IResourceBuilder<FoundryResource>? foundry = null;
        IResourceBuilder<FoundryResource> GetOrAddFoundry() => foundry ??= builder.AddFoundry("foundry");

        IResourceBuilder<OllamaResource>? ollama = null;
        IResourceBuilder<OllamaResource> GetOrAddOllama() => ollama ??= builder.AddOllama("ollama")
            .WithDataVolume()
            .WithGPUSupport()
            .WithOpenWebUI();

        webApp.WithEnvironment("ChatProvider", chatProvider.ToString());
        switch (chatProvider)
        {
            case ChatProvider.Foundry:
                var chatDeployment = GetOrAddFoundry().AddDeployment("chatModel", "gpt-4.1-mini", "2025-04-14", "OpenAI");
                webApp.WithReference(chatDeployment).WaitFor(chatDeployment);
                break;
            case ChatProvider.Ollama:
                var chatModel = GetOrAddOllama().AddModel("chat", "qwen3:8b");
                webApp.WithReference(chatModel).WaitFor(chatModel);
                break;
            case ChatProvider.Claude:
                // There is no Aspire integration for Anthropic, so the key is a secret parameter (the dashboard
                // prompts for a missing one), passed as the variable the Anthropic SDK reads.
                var anthropicApiKey = builder.AddParameter("anthropic-api-key", secret: true);
                webApp.WithEnvironment("ANTHROPIC_API_KEY", anthropicApiKey);
                break;
        }

        // Catalog.API works out its embedding source from what it is given: OllamaEnabled, or a textEmbeddingModel connection string.
        switch (embeddingProvider)
        {
            case EmbeddingProvider.Foundry:
                var embeddingDeployment = GetOrAddFoundry().AddDeployment("textEmbeddingModel", "text-embedding-3-small", "1", "OpenAI");
                catalogApi.WithReference(embeddingDeployment).WaitFor(embeddingDeployment);
                break;
            case EmbeddingProvider.Ollama:
                var embeddingModel = GetOrAddOllama().AddModel("embedding", "all-minilm");
                catalogApi.WithReference(embeddingModel)
                    .WithEnvironment("OllamaEnabled", "true")
                    .WaitFor(embeddingModel);
                break;
        }

        return builder;
    }

    private enum ChatProvider { None, Ollama, Foundry, Claude }

    private enum EmbeddingProvider { None, Ollama, Foundry }

    // Matches names only (case-insensitively), so a numeric string such as "1" is rejected rather than cast.
    private static TEnum ParseSelector<TEnum>(IConfiguration configuration, string setting) where TEnum : struct, Enum
    {
        var value = configuration[setting];
        if (string.IsNullOrWhiteSpace(value))
        {
            return default;
        }

        var allowed = Enum.GetNames<TEnum>();
        var name = allowed.FirstOrDefault(n => string.Equals(n, value.Trim(), StringComparison.OrdinalIgnoreCase));
        return name is not null
            ? Enum.Parse<TEnum>(name)
            : throw new InvalidOperationException(
                $"The {setting} setting has the unknown value '{value}'. Allowed values: {string.Join(", ", allowed)}.");
    }

    public static IResourceBuilder<YarpResource> ConfigureMobileBffRoutes(this IResourceBuilder<YarpResource> builder,
        IResourceBuilder<DotnetProjectResource> catalogApi,
        IResourceBuilder<DotnetProjectResource> orderingApi,
        IResourceBuilder<DotnetProjectResource> identityApi)
    {
        return builder.WithConfiguration(yarp =>
        {
            var catalogCluster = yarp.AddCluster(catalogApi);

            yarp.AddRoute("/catalog-api/api/catalog/items", catalogCluster)
                .WithMatchRouteQueryParameter([new() { Name = "api-version", Values = ["1.0", "1", "2.0"], Mode = QueryParameterMatchMode.Exact }])
                .WithTransformPathRemovePrefix("/catalog-api");

            yarp.AddRoute("/catalog-api/api/catalog/items/by", catalogCluster)
                .WithMatchRouteQueryParameter([new() { Name = "api-version", Values = ["1.0", "1", "2.0"], Mode = QueryParameterMatchMode.Exact }])
                .WithTransformPathRemovePrefix("/catalog-api");

            yarp.AddRoute("/catalog-api/api/catalog/items/{id}", catalogCluster)
                .WithMatchRouteQueryParameter([new() { Name = "api-version", Values = ["1.0", "1", "2.0"], Mode = QueryParameterMatchMode.Exact }])
                .WithTransformPathRemovePrefix("/catalog-api");

            yarp.AddRoute("/catalog-api/api/catalog/items/by/{name}", catalogCluster)
                .WithMatchRouteQueryParameter([new() { Name = "api-version", Values = ["1.0", "1"], Mode = QueryParameterMatchMode.Exact }])
                .WithTransformPathRemovePrefix("/catalog-api");

            yarp.AddRoute("/catalog-api/api/catalog/items/withsemanticrelevance/{text}", catalogCluster)
                .WithMatchRouteQueryParameter([new() { Name = "api-version", Values = ["1.0", "1"], Mode = QueryParameterMatchMode.Exact }])
                .WithTransformPathRemovePrefix("/catalog-api");

            yarp.AddRoute("/catalog-api/api/catalog/items/withsemanticrelevance", catalogCluster)
                .WithMatchRouteQueryParameter([new() { Name = "api-version", Values = ["2.0"], Mode = QueryParameterMatchMode.Exact }])
                .WithTransformPathRemovePrefix("/catalog-api");

            yarp.AddRoute("/catalog-api/api/catalog/items/type/{typeId}/brand/{brandId?}", catalogCluster)
                .WithMatchRouteQueryParameter([new() { Name = "api-version", Values = ["1.0", "1"], Mode = QueryParameterMatchMode.Exact }])
                .WithTransformPathRemovePrefix("/catalog-api");

            yarp.AddRoute("/catalog-api/api/catalog/items/type/all/brand/{brandId?}", catalogCluster)
                .WithMatchRouteQueryParameter([new() { Name = "api-version", Values = ["1.0", "1"], Mode = QueryParameterMatchMode.Exact }])
                .WithTransformPathRemovePrefix("/catalog-api");

            yarp.AddRoute("/catalog-api/api/catalog/catalogTypes", catalogCluster)
                .WithMatchRouteQueryParameter([new() { Name = "api-version", Values = ["1.0", "1", "2.0"], Mode = QueryParameterMatchMode.Exact }])
                .WithTransformPathRemovePrefix("/catalog-api");

            yarp.AddRoute("/catalog-api/api/catalog/catalogBrands", catalogCluster)
                .WithMatchRouteQueryParameter([new() { Name = "api-version", Values = ["1.0", "1", "2.0"], Mode = QueryParameterMatchMode.Exact }])
                .WithTransformPathRemovePrefix("/catalog-api");

            yarp.AddRoute("/catalog-api/api/catalog/items/{id}/pic", catalogCluster)
                .WithMatchRouteQueryParameter([new() { Name = "api-version", Values = ["1.0", "1", "2.0"], Mode = QueryParameterMatchMode.Exact }])
                .WithTransformPathRemovePrefix("/catalog-api");

            // Generic catalog catch-all route
            yarp.AddRoute("/api/catalog/{*any}", catalogCluster)
                .WithMatchRouteQueryParameter([new() { Name = "api-version", Values = ["1.0", "1", "2.0"], Mode = QueryParameterMatchMode.Exact }]);

            // Ordering routes
            yarp.AddRoute("/api/orders/{*any}", orderingApi.GetEndpoint("http"))
                .WithMatchRouteQueryParameter([new() { Name = "api-version", Values = ["1.0", "1"], Mode = QueryParameterMatchMode.Exact }]);

            // Identity routes
            yarp.AddRoute("/identity/{*any}", identityApi.GetEndpoint("http"))
                .WithTransformPathRemovePrefix("/identity");
        });
    }
}
