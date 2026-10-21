# 04: An outbox event whose type can't be resolved fails clearly and is retried

**What to build:** When the transactional outbox reads a pending event whose stored type name matches no loaded type, the failure names the event type and the event, and the lookup isn't remembered, so a later attempt can still succeed. Today `IntegrationEventLogService.ResolveEventType` (`src/IntegrationEventLogEF/Services/IntegrationEventLogService.cs:20`) caches the result with `GetOrAdd` even when it is `null`, for the whole process. The `null` then reaches `JsonSerializer.Deserialize(Content, null)` and throws a generic `ArgumentNullException` while the pending events are enumerated. In Ordering that happens in `TransactionBehavior` after the business change has been committed, so the request fails even though its data was saved.

**Blocked by:** None (can start immediately)

**Status:** needs-triage

- [ ] Only successful lookups are cached; a failed lookup is tried again next time
- [ ] An unresolvable type fails with an exception that names the stored type name and the event id, not an `ArgumentNullException` from the serializer
- [ ] Tested at the `IntegrationEventLogService` seam against a real database: a pending entry with an unknown type name produces that error
- [ ] Decide, and record here, whether one unresolvable event should stop the other pending events in the same transaction from being published, or be marked Failed and skipped

## Comments

- Introduced deliberately in test-strategy ticket 02. That change moved the lookup off `Assembly.GetEntryAssembly()`, the test host under `WebApplicationFactory`, onto all loaded assemblies, and added the cache. It's unlikely in practice, because events are written and read in the same process, so their type is loaded. That's why this was left as a follow-up.
- Not observable through the seam: that a failed lookup is retried and not cached. Showing it would mean loading an assembly at runtime in a test. Check it in review instead.
