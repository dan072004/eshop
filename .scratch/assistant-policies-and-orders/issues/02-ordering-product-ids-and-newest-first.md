# 02: Ordering returns product ids and lists orders newest-first

**What to build:** Two additive changes to Ordering.API's read side, so that the order tools don't have to work around missing data:
- The order detail response includes each item's `productId`, taken from the order item already stored in the domain.
- A Buyer's order list comes back newest-first by Order Date, with ties broken by order number descending.

Endpoints, their ownership checks and their 404-for-not-yours behaviour stay unchanged. See the spec's "Ordering.API (additive change)" section.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [x] The order detail response includes `productId` for every item
- [x] The Buyer's order list is sorted newest-first, ties by order number descending
- [x] Functional test over HTTP (`Ordering.FunctionalTests`, auto-authorised user): create two orders and assert the list order by order number
- [x] Functional test over HTTP: create an order with known product ids and assert those exact `productId` values in the detail response
- [x] Existing ownership tests still pass unchanged

## Comments

- `GET api/orders/{id}` items now carry `productId` (`Orderitem.ProductId`, an `int`, serialised camelCase). `GET api/orders` is sorted in `OrderQueries.GetOrdersFromUserAsync` by `OrderDate` descending, then `Id` descending, so the tools can pass the order through unchanged.
- Tests: `OrderDetailsIncludeEachItemsProductId`, `OrderListIsNewestFirstByOrderDate` (lower order number given the later date) and `OrdersWithTheSameDateAreListedByOrderNumberDescending`. The date-only test was already green before the change, because unsorted Postgres output happened to come back in ascending id order. It still catches a number-only sort. The tie-break test was red.
- `dotnet format whitespace` also tidied existing whitespace in `OrderQueries.cs`.
