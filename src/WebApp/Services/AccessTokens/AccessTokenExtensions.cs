using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace eShop.WebApp.Services.AccessTokens;

public static class AccessTokenExtensions
{
    /// <summary>
    /// Attaches the Buyer's token from the circuit's <see cref="AccessTokenHolder"/> to this client's requests.
    /// Use it for clients called from interactive components (the Shopping Assistant), where the
    /// <c>HttpContext</c>-based <c>AddAuthToken()</c> can't be relied on.
    /// </summary>
    public static IHttpClientBuilder AddAccessTokenFromHolder(this IHttpClientBuilder builder)
    {
        var services = builder.Services;
        services.TryAddScoped<AccessTokenHolder>();
        services.TryAddSingleton<CircuitServicesAccessor>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<CircuitHandler, CircuitServicesAccessorCircuitHandler>());
        services.TryAddTransient<AccessTokenHandler>();

        return builder.AddHttpMessageHandler<AccessTokenHandler>();
    }
}
