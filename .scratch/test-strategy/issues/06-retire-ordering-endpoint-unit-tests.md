# 06: Retire the Ordering endpoint unit tests

**What to build:** Ordering endpoint behaviour is tested only through HTTP. Every remaining case in the endpoint unit-test class either gets a functional equivalent or is shown to be covered already. Then the class, and its mocks of the current user, mediator and queries, is deleted.

**Blocked by:** 02 (Ordering ownership tested through HTTP), for the seed helper

**Status:** done

- [x] Cancelling an Order with a request id succeeds through HTTP (may already be covered by 02's owner-cancel test; if so, note it)
- [x] Shipping a Paid Order succeeds through HTTP, and reading it back shows it as Shipped. The seed helper can produce an Order in the needed status
- [x] Getting one of the Buyer's Orders through HTTP returns that Order's details
- [x] The existing get-all-stored-Orders functional test seeds an Order and asserts it appears in the list, replacing the status-only check and the unused variable
- [x] Bad-request, non-existent-Order and card-type cases are confirmed covered by existing functional tests
- [x] The endpoint unit-test class is deleted

## Comments

**Coverage map for the deleted `OrdersWebApiTest` cases.**

| Deleted unit test | Covered through HTTP by |
|---|---|
| `Cancel_order_with_requestId_success` | `OwnerCanCancelTheirOrder` (ticket 02), already present |
| `Cancel_order_bad_request` | `CancelWithEmptyGuidFails` |
| `Ship_order_with_requestId_success` | `ShippingAPaidOrderMarksItShipped` (new) |
| `Ship_order_bad_request` | `ShipWithEmptyGuidFails` |
| `Ship_order_returns_problem_when_command_fails` | `ShipNonExistentOrderFails` (500) |
| `Get_orders_success` | `GetAllStoredOrdersWorks` (now seeds an Order and finds it in the list) |
| `Get_order_success` | `GettingOwnOrderReturnsItsDetails` (new) |
| `Get_order_fails` | `GetStoredOrdersWithOrderId` (404) |
| `Get_cardTypes_success` | `GetAllOrdersCardType` |
| `Cancel_order_returns_problem_when_command_fails` | Not reachable through HTTP: the ownership check returns 404 for a missing Order, and the cancel handler only returns false for a missing Order. Dropped rather than replaced. |

**Seed helper.** `SeedPaidOrderAsync` is a sibling of `SeedOrderAsync` that walks the seeded Order through the domain methods to Paid, so `SeedOrderAsync` itself is unchanged.

**Test-order fix.** `GetStoredOrdersWithOrderId` asked for Order 1, which a seeded Order owned by the test identity can now be, so it failed when it ran after a seeding test. It now asks for `int.MaxValue`.

**Mutation check.** With the Paid transition removed from the seed, the ship test fails with 500 instead of 200.

The stale "`OrdersWebApiTest` is legacy awaiting removal" clause was removed from CLAUDE.md "## Testing".
