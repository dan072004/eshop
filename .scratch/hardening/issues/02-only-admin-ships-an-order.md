# 02: Only an Admin can ship an Order

**What to build:** Marking an Order as Shipped is an Admin action. Today any signed-in Buyer can call `PUT /api/orders/ship` (`src/Ordering.API/Apis/OrdersApi.cs`, mapped at line 12) with any Order number and mark another Buyer's Paid Order as Shipped. That breaks the order lifecycle and blocks cancelling, because a Shipped Order can't be cancelled. The endpoint is protected only by the route group's `RequireAuthorization()` (`src/Ordering.API/Program.cs:22`). Nothing in WebApp or ClientApp calls it.

**Blocked by:** 01 (Ordering functional tests can act as an Admin)

**Status:** needs-triage

- [ ] Identity seeds an `admin` role and an `admin` user in that role (dev password `Pass123$`, like alice and bob), idempotently like the existing user seed
- [ ] Tokens issued to the Ordering API carry the user's roles as `role` claims
- [ ] Ordering has an `Admin` authorization policy that requires the `admin` role, and `PUT /api/orders/ship` requires it
- [ ] Through HTTP: a Buyer shipping a Paid Order gets 403, and the Order is still Paid when read back
- [ ] Through HTTP: an Admin shipping a Paid Order gets 200, and the Order reads back as Shipped (the existing `ShippingAPaidOrderMarksItShipped` test, now acting as an Admin)
- [ ] Sign-in for alice and bob is unchanged

## Comments

- Found by the `security-reviewer` agent while verifying the cancel-order PR (#2), 2026-10-21. It's not a regression: the same code is on upstream `dotnet/eShop`.
- 403, not 404: an Order the Buyer doesn't own already returns 404 on cancel and get, so Order numbers can't be probed. Shipping is a different case, a role the caller doesn't have, and 403 says that.
- Overlaps coupons ticket 04 (on the `coupons` branch), which adds the same role, seeded user and Ordering policy, plus a WebApp admin area. If this lands first, ticket 04 keeps only the WebApp part.
