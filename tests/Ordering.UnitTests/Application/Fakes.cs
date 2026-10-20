using eShop.EventBus.Events;
using eShop.Ordering.API.Application.IntegrationEvents;
using eShop.Ordering.Domain.AggregatesModel.OrderAggregate;
using eShop.Ordering.Domain.Seedwork;

namespace eShop.Ordering.UnitTests.Application;

public sealed class FakeOrderRepository : IOrderRepository
{
    private readonly List<Order> _orders = [];

    public FakeUnitOfWork FakeUnitOfWork { get; } = new();

    public IUnitOfWork UnitOfWork => FakeUnitOfWork;

    public IReadOnlyList<Order> Orders => _orders;

    public Order Add(Order order)
    {
        _orders.Add(order);
        return order;
    }

    public void Update(Order order)
    {
    }

    public Task<Order> GetAsync(int orderId) =>
        Task.FromResult(_orders.SingleOrDefault(o => o.Id == orderId));
}

public sealed class FakeUnitOfWork : IUnitOfWork
{
    public bool SaveSucceeds { get; set; } = true;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(SaveSucceeds ? 1 : 0);

    public Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(SaveSucceeds);

    public void Dispose()
    {
    }
}

public sealed class FakeOrderingIntegrationEventService : IOrderingIntegrationEventService
{
    public List<IntegrationEvent> SavedEvents { get; } = [];

    public Task AddAndSaveEventAsync(IntegrationEvent evt)
    {
        SavedEvents.Add(evt);
        return Task.CompletedTask;
    }

    public Task PublishEventsThroughEventBusAsync(Guid transactionId) => Task.CompletedTask;
}

public sealed class FakeIdentityService(string userId) : IIdentityService
{
    public string GetUserIdentity() => userId;

    public string GetUserName() => "fakeName";
}
