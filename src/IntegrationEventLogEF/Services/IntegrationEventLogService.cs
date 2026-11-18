using System.Collections.Concurrent;

namespace eShop.IntegrationEventLogEF.Services;

public class IntegrationEventLogService<TContext> : IIntegrationEventLogService, IDisposable
    where TContext : DbContext
{
    private volatile bool _disposedValue;
    private readonly TContext _context;

    // Only successful lookups are cached, so a type name that resolves to nothing is looked up again next time.
    private static readonly ConcurrentDictionary<string, Type> s_eventTypes = new();

    public IntegrationEventLogService(TContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<IntegrationEventLogEntry>> RetrieveEventLogsPendingToPublishAsync(Guid transactionId)
    {
        var pendingEntries = await _context.Set<IntegrationEventLogEntry>()
            .Where(e => e.TransactionId == transactionId && e.State == EventStateEnum.NotPublished)
            .ToListAsync();

        // Deserialized eagerly, so nothing throws while the caller enumerates the entries.
        return pendingEntries
            .OrderBy(e => e.CreationTime)
            .Select(DeserializeIfTypeResolves)
            .ToList();
    }

    private static IntegrationEventLogEntry DeserializeIfTypeResolves(IntegrationEventLogEntry entry) =>
        ResolveEventType(entry.EventTypeName) is { } eventType ? entry.DeserializeJsonContent(eventType) : entry;

    private static Type ResolveEventType(string eventTypeName)
    {
        if (s_eventTypes.TryGetValue(eventTypeName, out var cachedType))
        {
            return cachedType;
        }

        var eventType = FindLoadedEventType(eventTypeName);
        if (eventType is not null)
        {
            s_eventTypes.TryAdd(eventTypeName, eventType);
        }
        return eventType;
    }

    // Searches every loaded assembly rather than the entry assembly, which is the test host
    // (not the service) when running under WebApplicationFactory.
    private static Type FindLoadedEventType(string eventTypeName) =>
        AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(eventTypeName))
            .FirstOrDefault(type => type is not null && typeof(IntegrationEvent).IsAssignableFrom(type));

    public Task SaveEventAsync(IntegrationEvent @event, IDbContextTransaction transaction)
    {
        if (transaction == null) throw new ArgumentNullException(nameof(transaction));

        var eventLogEntry = new IntegrationEventLogEntry(@event, transaction.TransactionId);

        _context.Database.UseTransaction(transaction.GetDbTransaction());
        _context.Set<IntegrationEventLogEntry>().Add(eventLogEntry);

        return _context.SaveChangesAsync();
    }

    public Task MarkEventAsPublishedAsync(Guid eventId)
    {
        return UpdateEventStatus(eventId, EventStateEnum.Published);
    }

    public Task MarkEventAsInProgressAsync(Guid eventId)
    {
        return UpdateEventStatus(eventId, EventStateEnum.InProgress);
    }

    public Task MarkEventAsFailedAsync(Guid eventId)
    {
        return UpdateEventStatus(eventId, EventStateEnum.PublishedFailed);
    }

    private Task UpdateEventStatus(Guid eventId, EventStateEnum status)
    {
        var eventLogEntry = _context.Set<IntegrationEventLogEntry>().Single(ie => ie.EventId == eventId);
        eventLogEntry.State = status;

        if (status == EventStateEnum.InProgress)
            eventLogEntry.TimesSent++;

        return _context.SaveChangesAsync();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _context.Dispose();
            }


            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
