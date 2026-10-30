using System.Net;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace eShop.Assistant.Tools.UnitTests;

[TestClass]
public class SearchCatalogToolTests
{
    // What Catalog.API v2 returns from items/withsemanticrelevance (PaginatedItems<CatalogItem>, web JSON casing).
    private const string CatalogPage = """
        {
          "pageIndex": 0,
          "pageSize": 8,
          "count": 1,
          "data": [
            {
              "id": 42,
              "name": "Trailblazer Tent",
              "description": "A two-person tent for alpine trekking.",
              "price": 199.5,
              "pictureFileName": "42.webp",
              "catalogTypeId": 3,
              "catalogType": { "id": 3, "type": "Tents" },
              "catalogBrandId": 7,
              "catalogBrand": { "id": 7, "brand": "Alpine Co" },
              "availableStock": 100,
              "restockThreshold": 10,
              "maxStockThreshold": 200,
              "onReorder": false
            }
          ]
        }
        """;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task SearchCatalogReturnsTheMatchingItemsWithTheHostsPictureUrl()
    {
        var catalog = new FakeHandler(_ => Json(HttpStatusCode.OK, CatalogPage));
        var tool = BuildTools(catalog, pictureUrl: id => $"product-images/{id}?api-version=2.0").SearchCatalog;

        var result = await InvokeAsync(tool, new() { ["productDescription"] = "tent for trekking" });

        var request = catalog.Requests.Single();
        Assert.AreEqual("/api/catalog/items/withsemanticrelevance", request.RequestUri!.AbsolutePath);
        var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);
        Assert.AreEqual("tent for trekking", query["text"]);
        Assert.AreEqual("0", query["pageIndex"]);
        Assert.AreEqual("8", query["pageSize"]);
        Assert.AreEqual("2.0", query["api-version"]);

        var expected = JsonElement.Parse("""
            {
              "pageIndex": 0,
              "pageSize": 8,
              "count": 1,
              "data": [
                {
                  "id": 42,
                  "name": "Trailblazer Tent",
                  "description": "A two-person tent for alpine trekking.",
                  "price": 199.5,
                  "pictureUrl": "product-images/42?api-version=2.0",
                  "catalogBrandId": 7,
                  "catalogBrand": { "id": 7, "brand": "Alpine Co" },
                  "catalogTypeId": 3,
                  "catalogType": { "id": 3, "type": "Tents" }
                }
              ]
            }
            """);
        Assert.IsTrue(JsonElement.DeepEquals(expected, result), result.GetRawText());
    }

    [TestMethod]
    public async Task SearchCatalogLeavesPictureUrlOutWhenTheHostSuppliesNone()
    {
        var catalog = new FakeHandler(_ => Json(HttpStatusCode.OK, CatalogPage));
        var tool = BuildTools(catalog).SearchCatalog;

        var result = await InvokeAsync(tool, new() { ["productDescription"] = "tent" });

        var expected = JsonElement.Parse("""
            {
              "id": 42,
              "name": "Trailblazer Tent",
              "description": "A two-person tent for alpine trekking.",
              "price": 199.5,
              "catalogBrandId": 7,
              "catalogBrand": { "id": 7, "brand": "Alpine Co" },
              "catalogTypeId": 3,
              "catalogType": { "id": 3, "type": "Tents" }
            }
            """);
        var item = result.GetProperty("data").EnumerateArray().Single();
        Assert.IsTrue(JsonElement.DeepEquals(expected, item), item.GetRawText());
    }

    [TestMethod]
    public async Task SearchCatalogReturnsUnavailableWhenCatalogFails()
    {
        var catalog = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var tool = BuildTools(catalog).SearchCatalog;

        var result = await InvokeAsync(tool, new() { ["productDescription"] = "tent" });

        Assert.AreEqual("unavailable", result.GetProperty("error").GetString());
        Assert.AreEqual("Error accessing catalog.", result.GetProperty("message").GetString());
    }

    [TestMethod]
    [DataRow("timeout")]
    [DataRow("invalid-json")]
    public async Task SearchCatalogReturnsUnavailableForAnyOtherFailure(string failure)
    {
        var catalog = new FakeHandler(_ => failure == "timeout"
            ? throw new TaskCanceledException("The request timed out.")
            : Json(HttpStatusCode.OK, "<html>not json</html>"));
        var tool = BuildTools(catalog).SearchCatalog;

        var result = await InvokeAsync(tool, new() { ["productDescription"] = "tent" });

        Assert.AreEqual("unavailable", result.GetProperty("error").GetString());
        Assert.AreEqual("Error accessing catalog.", result.GetProperty("message").GetString());
    }

    [TestMethod]
    public void SearchCatalogHasTheContractNameAndParameter()
    {
        var tool = BuildTools(new FakeHandler(_ => Json(HttpStatusCode.OK, CatalogPage))).SearchCatalog;

        Assert.AreEqual("search_catalog", tool.Name);
        var properties = tool.JsonSchema.GetProperty("properties");
        Assert.AreEqual("productDescription", properties.EnumerateObject().Single().Name);
        Assert.AreEqual("productDescription", tool.JsonSchema.GetProperty("required").EnumerateArray().Single().GetString());
    }

    private static AssistantTools BuildTools(FakeHandler catalog, Func<int, string>? pictureUrl = null)
    {
        var services = new ServiceCollection().AddLogging();
        var tools = services.AddAssistantTools();
        tools.CatalogClient.ConfigurePrimaryHttpMessageHandler(() => catalog);
        if (pictureUrl is not null)
        {
            tools.WithProductPictureUrl(_ => new ProductPictureUrl(pictureUrl));
        }

        return services.BuildServiceProvider().GetRequiredService<AssistantTools>();
    }

    private async Task<JsonElement> InvokeAsync(AIFunction tool, AIFunctionArguments arguments)
    {
        var result = await tool.InvokeAsync(arguments, TestContext.CancellationToken);
        return result is JsonElement element ? element : JsonSerializer.SerializeToElement(result);
    }
}
