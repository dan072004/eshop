using eShop.EventBus.Abstractions;
using eShop.EventBus.Events;
using eShop.IntegrationEventLogEF;
using eShop.IntegrationEventLogEF.Services;
using eShop.Ordering.API.Application.IntegrationEvents;
using eShop.Ordering.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace eShop.Ordering.FunctionalTests;

// Publishes the transactional outbox against the real Ordering database. The event bus is RabbitMQ,
// a boundary we don't own, so it is replaced by one that records what is published.
public sealed class OutboxPublishingTests(OrderingApiFixture fixture) : IClassFixture<OrderingApiFixture>
{
    private const string UnknownEventTypeName = "eShop.Removed.NoLongerLoadedIntegrationEvent";

    [Fact]
    public async Task AnEventWhoseTypeIsUnknownIsMarkedFailedAndTheRestOfItsTransactionIsPublished()
    {
        // Arrange: the unknown event comes first, so the valid one is only published if publishing carries on past it.
        var unknown = new OutboxProbeIntegrationEvent("unknown");
        var valid = new OutboxProbeIntegrationEvent("valid") { CreationDate = unknown.CreationDate.AddSeconds(1) };
        var transactionId = await SaveInOneTransactionAsync(unknown, valid);
        await SetStoredEventTypeNameAsync(unknown.Id, UnknownEventTypeName);
        var eventBus = new RecordingEventBus();
        var logger = new RecordingLogger<OrderingIntegrationEventService>();

        // Act
        await PublishAsync(transactionId, eventBus, logger);

        // Assert: only the valid event reaches the bus, the states read back, and the error names the unknown event.
        Assert.Equal([valid.Id], eventBus.Published.Select(e => e.Id));
        Assert.Equal(
            [(unknown.Id, EventStateEnum.PublishedFailed), (valid.Id, EventStateEnum.Published)],
            await ReadStatesAsync(transactionId));
        var error = Assert.Single(logger.Errors);
        Assert.Contains(UnknownEventTypeName, error);
        Assert.Contains(unknown.Id.ToString(), error);
    }

    private async Task<Guid> SaveInOneTransactionAsync(params IntegrationEvent[] events)
    {
        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderingContext>();
        var eventLog = scope.ServiceProvider.GetRequiredService<IIntegrationEventLogService>();

        return await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            var transaction = await context.BeginTransactionAsync();
            foreach (var @event in events)
            {
                await eventLog.SaveEventAsync(@event, transaction);
            }
            await context.CommitTransactionAsync(transaction);
            return transaction.TransactionId;
        });
    }

    // Stands in for an event written by a version of the service that had a type this one no longer loads.
    private async Task SetStoredEventTypeNameAsync(Guid eventId, string eventTypeName)
    {
        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderingContext>();
        var entry = await context.Set<IntegrationEventLogEntry>()
            .SingleAsync(e => e.EventId == eventId, TestContext.Current.CancellationToken);
        context.Entry(entry).Property(e => e.EventTypeName).CurrentValue = eventTypeName;
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task PublishAsync(Guid transactionId, IEventBus eventBus, ILogger<OrderingIntegrationEventService> logger)
    {
        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderingContext>();
        var eventLog = scope.ServiceProvider.GetRequiredService<IIntegrationEventLogService>();

        await new OrderingIntegrationEventService(eventBus, context, eventLog, logger)
            .PublishEventsThroughEventBusAsync(transactionId);
    }

    private async Task<List<(Guid EventId, EventStateEnum State)>> ReadStatesAsync(Guid transactionId)
    {
        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderingContext>();
        var entries = await context.Set<IntegrationEventLogEntry>()
            .Where(e => e.TransactionId == transactionId)
            .OrderBy(e => e.CreationTime)
            .ToListAsync(TestContext.Current.CancellationToken);
        return entries.Select(e => (e.EventId, e.State)).ToList();
    }

    private sealed record OutboxProbeIntegrationEvent(string Label) : IntegrationEvent;

    private sealed class RecordingEventBus : IEventBus
    {
        public List<IntegrationEvent> Published { get; } = [];

        public Task PublishAsync(IntegrationEvent @event)
        {
            Published.Add(@event);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Errors { get; } = [];

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (logLevel >= LogLevel.Error)
            {
                Errors.Add($"{formatter(state, exception)} {exception?.Message}");
            }
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;
    }
}
