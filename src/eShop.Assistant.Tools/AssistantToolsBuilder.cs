using eShop.Assistant.Tools.Catalog;
using eShop.Assistant.Tools.Ordering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace eShop.Assistant.Tools;

/// <summary>
/// Returned by <see cref="AssistantToolsServiceCollectionExtensions.AddAssistantTools"/> so the host can customise
/// the typed clients (e.g. attach its own token <see cref="DelegatingHandler"/>) and supply host-specific functions.
/// </summary>
public sealed class AssistantToolsBuilder(IServiceCollection services, IHttpClientBuilder catalogClient)
{
    public IServiceCollection Services { get; } = services;

    /// <summary>The Catalog.API v2 client. It needs no token.</summary>
    public IHttpClientBuilder CatalogClient { get; } = catalogClient;

    /// <summary>
    /// The Ordering.API v1 client. Its calls need the Buyer's token, which the host attaches with its own
    /// <see cref="DelegatingHandler"/> (<c>OrderingClient.AddHttpMessageHandler&lt;...&gt;()</c>); the library has no
    /// auth code. Without a token, Ordering.API returns 401 and the order tools return <c>sign_in_required</c>.
    /// </summary>
    public required IHttpClientBuilder OrderingClient { get; init; }

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

        var orderingClient = services.AddHttpClient<OrderingClient>(client => client.BaseAddress = new(OrderingClient.BaseAddress))
            .AddApiVersion(1.0);

        services.TryAddSingleton(TimeProvider.System);
        services.AddTransient<AssistantTools>();

        return new AssistantToolsBuilder(services, catalogClient)
        {
            OrderingClient = orderingClient,
        };
    }
}
