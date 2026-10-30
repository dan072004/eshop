using System.Net;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace eShop.Assistant.Tools.UnitTests;

[TestClass]
public class GetOrderToolTests
{
    // What Ordering.API v1 returns from GET api/orders/12 (its Order view model, web JSON casing).
    private const string OrderDetailsJson = """
        {
          "orderNumber": 12,
          "date": "2026-10-27T15:04:05Z",
          "status": "Shipped",
          "description": null,
          "street": "1 Main St",
          "city": "Redmond",
          "state": "WA",
          "zipcode": "98052",
          "country": "US",
          "orderItems": [
            { "productId": 31, "productName": "TrailFinder GPS Watch", "units": 1, "unitPrice": 199.99, "pictureUrl": "" },
            { "productId": 5, "productName": "Alpine Fleece Jacket", "units": 2, "unitPrice": 45.5, "pictureUrl": "" }
          ],
          "total": 290.99
        }
        """;

    // What Catalog.API returns from GET api/catalog/items/by (CatalogItem without its navigation properties).
    private const string CatalogItems = """
        [
          { "id": 5, "name": "Alpine Fleece Jacket", "price": 45.5, "catalogTypeId": 2, "catalogType": null, "catalogBrandId": 9, "catalogBrand": null, "availableStock": 10 },
          { "id": 31, "name": "TrailFinder GPS Watch", "price": 199.99, "catalogTypeId": 4, "catalogType": null, "catalogBrandId": 3, "catalogBrand": null, "availableStock": 10 }
        ]
        """;

    private const string CatalogTypes = """
        [ { "id": 2, "type": "Jackets" }, { "id": 4, "type": "Navigation" } ]
        """;

    private const string CatalogBrands = """
        [ { "id": 3, "brand": "Daybird" }, { "id": 9, "brand": "WildRunner" } ]
        """;

    private static readonly DateTimeOffset Now = new(2026, 10, 29, 23, 30, 0, TimeSpan.Zero);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task GetOrderReturnsTheOrderWithEachItemsTypeAndBrandFromCatalog()
    {
        var ordering = new FakeHandler(_ => Json(HttpStatusCode.OK, OrderDetailsJson));
        var catalog = CatalogHandler(CatalogItems);
        var tool = BuildTools(ordering, catalog).GetOrder;

        var result = await InvokeAsync(tool, 12);

        var request = ordering.Requests.Single();
        Assert.AreEqual("ordering-api", request.RequestUri!.Host);
        Assert.AreEqual("/api/orders/12", request.RequestUri.AbsolutePath);
        Assert.AreEqual("1.0", System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query)["api-version"]);

        var expected = JsonElement.Parse("""
            {
              "today": "2026-10-29",
              "orderNumber": 12,
              "orderDate": "2026-10-27T15:04:05Z",
              "status": "Shipped",
              "items": [
                { "productId": 31, "productName": "TrailFinder GPS Watch", "units": 1, "unitPrice": 199.99, "type": "Navigation", "brand": "Daybird" },
                { "productId": 5, "productName": "Alpine Fleece Jacket", "units": 2, "unitPrice": 45.5, "type": "Jackets", "brand": "WildRunner" }
              ]
            }
            """);
        Assert.IsTrue(JsonElement.DeepEquals(expected, result), result.GetRawText());
    }

    [TestMethod]
    public async Task GetOrderGivesNullTypeAndBrandToAProductCatalogDoesNotKnow()
    {
        // Catalog's batch endpoint leaves unknown ids out; here it knows only product 5.
        const string OnlyTheJacket = """
            [ { "id": 5, "name": "Alpine Fleece Jacket", "price": 45.5, "catalogTypeId": 2, "catalogBrandId": 9 } ]
            """;
        var ordering = new FakeHandler(_ => Json(HttpStatusCode.OK, OrderDetailsJson));
        var tool = BuildTools(ordering, CatalogHandler(OnlyTheJacket)).GetOrder;

        var result = await InvokeAsync(tool, 12);

        var expected = JsonElement.Parse("""
            [
              { "productId": 31, "productName": "TrailFinder GPS Watch", "units": 1, "unitPrice": 199.99, "type": null, "brand": null },
              { "productId": 5, "productName": "Alpine Fleece Jacket", "units": 2, "unitPrice": 45.5, "type": "Jackets", "brand": "WildRunner" }
            ]
            """);
        var items = result.GetProperty("items");
        Assert.IsTrue(JsonElement.DeepEquals(expected, items), items.GetRawText());
    }

    [TestMethod]
    [DataRow(HttpStatusCode.NotFound, "not_found")]
    [DataRow(HttpStatusCode.Unauthorized, "sign_in_required")]
    [DataRow(HttpStatusCode.InternalServerError, "unavailable")]
    public async Task GetOrderReturnsAnErrorWhenOrderingFails(HttpStatusCode status, string error)
    {
        var ordering = new FakeHandler(_ => new HttpResponseMessage(status));
        var catalog = CatalogHandler(CatalogItems);
        var tool = BuildTools(ordering, catalog).GetOrder;

        var result = await InvokeAsync(tool, 99);

        Assert.AreEqual(error, result.GetProperty("error").GetString());
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.GetProperty("message").GetString()));
        Assert.IsFalse(result.TryGetProperty("items", out _));
        Assert.IsEmpty(catalog.Requests);
    }

    [TestMethod]
    public async Task GetOrderReturnsUnavailableWhenCatalogFails()
    {
        // Without types the model could apply the wrong Return Window, so a Catalog outage is not hidden as nulls.
        var ordering = new FakeHandler(_ => Json(HttpStatusCode.OK, OrderDetailsJson));
        var catalog = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var tool = BuildTools(ordering, catalog).GetOrder;

        var result = await InvokeAsync(tool, 12);

        Assert.AreEqual("unavailable", result.GetProperty("error").GetString());
        Assert.AreEqual("Error accessing catalog.", result.GetProperty("message").GetString());
    }

    [TestMethod]
    public void GetOrderHasTheContractNameParameterNameAndSearchByTypeRule()
    {
        var tools = BuildTools(new FakeHandler(_ => Json(HttpStatusCode.OK, OrderDetailsJson)), CatalogHandler(CatalogItems));
        var tool = tools.GetOrder;

        Assert.AreEqual("get_order", tool.Name);
        var properties = tool.JsonSchema.GetProperty("properties").EnumerateObject().ToList();
        Assert.AreEqual("orderNumber", properties.Single().Name);
        Assert.AreEqual("integer", properties.Single().Value.GetProperty("type").GetString());
        Assert.AreEqual(
            "Gets one of the signed-in Buyer's own AdventureWorks orders by its order number, with today's date (UTC). " +
            "It has the Order Date, status and items; each item has its product id and name, units, unit price, " +
            "and its catalog type and brand (null when the catalog doesn't know the product). " +
            "Some Policy rules, such as Return Windows, depend on an item's type: name the types when searching the Policies.",
            tool.Description);
        CollectionAssert.AreEqual(
            new[] { "search_policies", "list_my_orders", "get_order", "search_catalog" },
            tools.All.Select(t => t.Name).ToArray());
    }

    private static FakeHandler CatalogHandler(string items) => new(request => request.RequestUri!.AbsolutePath switch
    {
        "/api/catalog/items/by" => Json(HttpStatusCode.OK, items),
        "/api/catalog/catalogtypes" => Json(HttpStatusCode.OK, CatalogTypes),
        "/api/catalog/catalogbrands" => Json(HttpStatusCode.OK, CatalogBrands),
        _ => new HttpResponseMessage(HttpStatusCode.NotFound),
    });

    private static AssistantTools BuildTools(FakeHandler ordering, FakeHandler catalog)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        var tools = services.AddAssistantTools();
        tools.OrderingClient.ConfigurePrimaryHttpMessageHandler(() => ordering);
        tools.CatalogClient.ConfigurePrimaryHttpMessageHandler(() => catalog);

        return services.BuildServiceProvider().GetRequiredService<AssistantTools>();
    }

    private async Task<JsonElement> InvokeAsync(AIFunction tool, int orderNumber)
    {
        var result = await tool.InvokeAsync(new AIFunctionArguments { ["orderNumber"] = orderNumber }, TestContext.CancellationToken);
        return result is JsonElement element ? element : JsonSerializer.SerializeToElement(result);
    }
}
