using eShop.Assistant.Tools.Catalog;
using eShop.Assistant.Tools.Policy;
using Microsoft.Extensions.DependencyInjection;

namespace eShop.Assistant.Tools;

/// <summary>
/// Returned by <see cref="AssistantToolsServiceCollectionExtensions.AddAssistantTools"/> so the host can customise
/// the typed clients (e.g. attach its own token <see cref="DelegatingHandler"/>) and supply host-specific functions.
/// </summary>
public sealed class AssistantToolsBuilder(IServiceCollection services, IHttpClientBuilder catalogClient, IHttpClientBuilder policyClient)
{
    public IServiceCollection Services { get; } = services;

    /// <summary>The Catalog.API v2 client. It needs no token.</summary>
    public IHttpClientBuilder CatalogClient { get; } = catalogClient;

    /// <summary>The policy-api client. Policies are public, so it needs no token.</summary>
    public IHttpClientBuilder PolicyClient { get; } = policyClient;

    /// <summary>
    /// Supplies the function that turns a product id into a picture URL for <c>search_catalog</c> results.
    /// Without one, results leave <c>pictureUrl</c> out.
    /// </summary>
    public AssistantToolsBuilder WithProductPictureUrl(Func<IServiceProvider, ProductPictureUrl> factory)
    {
        Services.AddSingleton(factory);
        return this;
    }
}

public static class AssistantToolsServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="AssistantTools"/> and the typed HTTP clients the tools call, addressed by Aspire service
    /// name. The host is expected to add service discovery (ServiceDefaults does this for every HttpClient).
    /// </summary>
    public static AssistantToolsBuilder AddAssistantTools(this IServiceCollection services)
    {
        var catalogClient = services.AddHttpClient<CatalogClient>(client => client.BaseAddress = new(CatalogClient.BaseAddress))
            .AddApiVersion(2.0);

        var policyClient = services.AddHttpClient<PolicyClient>(client => client.BaseAddress = new(PolicyClient.BaseAddress));

        services.AddTransient<AssistantTools>();

        return new AssistantToolsBuilder(services, catalogClient, policyClient);
    }
}
