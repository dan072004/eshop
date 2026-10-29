using System.Net;
using System.Text;
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

        Assert.AreEqual(1, result.GetProperty("count").GetInt32());
        var item = result.GetProperty("data").EnumerateArray().Single();
        Assert.AreEqual(42, item.GetProperty("id").GetInt32());
        Assert.AreEqual("Trailblazer Tent", item.GetProperty("name").GetString());
        Assert.AreEqual("A two-person tent for alpine trekking.", item.GetProperty("description").GetString());
        Assert.AreEqual(199.5m, item.GetProperty("price").GetDecimal());
        Assert.AreEqual("product-images/42?api-version=2.0", item.GetProperty("pictureUrl").GetString());
        Assert.AreEqual("Alpine Co", item.GetProperty("catalogBrand").GetProperty("brand").GetString());
        Assert.AreEqual("Tents", item.GetProperty("catalogType").GetProperty("type").GetString());
    }

    [TestMethod]
    public async Task SearchCatalogLeavesPictureUrlOutWhenTheHostSuppliesNone()
    {
        var catalog = new FakeHandler(_ => Json(HttpStatusCode.OK, CatalogPage));
        var tool = BuildTools(catalog).SearchCatalog;

        var result = await InvokeAsync(tool, new() { ["productDescription"] = "tent" });

        var item = result.GetProperty("data").EnumerateArray().Single();
        Assert.AreEqual("Trailblazer Tent", item.GetProperty("name").GetString());
        Assert.IsFalse(item.TryGetProperty("pictureUrl", out _));
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

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }
}
