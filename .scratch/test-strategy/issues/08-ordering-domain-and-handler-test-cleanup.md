# 08: Clean up Ordering domain and command-handler tests

**What to build:** The remaining Ordering unit tests fail when the behaviour they're named after breaks, and each one sits with the behaviour it covers.

**Blocked by:** 01 (Write the testing convention in CLAUDE.md)

**Status:** done

- [x] The create-Order-item domain test asserts the item's units, unit price, discount and product, not just non-null
- [x] The "throws when no buyer id" command-handler test becomes a `Buyer` domain test if no equivalent exists there; otherwise it is deleted
- [x] The "returns false if the Order is not persisted" create-Order handler test is rewritten over a fake Order repository whose save can be made to fail. It no longer uses NSubstitute on our own types. The mediator is no longer mocked: either the handler test needs no mediator or the dependency is a fake
- [x] No NSubstitute on our own types remains in the Ordering unit tests (apart from anything ticket 05 or 06 deletes)

## Comments

- "Throws when no buyer id" was deleted: `BuyerAggregateTest.Create_buyer_item_fail` already covers it.
- The handler takes `IMediator` but never uses it, so the test gives it a real MediatR mediator with nothing registered (`AddMediatR` over the test assembly) instead of a fake.
- New fakes in `tests/Ordering.UnitTests/Application/Fakes.cs`: `FakeOrderRepository` (+ `FakeUnitOfWork` whose save can fail), `FakeOrderingIntegrationEventService`, `FakeIdentityService`. Deviation from the convention: `FakeOrderRepository` has no shared contract test against the real EF `OrderRepository` (that needs Postgres and wasn't in this ticket's scope); it only stores orders.
- `GlobalUsings.cs` still imports NSubstitute because `IdentifiedCommandHandlerTest` and `OrdersWebApiTest` (tickets 05/06) use it; remove the using and the package once both are gone.
