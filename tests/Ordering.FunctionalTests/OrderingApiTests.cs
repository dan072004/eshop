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
        // Act
        var response = await _httpClient.GetAsync("api/orders", TestContext.Current.CancellationToken);
        var s = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
    public async Task GetStoredOrdersWithOrderId()
    {
        // Act
        var response = await _httpClient.GetAsync("api/orders/1", TestContext.Current.CancellationToken);
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
        var responseData = JsonSerializer.Deserialize<OrderDraftDTO>(s, new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

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
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
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
