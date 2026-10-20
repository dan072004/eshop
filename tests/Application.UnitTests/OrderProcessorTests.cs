using eShop.EventBus.Abstractions;
using eShop.EventBus.Events;
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
    public async Task PublishesGracePeriodConfirmationForEveryOrderPastTheGracePeriod()
    {
        var repository = new StubGracePeriodOrdersRepository(12, 34);
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

        Assert.AreEqual(TimeSpan.FromMinutes(1), repository.RequestedGracePeriod);
        await eventBus.Received(1).PublishAsync(
            Arg.Is<GracePeriodConfirmedIntegrationEvent>(e => e.OrderId == 12));
        await eventBus.Received(1).PublishAsync(
            Arg.Is<GracePeriodConfirmedIntegrationEvent>(e => e.OrderId == 34));
        await eventBus.Received(2).PublishAsync(Arg.Any<IntegrationEvent>());
    }

    /// <summary>
    /// Canned-answer stub for the grace-period query port: returns the configured Order ids and
    /// records the grace period it was asked for. The real SQL rule is covered against Postgres
    /// in OrderProcessor.FunctionalTests.
    /// </summary>
    private sealed class StubGracePeriodOrdersRepository(params int[] orderIds) : IGracePeriodOrdersRepository
    {
        public TimeSpan? RequestedGracePeriod { get; private set; }

        public ValueTask<List<int>> GetConfirmedGracePeriodOrdersAsync(TimeSpan gracePeriod, CancellationToken cancellationToken)
        {
            RequestedGracePeriod = gracePeriod;
            return ValueTask.FromResult(orderIds.ToList());
        }
    }
}
