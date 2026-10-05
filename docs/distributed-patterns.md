# Distributed patterns in eShop

A map of the distributed-system patterns used in this repo and where each one lives. Paths are relative to `src/`.

## Messaging and consistency

| Pattern | What it does here | Files |
|---|---|---|
| Transactional outbox | Integration events are saved in the same database transaction as the business change, then published to RabbitMQ and marked as sent. | `IntegrationEventLogEF/Services/IntegrationEventLogService.cs`, `IntegrationEventLogEF/IntegrationEventLogEntry.cs`, `IntegrationEventLogEF/EventStateEnum.cs`, `Ordering.API/Application/IntegrationEvents/OrderingIntegrationEventService.cs`, `Catalog.API/IntegrationEvents/CatalogIntegrationEventService.cs` |
| Choreographed saga | The order lifecycle has no central coordinator. Each service reacts to the previous service's event: Ordering → OrderProcessor (grace period) → Catalog (stock) → PaymentProcessor → Ordering. | `Ordering.API/Application/IntegrationEvents/EventHandling/*`, `Catalog.API/IntegrationEvents/EventHandling/OrderStatusChangedToAwaitingValidationIntegrationEventHandler.cs`, `PaymentProcessor/IntegrationEvents/EventHandling/OrderStatusChangedToStockConfirmedIntegrationEventHandler.cs` |
| Compensating actions | When stock is rejected or payment fails, the order is cancelled instead of rolling back a distributed transaction. | `Ordering.API/Application/IntegrationEvents/EventHandling/OrderStockRejectedIntegrationEventHandler.cs`, `Ordering.API/Application/IntegrationEvents/EventHandling/OrderPaymentFailedIntegrationEventHandler.cs`, `Ordering.API/Application/Commands/SetStockRejectedOrderStatusCommandHandler.cs` |
| Publish/subscribe event bus | Services depend on an abstract event bus, with RabbitMQ as the implementation. Each subscriber registers with `AddSubscription<TEvent, THandler>()` in its `Extensions.cs`. | `EventBus/Abstractions/IEventBus.cs`, `EventBus/Abstractions/IIntegrationEventHandler.cs`, `EventBusRabbitMQ/RabbitMQEventBus.cs`, `EventBusRabbitMQ/RabbitMqDependencyInjectionExtensions.cs` |
| Eventual consistency between services | Basket clears the user's basket when an order starts. Price changes are pushed to webhook subscribers. | `Basket.API/IntegrationEvents/EventHandling/OrderStartedIntegrationEventHandler.cs`, `Webhooks.API/IntegrationEvents/ProductPriceChangedIntegrationEventHandler.cs` |
| Domain events vs integration events | Domain events stay inside Ordering's transaction and are dispatched just before `SaveChanges`. Integration events cross service boundaries. | `Ordering.Infrastructure/MediatorExtension.cs`, `Ordering.Infrastructure/OrderingContext.cs`, `Ordering.API/Application/DomainEventHandlers/*` |

## Reliability

| Pattern | What it does here | Files |
|---|---|---|
| Idempotent receiver (request de-duplication) | HTTP commands sent with an `x-requestid` header are wrapped in `IdentifiedCommand<T>` and recorded in a `ClientRequest` table, so a repeated request isn't processed twice. Integration event handlers don't go through this path. | `Ordering.API/Application/Commands/IdentifiedCommand.cs`, `Ordering.API/Application/Commands/IdentifiedCommandHandler.cs`, `Ordering.Infrastructure/Idempotency/RequestManager.cs`, `Ordering.Infrastructure/Idempotency/ClientRequest.cs` |
| Retry with backoff (Polly) | Publishing to RabbitMQ is wrapped in a retry pipeline. | `EventBusRabbitMQ/RabbitMQEventBus.cs` (`CreateResiliencePipeline`) |
| Standard HTTP resilience | Every `HttpClient` gets retry, circuit breaker and timeout policies. | `eShop.ServiceDefaults/Extensions.cs` (`AddStandardResilienceHandler`) |
| Resilient transactions | Database transactions run under EF's execution strategy, so a transient failure retries the whole unit of work. | `IntegrationEventLogEF/Utilities/ResilientTransaction.cs`, `Ordering.API/Application/Behaviors/TransactionBehavior.cs` |
| Health checks | Each service exposes `/health` and `/alive`. Aspire's `WaitFor` uses them to order startup. | `eShop.ServiceDefaults/Extensions.cs` (`MapDefaultEndpoints`), `eShop.AppHost/AppHost.cs` |

## Scheduling and time

| Pattern | What it does here | Files |
|---|---|---|
| Polling background worker | A hosted service polls the ordering database for orders whose grace period has expired, then publishes `GracePeriodConfirmed`. | `OrderProcessor/Services/GracePeriodManagerService.cs`, `OrderProcessor/Services/GracePeriodOrdersRepository.cs` |
| HiLo id generation | EF reserves blocks of ids from a database sequence, so new entities get ids without a round trip per insert. Some logic depends on this; see the comment in the buyer domain event handler. | `Ordering.Infrastructure/EntityConfigurations/OrderEntityTypeConfiguration.cs`, `Ordering.Infrastructure/EntityConfigurations/BuyerEntityTypeConfiguration.cs`, `Ordering.API/Application/DomainEventHandlers/ValidateOrAddBuyerAggregateWhenOrderStartedDomainEventHandler.cs` |

## Communication and edge

| Pattern | What it does here | Files |
|---|---|---|
| Backend for frontend / API gateway | `mobile-bff` is a YARP reverse proxy for the MAUI app. Its routes are matched on `api-version`. | `eShop.AppHost/AppHost.cs`, `eShop.AppHost/Extensions.cs` |
| Service discovery | Services call each other by Aspire resource name, for example `https+http://ordering-api`. | `WebApp/Extensions/Extensions.cs`, `eShop.AppHost/AppHost.cs` |
| gRPC for internal synchronous calls | WebApp talks to Basket over gRPC rather than REST. | `Basket.API/Grpc/BasketService.cs`, `Basket.API/Proto/basket.proto` |
| Token relay | The user's access token is forwarded on calls from WebApp to the other services. | `WebApp/Extensions/Extensions.cs` (`AddAuthToken`) |
| Webhooks (outbound push) | Subscribers register callback URLs. Ownership of a URL is checked before it is accepted, and every delivery carries a shared token header (`X-eshop-whtoken`). | `Webhooks.API/Services/WebhooksSender.cs`, `Webhooks.API/Services/GrantUrlTesterService.cs`, `Webhooks.API/Services/WebhooksRetriever.cs` |
| Live push to the UI | WebApp subscribes to order-status events and refreshes the user's open pages. | `WebApp/Services/OrderStatus/OrderStatusNotificationService.cs`, `WebApp/Services/OrderStatus/IntegrationEvents/EventHandling/*` |

## Inside a service (supporting the distributed ones)

| Pattern | Files |
|---|---|
| CQRS: writes go through MediatR commands, reads through query classes | `Ordering.API/Application/Commands/*`, `Ordering.API/Application/Queries/OrderQueries.cs` |
| Pipeline behaviours: logging, validation and transactions as cross-cutting steps | `Ordering.API/Application/Behaviors/LoggingBehavior.cs`, `Ordering.API/Application/Behaviors/ValidatorBehavior.cs`, `Ordering.API/Application/Behaviors/TransactionBehavior.cs` |
| Distributed tracing: OpenTelemetry, with trace context carried through RabbitMQ messages | `eShop.ServiceDefaults/Extensions.cs`, `EventBusRabbitMQ/RabbitMQTelemetry.cs` |

## Reading order

To follow one order end to end, read the outbox first, then the choreographed saga, then the idempotent receiver. Together they give at-least-once delivery without distributed transactions.

Be aware of one gap: `x-requestid` de-duplication covers HTTP commands only. Integration event handlers have no message-level de-duplication, so a redelivered event relies on the `Order` aggregate's status rules to stay harmless.
