using System.Security.Claims;
using eShop.Basket.API.Grpc;
using eShop.Basket.API.IntegrationEvents.EventHandling;
using eShop.Basket.API.IntegrationEvents.EventHandling.Events;
using eShop.Basket.API.Model;
using eShop.Basket.FunctionalTests;
using eShop.Basket.UnitTests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Grpc.Core;
using BasketItem = eShop.Basket.API.Model.BasketItem;

namespace eShop.Basket.UnitTests;

// BasketService over InMemoryBasketRepository, the fake proven by BasketRepositoryContract.
// Tests assert what the service returns and what it stored (read back through GetBasket),
// never which repository methods were called.
[TestClass]
public class BasketServiceTests
{
    public TestContext TestContext { get; set; } = null!;

    private readonly InMemoryBasketRepository _repository = new();

    private BasketService CreateService() => new(_repository, NullLogger<BasketService>.Instance);

    [TestMethod]
    public async Task GetBasketReturnsEmptyForNoUser()
    {
        await SeedBasketAsync("1", productId: 7, quantity: 2);
        var service = CreateService();
        var context = TestServerCallContext.Create(cancellationToken: TestContext.CancellationToken);
        context.SetUserState("__HttpContext", new DefaultHttpContext());

        var response = await service.GetBasket(new GetBasketRequest(), context);

        Assert.IsEmpty(response.Items);
    }

    [TestMethod]
    public async Task GetBasketReturnsItemsForValidUserId()
    {
        await SeedBasketAsync("1", productId: 7, quantity: 2);
        var service = CreateService();

        var response = await service.GetBasket(new GetBasketRequest(), CreateContext("1"));

        Assert.HasCount(1, response.Items);
        Assert.AreEqual(7, response.Items[0].ProductId);
        Assert.AreEqual(2, response.Items[0].Quantity);
    }

    [TestMethod]
    public async Task GetBasketReturnsEmptyForUnknownUserId()
    {
        await SeedBasketAsync("1", productId: 7, quantity: 2);
        var service = CreateService();

        var response = await service.GetBasket(new GetBasketRequest(), CreateContext("unknown"));

        Assert.IsEmpty(response.Items);
    }

    [TestMethod]
    public async Task UpdateBasketPersistsItemsForAuthenticatedUser()
    {
        var service = CreateService();
        var request = new UpdateBasketRequest();
        request.Items.Add(new eShop.Basket.API.Grpc.BasketItem { ProductId = 42, Quantity = 3 });

        var response = await service.UpdateBasket(request, CreateContext("buyer-1"));

        Assert.HasCount(1, response.Items);
        Assert.AreEqual(42, response.Items[0].ProductId);
        Assert.AreEqual(3, response.Items[0].Quantity);
        var stored = await service.GetBasket(new GetBasketRequest(), CreateContext("buyer-1"));
        Assert.HasCount(1, stored.Items);
        Assert.AreEqual(42, stored.Items[0].ProductId);
        Assert.AreEqual(3, stored.Items[0].Quantity);
    }

    [TestMethod]
    public async Task UpdateBasketRejectsAnonymousUser()
    {
        var service = CreateService();
        var request = new UpdateBasketRequest();
        request.Items.Add(new eShop.Basket.API.Grpc.BasketItem { ProductId = 42, Quantity = 3 });

        var exception = await Assert.ThrowsAsync<RpcException>(() =>
            service.UpdateBasket(request, CreateContext(null!)));

        Assert.AreEqual(StatusCode.Unauthenticated, exception.StatusCode);
    }

    [TestMethod]
    public async Task UpdateBasketReturnsNotFoundWhenRepositoryCannotPersist()
    {
        _repository.RefusesSaves = true;
        var service = CreateService();
        var request = new UpdateBasketRequest();
        request.Items.Add(new eShop.Basket.API.Grpc.BasketItem { ProductId = 42, Quantity = 3 });

        var exception = await Assert.ThrowsAsync<RpcException>(() =>
            service.UpdateBasket(request, CreateContext("buyer-1")));

        Assert.AreEqual(StatusCode.NotFound, exception.StatusCode);
        var stored = await service.GetBasket(new GetBasketRequest(), CreateContext("buyer-1"));
        Assert.IsEmpty(stored.Items);
    }

    [TestMethod]
    public async Task DeleteBasketRemovesAuthenticatedUsersBasket()
    {
        await SeedBasketAsync("buyer-1", productId: 42, quantity: 3);
        await SeedBasketAsync("buyer-2", productId: 7, quantity: 1);
        var service = CreateService();

        await service.DeleteBasket(new DeleteBasketRequest(), CreateContext("buyer-1"));

        var deleted = await service.GetBasket(new GetBasketRequest(), CreateContext("buyer-1"));
        Assert.IsEmpty(deleted.Items);
        var untouched = await service.GetBasket(new GetBasketRequest(), CreateContext("buyer-2"));
        Assert.HasCount(1, untouched.Items);
    }

    [TestMethod]
    public async Task OrderStartedEventRemovesUsersBasket()
    {
        await SeedBasketAsync("buyer-1", productId: 42, quantity: 3);
        await SeedBasketAsync("buyer-2", productId: 7, quantity: 1);
        var handler = new OrderStartedIntegrationEventHandler(
            _repository,
            NullLogger<OrderStartedIntegrationEventHandler>.Instance);

        await handler.Handle(new OrderStartedIntegrationEvent("buyer-1"));

        var service = CreateService();
        var cleared = await service.GetBasket(new GetBasketRequest(), CreateContext("buyer-1"));
        Assert.IsEmpty(cleared.Items);
        var untouched = await service.GetBasket(new GetBasketRequest(), CreateContext("buyer-2"));
        Assert.HasCount(1, untouched.Items);
    }

    private Task<CustomerBasket> SeedBasketAsync(string buyerId, int productId, int quantity) =>
        _repository.UpdateBasketAsync(new CustomerBasket
        {
            BuyerId = buyerId,
            Items = [new BasketItem { Id = $"item-{productId}", ProductId = productId, Quantity = quantity }]
        });

    private TestServerCallContext CreateContext(string userId)
    {
        var context = TestServerCallContext.Create(cancellationToken: TestContext.CancellationToken);
        var httpContext = new DefaultHttpContext();
        if (userId is not null)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", userId)]));
        }
        context.SetUserState("__HttpContext", httpContext);
        return context;
    }
}
