namespace eShop.IntegrationEventLogEF.Services;

public interface IIntegrationEventLogService
{
    /// <summary>
    /// The transaction's unpublished entries, oldest first, with their events deserialized. An entry whose stored
    /// type name matches no loaded integration event type is still returned, with a null
    /// <see cref="IntegrationEventLogEntry.IntegrationEvent"/>.
    /// </summary>
    Task<IEnumerable<IntegrationEventLogEntry>> RetrieveEventLogsPendingToPublishAsync(Guid transactionId);
    Task SaveEventAsync(IntegrationEvent @event, IDbContextTransaction transaction);
    Task MarkEventAsPublishedAsync(Guid eventId);
    Task MarkEventAsInProgressAsync(Guid eventId);
    Task MarkEventAsFailedAsync(Guid eventId);
}
