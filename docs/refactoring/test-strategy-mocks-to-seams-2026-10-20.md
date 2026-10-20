# Refactoring: move tests off internal mocks and onto seams (2026-10-20)

Logged after reviewing `tests/` against the `/tdd` skill (mattpocock-skills) while evaluating `/implement-spec`.
Not scheduled yet; this records the findings and the target shape so it can become a spec later.

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
  (e.g. `OrderAggregateTest.order_total_subtracts_item_discounts`).
- **Webhook tests**: `WebhookTests` fakes only the HTTP handler; `WebhookClientTests` uses the real
  `HooksRepository` and a `TestServer`.
- **OrderProcessor / PaymentProcessor tests**: mock `IEventBus`, which is a system boundary (RabbitMQ).

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
   HTTP tests against real Postgres. Generalise `SeedOrderWithDiscountAsync` in `OrderingApiTests` to take an
   owner identity, then:
   - `CancellingAnotherUsersOrderReturnsNotFound`: seed for `other-<guid>`, PUT `/api/orders/cancel`,
     expect 404.
   - `OwnerCanCancelTheirOrder`: seed for `AutoAuthorizeMiddleware.IDENTITY_ID`, cancel, then GET the order
     and expect `Status == "Cancelled"`. Without this, the 404 test could pass because cancel is broken.
   - Check first: cancel raises `OrderCancelledDomainEvent` → integration event; the fixture doesn't start
     RabbitMQ. (Not verified.)
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
  `AutoAuthorizeMiddleware` bypasses auth (not verified);
- keeping domain and fake-backed tests as the fast, Docker-free loop.

Measure before and after with `dotnet test --project tests/Ordering.FunctionalTests` (per-test durations vs
total).

## Open decision

Whether to adopt this strategy or keep the current mock-heavy style. Either way, record it in `CLAUDE.md` so
agents (and `/implement-spec` subagents running `/tdd`) don't each pick their own.
