using eShop.Ordering.API.Application.IntegrationEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace eShop.Ordering.UnitTests.Application;

[TestClass]
public class NewOrderRequestHandlerTest
{
    [TestMethod]
    public async Task Handle_return_false_if_order_is_not_persisted()
    {
        var orderRepository = new FakeOrderRepository();
        orderRepository.FakeUnitOfWork.SaveSucceeds = false;
        var handler = new CreateOrderCommandHandler(
            UnusedMediator(),
            new FakeOrderingIntegrationEventService(),
            orderRepository,
            new FakeIdentityService("1234"),
            NullLogger<CreateOrderCommandHandler>.Instance);

        var result = await handler.Handle(CreateOrderCommandForBuyer("1234"), CancellationToken.None);

        Assert.IsFalse(result);
    }

    // The handler requires a mediator but never sends through it, so a real one with no handlers stands in.
    private static IMediator UnusedMediator() =>
        new ServiceCollection()
            .AddLogging()
            .AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<NewOrderRequestHandlerTest>())
            .BuildServiceProvider()
            .GetRequiredService<IMediator>();

    private static CreateOrderCommand CreateOrderCommandForBuyer(string userId) =>
        new(
            new List<BasketItem>(),
            userId: userId,
            userName: "fakeName",
            city: "city",
            street: "street",
            state: "state",
            country: "country",
            zipcode: "zipcode",
            cardNumber: "1234",
            cardExpiration: DateTime.UtcNow.AddYears(1),
            cardSecurityNumber: "123",
            cardHolderName: "XXX",
            cardTypeId: 1);
}
