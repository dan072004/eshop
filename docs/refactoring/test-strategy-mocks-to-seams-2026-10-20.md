# Refactoring: move tests off internal mocks and onto seams (2026-10-20)

Logged after reviewing `tests/` against the `/tdd` skill (mattpocock-skills) while evaluating `/implement-spec`.
Not scheduled yet; this records the findings and the target shape so it can become a spec later.
Decisions from a grilling session on 2026-10-20 are under **Decisions** at the end.

## The issue

About half the suite tests behaviour through public interfaces; the other half mocks our own classes and
asserts on how they are called. Those tests break on refactors that keep behaviour unchanged, and some of
them pass without exercising the logic they are named after.

It also matters for agents: `CLAUDE.md` says unit tests "use MSTest.Sdk with NSubstitute", and the nearest
examples an agent finds are the mock-heavy Ordering tests. `/tdd` says the opposite (mock only at system
boundaries), so parallel `/implement-spec` subagents will produce a mix of both styles.

## What is already in good shape

- **Functional tests** (Catalog, Ordering, Webhooks, Basket/Redis): real HTTP or real repository against real
  containers, asserting observable behaviour. `WebhooksApiTests.SubscriptionLifecyclePersistsAndIsScopedToUser`
  and `RedisBasketRepositoryTests` are the reference examples.
- **Domain tests** (`Ordering.UnitTests/Domain`): no mocks, literal expected values
  (e.g. `OrderAggregateTest.when_add_two_times_on_the_same_item_then_the_total_of_order_should_be_the_sum_of_the_two_items`).
- **Webhook tests**: `WebhookTests` fakes only the HTTP handler; `WebhookClientTests` uses the real
  `HooksRepository` and a `TestServer`.
- **PaymentProcessor tests**: mock `IEventBus`, which is a system boundary (RabbitMQ).
- **`CatalogAITests`**: mocks `IWebHostEnvironment`, a framework type, so a boundary.

## What needs refactoring

### Ordering application tests mock internal collaborators

`IMediator`, `IOrderRepository`, `IIdentityService` and `IOrderQueries` are substituted throughout
`tests/Ordering.UnitTests/Application/`.

- `OrdersWebApiTest.Get_orders_success`: the `GetOrdersFromUserAsync` setup uses a different
  `Guid.NewGuid()` from the identity mock, so it never matches; the test passes only because it asserts the
  result type.
- `OrdersWebApiTest.Cancel_order_not_owned_by_user_returns_not_found`: mocks both `GetUserIdentity()` and
  `IsOrderOwnedByUserAsync()`, so it only tests an `if`. The ownership rule actually lives in the EF query
  (`src/Ordering.API/Application/Queries/OrderQueries.cs`, `o.Buyer.IdentityGuid == userId`); a bug there
  (a security bug) would not be caught.
- `IdentifiedCommandHandlerTest.Handler_sends_no_command_when_order_already_exists`: interaction-only, no
  assertion on the result.
- `NewOrderCommandHandlerTest.Handle_throws_exception_when_no_buyerId`: tests the `Buyer` constructor, not
  the handler.

- `IdentifiedCommandHandlerTest` also substitutes `IRequestManager` (ours) alongside `IMediator`.

### OrderProcessor tests mock a repository that holds the rule

`tests/Application.UnitTests/OrderProcessorTests.cs` mocks `IEventBus` (fine) but also
`IGracePeriodOrdersRepository`. The grace-period rule ("Submitted and older than the grace period") is raw
SQL in `src/OrderProcessor/Services/GracePeriodOrdersRepository.cs` and has no test.

### Basket unit tests mock the repository and assert on calls

`tests/Basket.UnitTests/BasketServiceTests.cs` asserts `Received(1).UpdateBasketAsync(...)`;
`DeleteBasketRemovesAuthenticatedUsersBasket` only verifies a call happened. The service's logic (auth check,
request mapping, `NotFound` on failed persist) sits above Redis, so it can be tested with an in-memory fake.

### ClientApp tests check shape, not behaviour

`FilterCommandIsNotNullTest`, `*PropertyIsEmptyWhenViewModelInstantiatedTest` and similar
(`tests/ClientApp.UnitTests/ViewModels/`), and `CatalogServiceTests`, which only tests the built-in fake data
service.

### Near-empty assertions

- `OrderAggregateTest.Create_order_item_success`: asserts only non-null.
- `OrderingApiTests.GetAllStoredOrdersWorks`: asserts only status 200, with an unused `var s`.

## Target strategy

Decide per test by **where the behaviour lives**, not by "unit vs integration":

| Question | How to test |
|---|---|
| Is the behaviour inside the infrastructure call (a query, a Redis script, serialisation)? | Integration test against the real thing, at the HTTP or repository seam |
| Does the infrastructure only store/transport data for logic above it? | Fast test with an in-memory **fake** at that seam |
| Is it a system we don't own (payment, email, clock, event bus)? | Fake or mock at that boundary only |
| No infrastructure at all? | Plain fast test |

Rules:

- Never mock our own classes (`IMediator`, handlers, query/repository interfaces whose logic we're testing).
- A **fake** is a small working implementation (e.g. a `Dictionary`-backed `IBasketRepository`); tests assert
  through the public interface, not with `Received(...)`.
- Every fake has a matching integration test of the real adapter (ideally the same contract test run against
  both), so the fake can't drift. `IBasketRepository` already has the Redis side (`RedisBasketRepositoryTests`).

## Work items

1. **Write the convention down.** Add a testing section to `CLAUDE.md`: the table above, "NSubstitute only at
   external boundaries", and the reference examples. This also gives `/code-review` a standard to check.
2. **Move ownership tests to Ordering functional tests.** Replace
   `Cancel_order_not_owned_by_user_returns_not_found` (and the `Get_order_not_owned...` equivalent) with
   HTTP tests against real Postgres. Add a seed helper to `OrderingApiTests` (writing through `OrderingContext`) that takes an
   owner identity, then:
   - `CancellingAnotherUsersOrderReturnsNotFound`: seed for `other-<guid>`, PUT `/api/orders/cancel`,
     expect 404.
   - `OwnerCanCancelTheirOrder`: seed for `AutoAuthorizeMiddleware.IDENTITY_ID`, cancel, then GET the order
     and expect `Status == "Cancelled"`. Without this, the 404 test could pass because cancel is broken.
   - Cancelling works without RabbitMQ. `OrderCancelledDomainEventHandler` only writes the event to the
     outbox; publishing then fails (no connection) and `OrderingIntegrationEventService` catches it and
     marks the event Failed. The order is still Cancelled; expect a logged publish error, not a test failure.
     A code read first suggested this already worked, but running the test showed otherwise:
     `IntegrationEventLogService` looked up event types in `Assembly.GetEntryAssembly()`, which is the test
     host under `WebApplicationFactory`, so cancelling returned 500. Fixed in ticket 02 by resolving the
     stored type name across loaded assemblies.
3. **Basket: replace NSubstitute with an in-memory `IBasketRepository` fake**; assert via `GetBasket` after
   `UpdateBasket` / `DeleteBasket`. Add a shared contract test run against the fake and Redis.
4. **Ordering application tests:** delete those superseded by item 2 and by existing functional tests; fix or
   remove `Get_orders_success`, `Handle_throws_exception_when_no_buyerId` and the interaction-only
   `IdentifiedCommandHandlerTest` case.
5. **Strengthen near-empty assertions** (`Create_order_item_success`, `GetAllStoredOrdersWorks`) or delete them.
6. **ClientApp:** replace not-null/shape tests with behaviour tests (e.g. filtering changes `Products`), or
   drop them. Lowest priority; not in the CI solution filter.

## Test-run speed

Moving tests to functional projects costs container startup **per fixture**, not per test; each request
against a warm in-process `WebApplicationFactory` + Postgres is cheap. Keep it fast by:

- sharing containers across test classes (xUnit v3 collection or assembly fixtures);
- isolating by unique data per test (fresh buyer ids) instead of resetting the database, so tests can run in
  parallel;
- checking whether `OrderingApiFixture` needs to start Identity API and its Postgres at all, since
  `AutoAuthorizeMiddleware` bypasses auth (the fixture only reads `Identity:Url` from it; whether Ordering
  needs that at startup is not verified);
- keeping domain and fake-backed tests as the fast, Docker-free loop.

Measure before and after with `dotnet test --project tests/Ordering.FunctionalTests` (per-test durations vs
total).

## Decisions (2026-10-20)

- **Adopted, in phases.** First batch: items 1 and 2, plus a recorded baseline of
  `dotnet test --project tests/Ordering.FunctionalTests`. Everything else is a later batch.
- **Fakes vs mocks.** NSubstitute is never used on our own types. Fakes are allowed for our repository
  interfaces whose real adapter only persists. An interface whose adapter holds behaviour (e.g.
  `IOrderQueries` with its ownership filter, `IGracePeriodOrdersRepository` with its SQL rule) is tested
  against the real database; no fake stands in for that behaviour.
- **Where it's written.** A testing section in `CLAUDE.md` only; no ADR (cheap to reverse).
- **`IIdentityService` is not a boundary.** The current user comes through HTTP (`AutoAuthorizeMiddleware`).
  Endpoint behaviour is tested only via functional tests; `OrdersApi` static methods are not unit-tested,
  so `OrdersWebApiTest` goes away.
- **Superseded mock tests are deleted in the same change** that adds their replacement.
- **Basket fake and contract.** The in-memory `IBasketRepository` fake and an abstract xUnit
  `BasketRepositoryContract` live in `Basket.FunctionalTests`, with Redis and in-memory subclasses.
  `Basket.UnitTests` (MSTest) links the fake's source file, so service tests stay Docker-free.
- **De-duplication (`x-requestid`).** Replace the interaction-only `IdentifiedCommandHandlerTest` case with a
  functional test: POST the same order twice with one request id, assert the buyer has one order.
- **Grace-period SQL (later batch).** Integration test for `GracePeriodOrdersRepository` against Postgres
  (orders either side of the grace period and in other statuses); `OrderProcessorTests` uses a fake
  repository instead of NSubstitute.
- **Items 5–6.** Strengthen `Create_order_item_success` to assert the item's values (originally "delete",
  because a coupons test covered it; that test went with the coupons revert in `b957f63`).
  Strengthen `GetAllStoredOrdersWorks` to seed an order and assert it is listed. Delete the ClientApp shape
  tests without replacement until ClientApp is worked on. Delete `Handle_throws_exception_when_no_buyerId`,
  or move it to the domain tests as a `Buyer` constructor test if none exists there.
- Implemented as tickets 01–10 in `.scratch/test-strategy/` on branch `test-strategy-integration` (2026-10-20). Each ticket's Comments record deviations and the bugs found; ticket 11 (create-Order returns 200 on failure) is left for triage.
