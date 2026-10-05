# Spec: Coupons

Status: ready-for-agent

## Problem Statement

The store has no way to run promotions. An Admin who wants to offer "10% off this weekend" or "$5 off for the first 500 orders" cannot create such an offer, cannot limit how often it is used or for how long, and cannot see who used it and when. Buyers have nowhere to enter a code at checkout. On top of that, the store has no notion of an Admin at all: there is no admin role, no admin user and no admin area.

## Solution

Admins get a Coupons area in the web store where they create Coupons with a Coupon Code, a Coupon Value (a percentage or a fixed amount), a Validity Period and a Redemption Limit. Every Coupon allows each Buyer one active Redemption.

At checkout a Buyer types a Coupon Code, clicks Apply and immediately sees the Order Discount and the new total, calculated by the server. When the Buyer places the Order, the Coupon is redeemed in the same step. If the Coupon stopped being valid in the meantime, the Order is rejected with a clear message and the basket is kept.

Every Redemption is recorded with when it happened, on which Order, for which Buyer and for how much. If the Order is later cancelled, the Redemption is released, so the use goes back to the pool while its history is kept. Admins see the full Redemption history for each Coupon. Buyers see the applied Coupon Code and Order Discount on their past Orders.

## User Stories

### Admin access

1. As an Admin, I want to sign in with my own admin account, so that I can manage the store's promotions.
2. As an Admin, I want an admin area that only Admins can reach, so that Buyers cannot see or change promotions.
3. As a Buyer, I want admin pages and admin actions to be refused to me, so that store configuration cannot be changed by customers.
4. As a developer, I want a seeded admin user alongside the existing seeded Buyers, so that I can try the admin area locally without manual setup.

### Creating and managing Coupons

5. As an Admin, I want to create a Coupon with a Coupon Code, so that Buyers have something to type at checkout.
6. As an Admin, I want Coupon Codes to be matched regardless of case, so that a Buyer typing "summer10" gets the same Coupon as "SUMMER10".
7. As an Admin, I want creating a Coupon with a code that any Coupon (past or present) already used to be refused, so that a code always points to exactly one Coupon and its history.
8. As an Admin, I want to choose whether a Coupon takes a percentage or a fixed amount off the Order, so that I can run both kinds of promotion.
9. As an Admin, I want a percentage Coupon Value to be limited to a sensible range (above 0, at most 100), so that I cannot create a Coupon that charges more or gives everything away by mistake.
10. As an Admin, I want a fixed-amount Coupon Value to be greater than zero, so that every Coupon actually gives something.
11. As an Admin, I want to set a Validity Period with a start and an end, so that a promotion runs only when I intend it to.
12. As an Admin, I want to enter and see the Validity Period in my local time, so that I do not have to convert to UTC.
13. As an Admin, I want the end of the Validity Period to be after its start, so that I cannot create a Coupon that can never be redeemed.
14. As an Admin, I want to set a Redemption Limit, so that a promotion has a bounded cost.
15. As an Admin, I want to see a list of all Coupons with their code, value, Validity Period, Redemption Limit, active Redemption count and state (scheduled, active, expired, Deactivated, used up), so that I can see what is running at a glance.
16. As an Admin, I want to extend the end of a Coupon's Validity Period, so that I can prolong a promotion that is doing well.
17. As an Admin, I want to raise a Coupon's Redemption Limit, so that I can let more Buyers use a popular promotion.
18. As an Admin, I want to deactivate a Coupon at any time, so that I can stop a promotion early.
19. As an Admin, I want deactivation to keep existing Redemptions unchanged, so that Orders already placed keep their Order Discount and history.
20. As an Admin, I want to be prevented from changing a Coupon's code or Coupon Value once it has been redeemed, so that the meaning of past Redemptions never changes.
21. As an Admin, I want to be allowed to change any field of a Coupon that has never been redeemed, so that I can fix mistakes before launch.
22. As an Admin, I want to delete a Coupon that has never been redeemed, so that I can remove mistakes entirely.
23. As an Admin, I want deleting a redeemed Coupon to be refused with a hint to deactivate it instead, so that Redemption history is never lost.

### Redemption history

24. As an Admin, I want to see every Redemption of a Coupon with when it was redeemed, so that I know when the promotion was used.
25. As an Admin, I want each Redemption to show the Order and the Buyer, so that I can trace a use to a purchase.
26. As an Admin, I want each Redemption to show the Order Discount actually given, so that I know what the promotion cost.
27. As an Admin, I want Released Redemptions to stay in the history with when they were released, so that I can see uses that were later undone.
28. As an Admin, I want the active Redemption count to exclude Released Redemptions, so that I see the real remaining capacity.

### Applying a Coupon at checkout

29. As a Buyer, I want a field on the Checkout page to enter a Coupon Code, so that I can use a promotion I was given.
30. As a Buyer, I want to click Apply and see the Order Discount and new total before placing the Order, so that I know what I will pay.
31. As a Buyer, I want the preview to be calculated by the server, so that what I see is what the store will charge.
32. As a Buyer, I want a clear message when the code does not exist, so that I can check for typos.
33. As a Buyer, I want a clear message when the Coupon has not started yet, has expired, has been deactivated or is used up, so that I understand why it does not work.
34. As a Buyer, I want a clear message when I have already redeemed this Coupon, so that I know the Per-Buyer Limit applies.
35. As a Buyer, I want to remove an applied Coupon Code before placing the Order, so that I can change my mind.
36. As a Buyer, I want checkout without a Coupon Code to work exactly as before, so that coupons never get in the way.
37. As a Buyer, I want a fixed-amount Coupon worth more than my Order to bring the total to zero rather than below it, so that the result makes sense.
38. As a Buyer, I want a percentage Order Discount rounded down to the cent, so that amounts are always whole cents.

### Placing an Order with a Coupon

39. As a Buyer, I want the Coupon to be redeemed when I place the Order, so that my Order Discount is secured at that moment.
40. As a Buyer, I want my Order to be rejected with a clear message, and my basket kept, if the Coupon stopped being valid between preview and placing the Order, so that I am never charged more than I was shown without being asked.
41. As an Admin, I want two Buyers racing for the last use of a Coupon to never both succeed, so that the Redemption Limit is never exceeded.
42. As an Admin, I want a Buyer's second Order with the same Coupon to be rejected while their first Redemption is active, so that one Buyer cannot use up a promotion.
43. As an Admin, I want the Buyer on a Redemption to be the signed-in user, not a value the client sends, so that the Per-Buyer Limit cannot be bypassed.
44. As a Buyer, I want to see the Coupon Code and Order Discount on my past Orders, so that I can check what I saved.
45. As a Buyer, I want my Order totals everywhere to reflect any discount, so that the numbers are consistent.

### Cancellation and release

46. As a Buyer, I want cancelling my Order to release its Redemption, so that I can use the Coupon again on a new Order.
47. As a Buyer, I want an Order cancelled because of missing stock to release its Redemption, so that a store-side problem does not cost me my Coupon.
48. As a Buyer, I want an Order cancelled because payment failed to release its Redemption, so that I can retry with the Coupon.
49. As an Admin, I want a released use to count again toward the Redemption Limit's remaining capacity, so that cancelled Orders do not burn promotion budget.
50. As an Admin, I want Redemptions on Paid or Shipped Orders to be permanent, so that completed purchases always count.

## Implementation Decisions

### Ownership and modules

- Coupons live in the Ordering service as a new aggregate (ADR 0001). No new service, no new database. Redeeming a Coupon and placing an Order happen in one database transaction.
- New `Coupon` aggregate in the Ordering domain. Its root owns the Coupon Code, Coupon Value (kind + amount), Validity Period, Redemption Limit, deactivation state and its collection of Redemptions. `Redemption` is an entity inside the Coupon aggregate.
- The Coupon aggregate is a deep module: its public interface is small (create, check whether it can be redeemed by a Buyer at a moment for a subtotal and get the Order Discount, redeem, release, extend, raise limit, deactivate, edit while unredeemed) and it hides all rule checking. Every method that depends on time takes "now" as an argument, so the rules are deterministic and testable.
- Rule checks produce a specific reason when a Coupon cannot be redeemed: unknown code, not started, expired, Deactivated, Redemption Limit reached, Per-Buyer Limit reached. These reasons drive both the preview messages and the order-rejection message.
- New repository for Coupons in Ordering infrastructure, following the existing repository and unit-of-work pattern.

### Schema

- New tables in the Ordering database for Coupons and Redemptions, added through an EF Core migration run by Ordering.API like the existing ones.
- Coupon: id, code (stored as entered, with a unique index on its normalised upper-case form), value kind (percentage or fixed amount), value amount, validity start (UTC), validity end (UTC), Redemption Limit, deactivated-at (nullable), created-at, and a concurrency token.
- Redemption: id, coupon id, order id, buyer id, redeemed-at (UTC), Order Discount amount, released-at (nullable).
- Order gains two nullable fields: the applied Coupon Code and the Order Discount amount.

### Concurrency

- The Redemption Limit and Per-Buyer Limit are enforced inside the Coupon aggregate and protected by the Coupon's optimistic concurrency token. When two Orders race for the last use, one save fails with a concurrency conflict, and that request is rejected as "used up" (after reloading the Coupon and re-checking), never silently accepted.

### Order and totals

- The Order Discount is recorded at Order level. The existing per-item discount on Order items keeps its current meaning and is not used by Coupons.
- Order total = sum of (units × unit price − item discount) − Order Discount, never below zero. Both the single-Order total and the Buyer's Orders list total are corrected to this formula (today both ignore discounts).
- Percentage Order Discount = subtotal × percentage / 100, rounded down to the cent. A fixed-amount Order Discount is capped at the subtotal. Single currency.

### Placing an Order

- The create-order request gains an optional Coupon Code. Absent or empty means today's behaviour exactly.
- The Buyer used for the Per-Buyer Limit and stored on the Redemption is the authenticated user's identity, never the user id in the request body.
- When a Coupon Code is present, the create-order command handler loads the Coupon, asks it to redeem for this Buyer, Order and subtotal at the current time, records the Coupon Code and Order Discount on the Order, and saves both aggregates in the same transaction.
- If the Coupon cannot be redeemed, the endpoint returns a problem response with the specific reason and no Order is created. WebApp shows the reason and keeps the basket.
- The existing de-duplication by request id (identified commands) still applies, so a retried request does not create a second Redemption.

### Preview

- The existing order-draft endpoint accepts an optional Coupon Code. It never redeems: it returns the subtotal, the Order Discount, the total, and, if the Coupon cannot be redeemed, the reason.
- The preview applies the same Per-Buyer Limit, again using the authenticated user's identity.

### Release on cancellation

- Every transition of an Order to Cancelled raises the Order-cancelled domain event. This includes the stock-rejected path, which today cancels silently, as well as Buyer cancellation and payment failure (which already raise it).
- A new handler for the Order-cancelled domain event releases the Order's active Redemption, if any, by setting its released-at. It runs in the same transaction as the cancellation.
- Released Redemptions do not count toward the Redemption Limit or the Per-Buyer Limit.

### Admin API (Ordering)

- A new group of admin coupon endpoints in Ordering, protected by an `Admin` authorization policy that requires the `admin` role:
  - list Coupons with their state and active Redemption count
  - get one Coupon
  - create a Coupon
  - update a Coupon (code and value only while unredeemed; extend the end and raise the limit at any time)
  - deactivate a Coupon
  - delete a Coupon (only if it has never been redeemed; otherwise a conflict response that suggests deactivating)
  - list a Coupon's Redemptions
- Admin writes follow the existing command pattern (MediatR commands, FluentValidation validators, the transaction behaviour). Admin endpoints check the role; Coupon commands themselves do not, consistent with the rule that per-user checks live in endpoints.
- Validation: code required, trimmed, of reasonable length, unique (ignoring case) across all Coupons; percentage above 0 and at most 100; fixed amount above 0; end after start; Redemption Limit at least 1 and, when lowered, never below the active Redemption count (lowering is allowed only while unredeemed, as part of a full edit).

### Identity

- Seed an `admin` role and an `admin` user (dev password `Pass123$`, like the seeded Buyers) and add the user to the role.
- The profile service includes the user's roles as `role` claims in the tokens issued to WebApp and the APIs.

### WebApp

- WebApp gets an `Admin` authorization policy based on the `role` claim.
- Admin pages under `/admin/coupons`: list, create, edit, deactivate and delete, and a per-Coupon Redemption history page. They are static SSR pages using enhanced forms, like the rest of WebApp, and the existing design tokens.
- A link to the admin area appears only for Admins.
- Checkout page: a Coupon Code field with Apply and Remove, showing subtotal, Order Discount and total from the draft endpoint, and passing the Coupon Code when the Order is placed. Errors appear next to the field.
- The Buyer's Orders page shows the Coupon Code and Order Discount on Orders that have one.
- The WebApp clients for Ordering gain the admin and coupon calls. The existing token forwarding carries the role claim.

### Compatibility

- The Coupon Code is optional on every API, so the MAUI apps and existing callers keep working unchanged.

## Testing Decisions

- Good tests here exercise behaviour through public interfaces: HTTP requests and responses for the API, and the Coupon and Order aggregates' public methods for domain rules. They do not assert on EF entities, repository calls or handler internals.
- **Main seam: Ordering.API over HTTP**, using the existing functional-test fixture (WebApplicationFactory with a real Postgres via Aspire). The existing auto-authorize test middleware is extended so each test can choose the user id and whether the user has the `admin` role (for example via a test-only request header), so tests can act as an Admin and as several different Buyers. Covered here:
  - admin coupon endpoints: create, list, get, update rules (including the lock once redeemed), deactivate, delete (allowed vs refused), Redemption history; non-admin requests refused
  - duplicate code (ignoring case) refused
  - draft preview with no code, a valid code (percentage and fixed amount), and each invalid reason
  - placing an Order with a valid Coupon records the Coupon Code and Order Discount, and creates a Redemption with the authenticated Buyer
  - Redemption Limit reached and Per-Buyer Limit reached reject the Order
  - a Buyer cancelling an Order releases the Redemption, and the same Buyer can then redeem again
  - the Buyer's Orders list and single-Order totals reflect the Order Discount
  - Validity Periods in these tests are clearly in the past, present or future relative to now; no fake clock is introduced at this level
- **Domain seam: Ordering unit tests** (MSTest + the existing builders), only for what HTTP cannot reach cleanly:
  - the stock-rejected cancellation now raises the Order-cancelled domain event (existing Order aggregate tests)
  - Coupon aggregate rules with a fixed "now": start included, end excluded; rounding down to the cent; fixed amount capped at the subtotal; edit locking after the first Redemption; release frees capacity
- **End-to-end: one Playwright journey** in the existing e2e suite: the seeded admin signs in, creates a Coupon, then alice applies it at checkout, sees the discounted total and places the Order. This is the only test of the Identity role claim, the WebApp `Admin` policy and the new pages.
- Prior art: the existing Ordering functional tests (including the order-draft tests), the Order and Buyer aggregate unit tests, and the existing Playwright journeys that log in with the seeded users.

## Out of Scope

- Securing the existing admin-only operations (Catalog item create/update/delete, shipping an Order). The new `Admin` policy makes this easy later, but it is a separate change.
- Checking client-supplied unit prices against Catalog. Today Ordering trusts the prices WebApp sends, which also affects percentage Coupons. This is logged as a known issue in the docs folder alongside the earlier security review, not fixed here.
- Coupons limited to specific products, brands or types, stacking several Coupons on one Order, automatic promotions without a code, and single-use generated codes.
- Coupon support in the MAUI ClientApp and HybridApp.
- Reporting beyond the per-Coupon Redemption history (dashboards, exports).
- Multiple currencies.

## Further Notes

- Vocabulary follows the root glossary (Coupon, Coupon Code, Coupon Value, Validity Period, Redemption Limit, Per-Buyer Limit, Redemption, Released Redemption, Order Discount, Deactivated Coupon, Admin, Buyer).
- Ownership decision: ADR 0001 (Coupons live in the Ordering service).
- Making the stock-rejected cancellation raise the Order-cancelled domain event also changes its other side effects (the existing handler for that event publishes the order-status-changed-to-cancelled integration event, which WebApp uses to refresh the Buyer's Orders page live). That is desirable, since today a stock-rejected Order never triggers that refresh, but it should be called out in the PR.
- The known issue about trusted client prices should be written up as part of this work so it is not forgotten.
