# 05: Order de-duplication tested through HTTP

**What to build:** Sending the same create-Order request twice with the same `x-requestid` creates only one Order, proved through the Ordering HTTP API against real Postgres. The de-duplication unit tests mock the mediator and assert nothing about the result, so they are deleted.

**Blocked by:** 02 (Ordering ownership tested through HTTP). It doesn't depend on 02's behaviour, but both edit the Ordering functional tests, and running them one after the other avoids conflicts.

**Status:** done

- [x] A functional test posts the same Order twice with one `x-requestid` and asserts that exactly one new Order exists for the Buyer
- [x] The test can't pass by accident: it checks the Order count before and after, or uses a fresh identity
- [x] Both identified-command-handler unit tests are deleted. The "sends the command when new" case is already covered by the existing add-new-Order functional test (it isn't; see below — the new test covers it)

## Comments

**Unit-test deletion.** The implementer agent's attempt to delete `tests/Ordering.UnitTests/Application/IdentifiedCommandHandlerTest.cs` was refused by the permission system; the user then approved the deletion and the orchestrating session did it.

**Bug found (production, reported not fixed).** `POST api/orders` answers 200 OK even when the create command fails. `IdentifiedCommandHandler` swallows any exception and returns `false`, and `OrdersApi.CreateOrderAsync` returns `Ok` whatever the result. Validation failures therefore look like success to the caller. Tracked as ticket 11.

**The existing `AddNewOrder` test does not prove an Order is created.** Its request fails validation (null address, card security number `"test buyer"`), so no Order is written and it passes only because of the bug above. The ticket's claim that it covers "sends the command when new" is wrong. The new test covers that case instead: it asserts the Buyer's Order count goes up by exactly one.

**How the new test works.** `PostingTheSameOrderTwiceWithOneRequestIdCreatesOneOrder` counts the fixed test identity's Orders through `GET api/orders`, posts a valid Order twice with one `x-requestid`, and asserts the count rose by one. Its helpers (`PostOrderAsync`, `CountMyOrdersAsync`) sit just above `FakeOrderDraftCommand`.

**Mutation check.** With the `ExistAsync` check and the request record write in `IdentifiedCommandHandler` disabled, the test fails with 2 new Orders instead of 1. It passes once they are restored.
