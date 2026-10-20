using eShop.EventBus.Abstractions;
using eShop.OrderProcessor;
using eShop.OrderProcessor.Events;
using eShop.OrderProcessor.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace eShop.Application.UnitTests;

[TestClass]
public class OrderProcessorTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task PublishesGracePeriodConfirmationForEveryEligibleOrder()
    {
        var repository = new FakeGracePeriodOrdersRepository();
        repository.AddSubmittedOrder(12, placedAgo: TimeSpan.FromMinutes(2));
        repository.AddSubmittedOrder(34, placedAgo: TimeSpan.FromMinutes(5));
        repository.AddSubmittedOrder(56, placedAgo: TimeSpan.FromSeconds(30));
        var eventBus = Substitute.For<IEventBus>();
        var service = new GracePeriodManagerService(
            Options.Create(new BackgroundTaskOptions
            {
                GracePeriodTime = 1,
                CheckUpdateTime = 30
            }),
            eventBus,
            NullLogger<GracePeriodManagerService>.Instance,
            repository);

        await service.CheckConfirmedGracePeriodOrders(TestContext.CancellationToken);

        await eventBus.Received(1).PublishAsync(
            Arg.Is<GracePeriodConfirmedIntegrationEvent>(e => e.OrderId == 12));
        await eventBus.Received(1).PublishAsync(
            Arg.Is<GracePeriodConfirmedIntegrationEvent>(e => e.OrderId == 34));
        await eventBus.DidNotReceive().PublishAsync(
            Arg.Is<GracePeriodConfirmedIntegrationEvent>(e => e.OrderId == 56));
    }

    /// <summary>
    /// In-memory stand-in for the Postgres grace-period repository: holds Submitted Orders
    /// with their age and returns those at least as old as the requested grace period.
    /// The real SQL rule is covered against Postgres in OrderProcessor.FunctionalTests.
    /// </summary>
    private sealed class FakeGracePeriodOrdersRepository : IGracePeriodOrdersRepository
    {
        private readonly Dictionary<int, TimeSpan> _submittedOrderAges = [];

        public void AddSubmittedOrder(int orderId, TimeSpan placedAgo) => _submittedOrderAges[orderId] = placedAgo;

        public ValueTask<List<int>> GetConfirmedGracePeriodOrdersAsync(TimeSpan gracePeriod, CancellationToken cancellationToken) =>
            ValueTask.FromResult(_submittedOrderAges
                .Where(order => order.Value >= gracePeriod)
                .Select(order => order.Key)
                .ToList());
    }
}
