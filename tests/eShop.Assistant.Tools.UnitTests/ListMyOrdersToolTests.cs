using System.Net;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace eShop.Assistant.Tools.UnitTests;

[TestClass]
public class ListMyOrdersToolTests
{
    // What Ordering.API v1 returns from GET api/orders (OrderSummary, web JSON casing), already newest-first.
    private const string OrderingOrders = """
        [
          { "orderNumber": 12, "date": "2026-10-27T15:04:05Z", "status": "Shipped", "total": 247.5 },
          { "orderNumber": 7, "date": "2026-09-30T09:00:00Z", "status": "Cancelled", "total": 19.99 }
        ]
        """;

    private static readonly DateTimeOffset Now = new(2026, 10, 29, 23, 30, 0, TimeSpan.Zero);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ListMyOrdersReturnsTheOrdersAsOrderingSentThemWithToday()
    {
        var ordering = new FakeHandler(_ => Json(HttpStatusCode.OK, OrderingOrders));
        var tool = BuildTools(ordering).ListMyOrders;

        var result = await InvokeAsync(tool);

        var request = ordering.Requests.Single();
        Assert.AreEqual(HttpMethod.Get, request.Method);
        Assert.AreEqual("ordering-api", request.RequestUri!.Host);
        Assert.AreEqual("/api/orders", request.RequestUri.AbsolutePath.TrimEnd('/'));
        Assert.AreEqual("1.0", System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query)["api-version"]);

        Assert.AreEqual("2026-10-29", result.GetProperty("today").GetString());
        var orders = result.GetProperty("orders").EnumerateArray().ToList();
        Assert.HasCount(2, orders);
        Assert.AreEqual(12, orders[0].GetProperty("orderNumber").GetInt32());
        Assert.AreEqual("2026-10-27T15:04:05Z", orders[0].GetProperty("orderDate").GetString());
        Assert.AreEqual("Shipped", orders[0].GetProperty("status").GetString());
        Assert.AreEqual(247.5m, orders[0].GetProperty("total").GetDecimal());
        Assert.AreEqual(7, orders[1].GetProperty("orderNumber").GetInt32());
        Assert.AreEqual("2026-09-30T09:00:00Z", orders[1].GetProperty("orderDate").GetString());
        Assert.AreEqual("Cancelled", orders[1].GetProperty("status").GetString());
        Assert.AreEqual(19.99m, orders[1].GetProperty("total").GetDecimal());
    }

    [TestMethod]
    public async Task ListMyOrdersReturnsSignInRequiredWhenOrderingSays401()
    {
        var ordering = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var tool = BuildTools(ordering).ListMyOrders;

        var result = await InvokeAsync(tool);

        Assert.AreEqual("sign_in_required", result.GetProperty("error").GetString());
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.GetProperty("message").GetString()));
        Assert.IsFalse(result.TryGetProperty("orders", out _));
    }

    [TestMethod]
    public async Task ListMyOrdersReturnsUnavailableWhenOrderingFails()
    {
        var ordering = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var tool = BuildTools(ordering).ListMyOrders;

        var result = await InvokeAsync(tool);

        Assert.AreEqual("unavailable", result.GetProperty("error").GetString());
        Assert.AreEqual("Error accessing orders.", result.GetProperty("message").GetString());
    }

    [TestMethod]
    public void ListMyOrdersHasTheContractNameAndNoParameters()
    {
        var tools = BuildTools(new FakeHandler(_ => Json(HttpStatusCode.OK, "[]")));
        var tool = tools.ListMyOrders;

        Assert.AreEqual("list_my_orders", tool.Name);
        Assert.IsFalse(tool.JsonSchema.TryGetProperty("properties", out var properties) && properties.EnumerateObject().Any());
        CollectionAssert.Contains(tools.All.ToList(), tool);
    }

    private static AssistantTools BuildTools(FakeHandler ordering)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        var tools = services.AddAssistantTools();
        tools.OrderingClient.ConfigurePrimaryHttpMessageHandler(() => ordering);

        return services.BuildServiceProvider().GetRequiredService<AssistantTools>();
    }

    private async Task<JsonElement> InvokeAsync(AIFunction tool)
    {
        var result = await tool.InvokeAsync(new AIFunctionArguments(), TestContext.CancellationToken);
        return result is JsonElement element ? element : JsonSerializer.SerializeToElement(result);
    }
}
