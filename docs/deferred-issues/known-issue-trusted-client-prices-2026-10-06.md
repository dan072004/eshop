# Known issue: Ordering trusts client-supplied unit prices (2026-10-06)

Logged while implementing coupons ticket 01 (order totals subtract discounts). Tracked here, alongside
the [security review](./security-review-2026-10-03.md), rather than fixed — it is explicitly out of scope
for the Coupons feature (see `.scratch/coupons/spec.md`, "Out of Scope").

## The issue

When a Buyer places an Order, Ordering takes the unit prices straight from the request body and never
re-checks them against Catalog:

- WebApp sends the basket (`CustomerBasket` / `BasketItem`) to Ordering, including each item's `UnitPrice`.
- `CreateOrderCommandHandler` (`src/Ordering.API/Application/Commands/CreateOrderCommandHandler.cs`) maps
  those prices straight onto the Order items via `AddOrderItem`, with no call back to Catalog.
- The Order total — and, once coupons land, the Order Discount — is therefore computed from whatever price
  the client sent.

## Why it matters for coupons

A percentage Coupon computes the Order Discount as `subtotal × percentage / 100`. The subtotal is the sum
of the client-supplied unit prices. A client that inflates a unit price inflates the subtotal and therefore
the Order Discount, so a trusted-price gap turns a cosmetic problem into money off the real total. (It also
already enables buying at an attacker-chosen price; see finding C1 in the security review, which reaches the
same prices through an unauthenticated Catalog write path.)

Coupons rely on these prices being correct but do **not** make the situation worse: they add no new way for a
client to set a price. The dependency is the reason to track the gap now, before more logic leans on it.

## What fixing it would involve

Ordering should treat the client prices as untrusted and re-establish the real prices server-side when an
Order is placed:

- On create-order (and on the order-draft preview), look up each item's current price from Catalog by product
  id instead of trusting `BasketItem.UnitPrice`, and use the looked-up price for the Order item, the subtotal
  and any Order Discount.
- Decide how to handle a price that has changed since the basket was built (reject with a clear message, or
  re-price and show the Buyer the new total) rather than silently charging a stale or forged price.
- This adds a dependency from Ordering to Catalog on the order path, so it needs the usual resilience and
  failure handling, which is why it is a change of its own rather than part of the Coupons work.
