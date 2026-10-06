# 01: Order totals subtract discounts

**What to build:** A Buyer's Order total, both on a single Order and in the Buyer's Orders list, is the sum of units × unit price minus each item's discount, never below zero. Today both totals ignore discounts. The same formula will later subtract the Order Discount (ticket 07). Also write up the known issue that Ordering trusts the unit prices sent by the client, so that it is tracked before Coupons start depending on those prices.

**Blocked by:** None (can start immediately)

**Status:** done

- [x] The single-Order total subtracts item discounts
- [x] The Orders-list total uses the same formula, so both totals always agree for the same Order
- [x] A total can never go below zero
- [x] An Order aggregate unit test covers the total with and without item discounts
- [x] An Ordering functional test places an Order whose items have a discount and checks both totals through the API
- [x] A known-issue note in the docs folder, next to the existing security review, explains that client-supplied unit prices are trusted, why that matters for percentage Coupons, and what fixing it would involve (re-checking prices with Catalog)

## Comments

Done in this iteration:

- `Order.GetTotal()` now returns `Math.Max(0, Σ(units × unit price − item discount))` so the single-Order
  total subtracts item discounts and never goes below zero.
- `OrderQueries.GetOrdersFromUserAsync` computes the same `Σ(unitPrice × units − discount)` in SQL and clamps
  it at zero in memory, so the Orders-list total always agrees with the single-Order total for the same Order.
- Unit tests added to `OrderAggregateTest`: `order_total_subtracts_item_discounts` (with and without discount)
  and `order_total_is_never_below_zero`.
- Functional test `OrderTotalsSubtractItemDiscountsAndAgree` added: seeds an Order for the auto-authorized
  buyer with a discounted item (3 × 10.00 − 4.50 = 25.50) via `OrderingContext`, then reads both the
  single-Order (`GET api/orders/{id}`) and list (`GET api/orders`) totals and asserts they equal 25.50 and
  each other.
- Known issue written up in `docs/known-issue-trusted-client-prices-2026-10-06.md` next to the security
  review: Ordering trusts client-supplied unit prices, why that matters for percentage Coupons, and that a
  fix means re-checking prices with Catalog on create-order and draft.

**Note:** the functional test was NOT run — the sandbox has no Docker, and Ordering.FunctionalTests starts
real Postgres/Identity containers. It compiles as part of `dotnet build eShop.Web.slnf`. It still needs to be
run on a host with a container runtime. Build is clean (0 warnings) and all 47 Ordering unit tests pass.
