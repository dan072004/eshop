using eShop.Assistant.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace ShoppingAssistant.Evals.World;

public static class EvalWorldServiceCollectionExtensions
{
    /// <summary>
    /// Registers the tools library for real over the fixed world: the clock stopped at <see cref="EvalWorld.Now"/>,
    /// Ordering.API and Catalog.API as stubs at their outbound handlers, and the case's Buyer's token on Ordering calls
    /// (none when <paramref name="signedInAs"/> is null). <c>search_catalog</c> finds the case's
    /// <paramref name="catalogResults"/>, or nothing.
    /// </summary>
    /// <returns>The tools builder, so the runner can point <see cref="AssistantToolsBuilder.PolicyClient"/> at policy-api.</returns>
    public static AssistantToolsBuilder AddEvalWorld(
        this IServiceCollection services, EvalWorld world, string? signedInAs, IReadOnlyList<WorldProduct>? catalogResults = null)
    {
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(world.Now));

        var tools = services.AddAssistantTools();
        tools.OrderingClient.ConfigurePrimaryHttpMessageHandler(() => new OrderingStub(world));
        tools.CatalogClient.ConfigurePrimaryHttpMessageHandler(() => new CatalogStub(world, catalogResults ?? []));
        if (signedInAs is not null)
        {
            var buyer = world.Buyer(signedInAs);
            tools.OrderingClient.AddHttpMessageHandler(() => new BuyerTokenHandler(buyer.UserName));
        }

        return tools;
    }
}

/// <summary>The clock is a boundary we don't own; the world stops it at one moment.</summary>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
