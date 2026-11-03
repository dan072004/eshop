using Microsoft.AspNetCore.Components.Server.Circuits;

namespace eShop.WebApp.Services.AccessTokens;

/// <summary>
/// Gives code running in another DI scope the current circuit's services. <c>IHttpClientFactory</c> builds message
/// handlers in its own scope (shared by every circuit), so a handler can't inject a circuit-scoped service directly.
/// This is Microsoft's documented pattern ("Access server-side Blazor services from a different DI scope").
/// </summary>
public sealed class CircuitServicesAccessor
{
    private static readonly AsyncLocal<IServiceProvider?> s_circuitServices = new();

    /// <summary>The services of the circuit whose event is being handled; null outside a circuit event.</summary>
    public IServiceProvider? Services
    {
        get => s_circuitServices.Value;
        set => s_circuitServices.Value = value;
    }
}

/// <summary>Sets <see cref="CircuitServicesAccessor.Services"/> for each inbound circuit activity (event, JS interop call).</summary>
public sealed class CircuitServicesAccessorCircuitHandler(IServiceProvider services, CircuitServicesAccessor accessor) : CircuitHandler
{
    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(
        Func<CircuitInboundActivityContext, Task> next) =>
        async context =>
        {
            accessor.Services = services;
            await next(context);
            accessor.Services = null;
        };
}
