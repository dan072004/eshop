using eShop.Assistant.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace ShoppingAssistant.Evals.World;

/// <summary>
/// The fixed world's stubs, seen through the tools library's tool functions invoked as a model would. They show that
/// the stubs answer as the real Ordering.API and Catalog.API do: your own orders, a 404 for anyone else's, a 401 with
/// no token.
/// </summary>
public sealed class WorldToolsTests
{
    [Fact]
    public async Task AliceListsHerFourOrdersNewestFirstWithTheWorldsToday()
    {
        var result = await InvokeAsync(ToolsFor("alice").ListMyOrders);

        var expected = JsonElement.Parse("""
            {
              "today": "2026-11-01",
              "orders": [
                { "orderNumber": 102, "orderDate": "2026-10-30T09:30:00Z", "status": "AwaitingValidation", "total": 59.99 },
                { "orderNumber": 104, "orderDate": "2026-10-25T09:30:00Z", "status": "Cancelled", "total": 129.99 },
                { "orderNumber": 101, "orderDate": "2026-10-20T09:30:00Z", "status": "Shipped", "total": 379.98 },
                { "orderNumber": 103, "orderDate": "2026-07-15T09:30:00Z", "status": "Shipped", "total": 109.99 }
              ]
            }
            """);
        Assert.True(JsonElement.DeepEquals(expected, result), result.GetRawText());
    }

    [Fact]
    public async Task AlicesOrder101HasTheGpsWatchAndTheJacketWithTheirCatalogTypeAndBrand()
    {
        var result = await InvokeAsync(ToolsFor("alice").GetOrder, new() { ["orderNumber"] = 101 });

        var expected = JsonElement.Parse("""
            {
              "today": "2026-11-01",
              "orderNumber": 101,
              "orderDate": "2026-10-20T09:30:00Z",
              "status": "Shipped",
              "items": [
                { "productId": 9, "productName": "VenturePro GPS Watch", "units": 1, "unitPrice": 199.99, "type": "Navigation", "brand": "AirStrider" },
                { "productId": 8, "productName": "Frostbite Insulated Jacket", "units": 1, "unitPrice": 179.99, "type": "Jackets", "brand": "Grolltex" }
              ]
            }
            """);
        Assert.True(JsonElement.DeepEquals(expected, result), result.GetRawText());
    }

    [Fact]
    public async Task AliceAskingForBobsOrder201GetsNotFound()
    {
        var result = await InvokeAsync(ToolsFor("alice").GetOrder, new() { ["orderNumber"] = 201 });

        var expected = JsonElement.Parse("""
            { "error": "not_found", "message": "The Buyer has no order with that order number." }
            """);
        Assert.True(JsonElement.DeepEquals(expected, result), result.GetRawText());
    }

    [Fact]
    public async Task ASignedOutBuyerListingOrdersGetsSignInRequired()
    {
        var result = await InvokeAsync(ToolsFor(signedInAs: null).ListMyOrders);

        var expected = JsonElement.Parse("""
            { "error": "sign_in_required", "message": "The Buyer must sign in to see their orders." }
            """);
        Assert.True(JsonElement.DeepEquals(expected, result), result.GetRawText());
    }

    [Fact]
    public async Task BobListsOnlyHisOwnOrders202And201()
    {
        var result = await InvokeAsync(ToolsFor("bob").ListMyOrders);

        var expected = JsonElement.Parse("""
            {
              "today": "2026-11-01",
              "orders": [
                { "orderNumber": 202, "orderDate": "2026-10-31T09:30:00Z", "status": "Submitted", "total": 189.99 },
                { "orderNumber": 201, "orderDate": "2026-10-28T09:30:00Z", "status": "Paid", "total": 699.00 }
              ]
            }
            """);
        Assert.True(JsonElement.DeepEquals(expected, result), result.GetRawText());
    }

    [Fact]
    public async Task CatalogSearchReturnsTheResultsTheCaseLists()
    {
        WorldProduct navigator = new(50, "Astro GPS Navigator", "A handheld GPS navigator.", 249.99m, "Navigation", "AirStrider");
        var tools = ToolsFor(signedInAs: null, catalogResults: [navigator]);

        var result = await InvokeAsync(tools.SearchCatalog, new() { ["productDescription"] = "a GPS for hiking" });

        Assert.Equal(1, result.GetProperty("count").GetInt32());
        Assert.Equal(
            [(50, "Astro GPS Navigator", 249.99m)],
            result.GetProperty("data").EnumerateArray().Select(i => (
                i.GetProperty("id").GetInt32(), i.GetProperty("name").GetString(), i.GetProperty("price").GetDecimal())));
    }

    [Fact]
    public async Task CatalogSearchFindsNothingWhenTheCaseListsNoResults()
    {
        var result = await InvokeAsync(ToolsFor(signedInAs: null).SearchCatalog, new() { ["productDescription"] = "kayaks" });

        var expected = JsonElement.Parse("""
            { "pageIndex": 0, "pageSize": 8, "count": 0, "data": [] }
            """);
        Assert.True(JsonElement.DeepEquals(expected, result), result.GetRawText());
    }

    private static AssistantTools ToolsFor(string? signedInAs, IReadOnlyList<WorldProduct>? catalogResults = null)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddEvalWorld(EvalWorld.Load(), signedInAs, catalogResults);
        return services.BuildServiceProvider().GetRequiredService<AssistantTools>();
    }

    private static async Task<JsonElement> InvokeAsync(AIFunction tool, AIFunctionArguments? arguments = null)
    {
        var result = await tool.InvokeAsync(arguments ?? [], TestContext.Current.CancellationToken);
        return result is JsonElement element ? element : JsonSerializer.SerializeToElement(result);
    }
}
