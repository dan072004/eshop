# 01: Order totals subtract discounts

**What to build:** A Buyer's Order total, both on a single Order and in the Buyer's Orders list, is the sum of units × unit price minus each item's discount, never below zero. Today both totals ignore discounts. The same formula will later subtract the Order Discount (ticket 07). Also write up the known issue that Ordering trusts the unit prices sent by the client, so that it is tracked before Coupons start depending on those prices.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] The single-Order total subtracts item discounts
- [ ] The Orders-list total uses the same formula, so both totals always agree for the same Order
- [ ] A total can never go below zero
- [ ] An Order aggregate unit test covers the total with and without item discounts
- [ ] An Ordering functional test places an Order whose items have a discount and checks both totals through the API
- [ ] A known-issue note in the docs folder, next to the existing security review, explains that client-supplied unit prices are trusted, why that matters for percentage Coupons, and what fixing it would involve (re-checking prices with Catalog)
