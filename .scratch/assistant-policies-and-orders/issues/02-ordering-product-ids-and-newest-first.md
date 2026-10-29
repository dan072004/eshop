# 02: Ordering returns product ids and lists orders newest-first

**What to build:** Two additive changes to Ordering.API's read side, so that the order tools don't have to work around missing data:
- The order detail response includes each item's `productId`, taken from the order item already stored in the domain.
- A Buyer's order list comes back newest-first by Order Date, with ties broken by order number descending.

Endpoints, their ownership checks and their 404-for-not-yours behaviour stay unchanged. See the spec's "Ordering.API (additive change)" section.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] The order detail response includes `productId` for every item
- [ ] The Buyer's order list is sorted newest-first, ties by order number descending
- [ ] Functional test over HTTP (`Ordering.FunctionalTests`, auto-authorised user): create two orders and assert the list order by order number
- [ ] Functional test over HTTP: create an order with known product ids and assert those exact `productId` values in the detail response
- [ ] Existing ownership tests still pass unchanged
