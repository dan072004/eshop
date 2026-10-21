using System.Net;
using System.Text;
using System.Text.Json;
using Asp.Versioning;
using Asp.Versioning.Http;
using eShop.Ordering.API.Application.Commands;
using eShop.Ordering.API.Application.Models;
using eShop.Ordering.API.Application.Queries;
using eShop.Ordering.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using DomainOrder = eShop.Ordering.Domain.AggregatesModel.OrderAggregate.Order;
using DomainAddress = eShop.Ordering.Domain.AggregatesModel.OrderAggregate.Address;
using Buyer = eShop.Ordering.Domain.AggregatesModel.BuyerAggregate.Buyer;

namespace eShop.Ordering.FunctionalTests;

public sealed class OrderingApiTests : IClassFixture<OrderingApiFixture>
{
    private static readonly JsonSerializerOptions CamelCaseJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly WebApplicationFactory<Program> _webApplicationFactory;
    private readonly HttpClient _httpClient;

    public OrderingApiTests(OrderingApiFixture fixture)
    {
        var handler = new ApiVersionHandler(new QueryStringApiVersionWriter(), new ApiVersion(1.0));

        _webApplicationFactory = fixture;
        _httpClient = _webApplicationFactory.CreateDefaultClient(handler);
    }

    [Fact]
    public async Task GetAllStoredOrdersWorks()
    {
        // Arrange
        var orderId = await SeedOrderAsync(ownerIdentity: AutoAuthorizeMiddleware.IDENTITY_ID);

        // Act
        var response = await _httpClient.GetAsync("api/orders", TestContext.Current.CancellationToken);

        // Assert: the seeded Order is in the Buyer's list with its status and total.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var orders = JsonSerializer.Deserialize<List<OrderSummary>>(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            CamelCaseJson);
        var summary = Assert.Single(orders, o => o.OrderNumber == orderId);
        Assert.Equal("Submitted", summary.Status);
        Assert.Equal(10d, summary.Total);
    }

    [Fact]
    public async Task CancelWithEmptyGuidFails()
    {
        // Act
        var content = new StringContent(BuildOrder(), UTF8Encoding.UTF8, "application/json")
        {
            Headers = { { "x-requestid", Guid.Empty.ToString() } }
        };
        var response = await _httpClient.PutAsync("/api/orders/cancel", content, TestContext.Current.CancellationToken);
        var s = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CancelNonExistentOrderFails()
    {
        // Act
        var content = new StringContent(BuildOrder(), UTF8Encoding.UTF8, "application/json")
        {
            Headers = { { "x-requestid", Guid.NewGuid().ToString() } }
        };
        var response = await _httpClient.PutAsync("api/orders/cancel", content, TestContext.Current.CancellationToken);
        var s = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ShipWithEmptyGuidFails()
    {
        // Act
        var content = new StringContent(BuildOrder(), UTF8Encoding.UTF8, "application/json")
        {
            Headers = { { "x-requestid", Guid.Empty.ToString() } }
        };
        var response = await _httpClient.PutAsync("api/orders/ship", content, TestContext.Current.CancellationToken);
        var s = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ShipNonExistentOrderFails()
    {
        // Act
        var content = new StringContent(BuildOrder(), UTF8Encoding.UTF8, "application/json")
        {
            Headers = { { "x-requestid", Guid.NewGuid().ToString() } }
        };
        var response = await _httpClient.PutAsync("api/orders/ship", content, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task GetAllOrdersCardType()
    {
        // Act 1
        var response = await _httpClient.GetAsync("api/orders/cardtypes", TestContext.Current.CancellationToken);
        var s = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ShippingAPaidOrderMarksItShipped()
    {
        // Arrange
        var orderId = await SeedPaidOrderAsync(ownerIdentity: AutoAuthorizeMiddleware.IDENTITY_ID);

        // Act
        var content = new StringContent(JsonSerializer.Serialize(new ShipOrderCommand(orderId)), UTF8Encoding.UTF8, "application/json")
        {
            Headers = { { "x-requestid", Guid.NewGuid().ToString() } }
        };
        var shipResponse = await _httpClient.PutAsync("api/orders/ship", content, TestContext.Current.CancellationToken);

        // Assert: the ship succeeded and the Order now reads back as Shipped.
        Assert.Equal(HttpStatusCode.OK, shipResponse.StatusCode);
        var order = await ReadOrderAsync(orderId);
        Assert.Equal("Shipped", order.Status);
    }

    [Fact]
    public async Task GettingOwnOrderReturnsItsDetails()
    {
        // Arrange
        var orderId = await SeedOrderAsync(ownerIdentity: AutoAuthorizeMiddleware.IDENTITY_ID);

        // Act
        var order = await ReadOrderAsync(orderId);

        // Assert: the details are the ones the seed helper wrote.
        Assert.Equal(orderId, order.OrderNumber);
        Assert.Equal("Submitted", order.Status);
        Assert.Equal("street", order.Street);
        Assert.Equal("city", order.City);
        Assert.Equal(10m, order.Total);
        var item = Assert.Single(order.OrderItems);
        Assert.Equal("Test product", item.ProductName);
        Assert.Equal(1, item.Units);
    }

    [Fact]
    public async Task GetStoredOrdersWithOrderId()
    {
        // Act
        // An Order number no seeded Order can have, so the result doesn't depend on test order.
        var response = await _httpClient.GetAsync($"api/orders/{int.MaxValue}", TestContext.Current.CancellationToken);
        var responseStatus = response.StatusCode;

        // Assert
        Assert.Equal("NotFound", responseStatus.ToString());
    }

    [Fact]
    public async Task AddNewEmptyOrder()
    {
        // Act
        var content = new StringContent(JsonSerializer.Serialize(new Order()), UTF8Encoding.UTF8, "application/json")
        {
            Headers = { { "x-requestid", Guid.Empty.ToString() } }
        };
        var response = await _httpClient.PostAsync("api/orders", content, TestContext.Current.CancellationToken);
        var s = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddNewOrder()
    {
        // Act
        var item = new BasketItem
        {
            Id = "1",
            ProductId = 12,
            ProductName = "Test",
            UnitPrice = 10,
            OldUnitPrice = 9,
            Quantity = 1,
            PictureUrl = null
        };
        var cardExpirationDate = Convert.ToDateTime("2023-12-22T12:34:24.334Z");
        var OrderRequest = new CreateOrderRequest("1", "TestUser", null, null, null, null, null, "XXXXXXXXXXXX0005", "Test User", cardExpirationDate, "test buyer", 1, null, new List<BasketItem> { item });
        var content = new StringContent(JsonSerializer.Serialize(OrderRequest), UTF8Encoding.UTF8, "application/json")
        {
            Headers = { { "x-requestid", Guid.NewGuid().ToString() } }
        };
        var response = await _httpClient.PostAsync("api/orders", content, TestContext.Current.CancellationToken);
        var s = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostDraftOrder()
    {
        // Act
        var item = new BasketItem
        {
            Id = "1",
            ProductId = 12,
            ProductName = "Test",
            UnitPrice = 10,
            OldUnitPrice = 9,
            Quantity = 1,
            PictureUrl = null
        };
        var bodyContent = new CustomerBasket("1", new List<BasketItem> { item });
        var content = new StringContent(JsonSerializer.Serialize(bodyContent), UTF8Encoding.UTF8, "application/json")
        {
            Headers = { { "x-requestid", Guid.NewGuid().ToString() } }
        };
        var response = await _httpClient.PostAsync("api/orders/draft", content, TestContext.Current.CancellationToken);
        var s = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrderDraftSucceeds()
    {
        var payload = FakeOrderDraftCommand();
        var content = new StringContent(JsonSerializer.Serialize(FakeOrderDraftCommand()), UTF8Encoding.UTF8, "application/json")
        {
            Headers = { { "x-requestid", Guid.NewGuid().ToString() } }
        };
        var response = await _httpClient.PostAsync("api/orders/draft", content, TestContext.Current.CancellationToken);

        var s = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var responseData = JsonSerializer.Deserialize<OrderDraftDTO>(s, CamelCaseJson);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(payload.Items.Count(), responseData.OrderItems.Count());
        Assert.Equal(payload.Items.Sum(o => o.Quantity * o.UnitPrice), responseData.Total);
        AssertThatOrderItemsAreTheSameAsRequestPayloadItems(payload, responseData);
    }

    [Fact]
    public async Task CancellingAnotherBuyersOrderReturnsNotFound()
    {
        // Arrange
        var orderId = await SeedOrderAsync(ownerIdentity: $"other-{Guid.NewGuid()}");

        // Act
        var response = await CancelOrderAsync(orderId);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GettingAnotherBuyersOrderReturnsNotFound()
    {
        // Arrange
        var orderId = await SeedOrderAsync(ownerIdentity: $"other-{Guid.NewGuid()}");

        // Act
        var response = await _httpClient.GetAsync($"api/orders/{orderId}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OwnerCanCancelTheirOrder()
    {
        // Arrange
        var orderId = await SeedOrderAsync(ownerIdentity: AutoAuthorizeMiddleware.IDENTITY_ID);

        // Act
        var cancelResponse = await CancelOrderAsync(orderId);

        // Assert: the cancel succeeded and the Order now reads back as Cancelled.
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
        var getResponse = await _httpClient.GetAsync($"api/orders/{orderId}", TestContext.Current.CancellationToken);
        getResponse.EnsureSuccessStatusCode();
        var order = JsonSerializer.Deserialize<Order>(
            await getResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            CamelCaseJson);
        Assert.Equal("Cancelled", order.Status);
    }

    private Task<HttpResponseMessage> CancelOrderAsync(int orderId)
    {
        var content = new StringContent(JsonSerializer.Serialize(new CancelOrderCommand(orderId)), UTF8Encoding.UTF8, "application/json")
        {
            Headers = { { "x-requestid", Guid.NewGuid().ToString() } }
        };
        return _httpClient.PutAsync("api/orders/cancel", content, TestContext.Current.CancellationToken);
    }

    // Writes a Submitted Order owned by the given identity straight to the database and returns its number.
    private async Task<int> SeedOrderAsync(string ownerIdentity)
    {
        using var scope = _webApplicationFactory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderingContext>();

        var buyer = await context.Buyers
            .FirstOrDefaultAsync(b => b.IdentityGuid == ownerIdentity, TestContext.Current.CancellationToken);
        if (buyer is null)
        {
            buyer = new Buyer(ownerIdentity, "Test Buyer");
            context.Buyers.Add(buyer);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var address = new DomainAddress("street", "city", "state", "country", "zipcode");
        var order = new DomainOrder(
            ownerIdentity, "Test Buyer", address,
            cardTypeId: 1, cardNumber: "1234", cardSecurityNumber: "123",
            cardHolderName: "Test Buyer", cardExpiration: DateTime.UtcNow.AddYears(1),
            buyerId: buyer.Id);
        order.AddOrderItem(1, "Test product", 10m, 0m, "pic", 1);

        context.Orders.Add(order);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return order.Id;
    }

    [Fact]
    public async Task PostingTheSameOrderTwiceWithOneRequestIdCreatesOneOrder()
    {
        // Arrange
        var ordersBefore = await CountMyOrdersAsync();
        var requestId = Guid.NewGuid();

        // Act
        var firstResponse = await PostOrderAsync(requestId);
        var secondResponse = await PostOrderAsync(requestId);

        // Assert: both requests succeed, but only one new Order exists for the Buyer.
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(ordersBefore + 1, await CountMyOrdersAsync());
    }

    [Fact]
    public async Task PostingTwoOrdersWithDifferentRequestIdCreatesTwoOrders()
    {
        // Arrange
        var ordersBefore = await CountMyOrdersAsync();
        var requestId = Guid.NewGuid();

        // Act
        var firstResponse = await PostOrderAsync(requestId);
        var secondResponse = await PostOrderAsync(Guid.NewGuid());

        // Assert: a new request id is a new Order, even with identical contents.
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(ordersBefore + 2, await CountMyOrdersAsync());
    }

    // Posts a one-item Order for the fixed test identity with the given x-requestid.
    private Task<HttpResponseMessage> PostOrderAsync(Guid requestId)
    {
        var item = new BasketItem
        {
            Id = "1",
            ProductId = 12,
            ProductName = "Test",
            UnitPrice = 10,
            OldUnitPrice = 9,
            Quantity = 1,
            PictureUrl = null
        };
        // The request must pass validation: the endpoint answers 200 even when the command fails.
        var request = new CreateOrderRequest(
            UserId: AutoAuthorizeMiddleware.IDENTITY_ID, UserName: "TestUser",
            City: "city", Street: "street", State: "state", Country: "country", ZipCode: "zipcode",
            CardNumber: "XXXXXXXXXXXX0005", CardHolderName: "Test User", CardExpiration: DateTime.UtcNow.AddYears(1),
            CardSecurityNumber: "123", CardTypeId: 1, Buyer: null, Items: new List<BasketItem> { item });
        var content = new StringContent(JsonSerializer.Serialize(request), UTF8Encoding.UTF8, "application/json")
        {
            Headers = { { "x-requestid", requestId.ToString() } }
        };
        return _httpClient.PostAsync("api/orders", content, TestContext.Current.CancellationToken);
    }

    // Counts the Orders the API lists for the fixed test identity.
    private async Task<int> CountMyOrdersAsync()
    {
        var response = await _httpClient.GetAsync("api/orders", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        using var orders = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return orders.RootElement.GetArrayLength();
    }

    // Seeds an Order for the given identity and moves it through stock validation to Paid.
    private async Task<int> SeedPaidOrderAsync(string ownerIdentity)
    {
        var orderId = await SeedOrderAsync(ownerIdentity);

        using var scope = _webApplicationFactory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderingContext>();
        var order = await context.Orders.SingleAsync(o => o.Id == orderId, TestContext.Current.CancellationToken);
        order.SetAwaitingValidationStatus();
        order.SetStockConfirmedStatus();
        order.SetPaidStatus();
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return orderId;
    }

    private async Task<Order> ReadOrderAsync(int orderId)
    {
        var response = await _httpClient.GetAsync($"api/orders/{orderId}", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<Order>(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            CamelCaseJson);
    }

    private CreateOrderDraftCommand FakeOrderDraftCommand()
    {
        return new CreateOrderDraftCommand(
            BuyerId: Guid.NewGuid().ToString(),
            new List<BasketItem>()
            {
                new BasketItem()
                {
                    Id = Guid.NewGuid().ToString(),
                    ProductId = 1,
                    ProductName = "Test Product 1",
                    UnitPrice = 10.2m,
                    OldUnitPrice = 9.8m,
                    Quantity = 2,
                    PictureUrl = Guid.NewGuid().ToString(),
                }
            });
    }

    private static void AssertThatOrderItemsAreTheSameAsRequestPayloadItems(CreateOrderDraftCommand payload, OrderDraftDTO responseData)
    {
        // check that OrderItems contain all product Ids from the payload
        var payloadItemsProductIds = payload.Items.Select(x => x.ProductId);
        var orderItemsProductIds = responseData.OrderItems.Select(x => x.ProductId);
        Assert.All(orderItemsProductIds, orderItemProdId => payloadItemsProductIds.Contains(orderItemProdId));
        // TODO: might need to add more asserts in here
    }

    string BuildOrder()
    {
        var order = new
        {
            OrderNumber = "-1"
        };
        return JsonSerializer.Serialize(order);
    }
}
