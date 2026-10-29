# 07: Shopping Assistant reads one order, with item type and brand

**What to build:** The Shopping Assistant can open one of the Buyer's orders and see what was in it, including each item's catalog type and brand. That lets it apply per-type Policy rules (e.g. the 30-day Return Window for Navigation) and per-brand warranty lengths.

- `get_order(orderNumber)` in the library returns `{ today, orderNumber, orderDate, status, items: [ { productId, productName, units, unitPrice, type, brand } ] }`.
- `type` and `brand` come from Catalog.API by `productId`. An item Catalog doesn't know gets `null` for both, and the tool doesn't fail.
- Errors are tool output: 404 → `not_found` (covers unknown orders and orders that aren't yours), 401 or no token → `sign_in_required`, anything else → `unavailable`.
- The tool passes any order number through. Ordering.API's endpoint does the ownership check, and the tool adds none.

**Blocked by:** 06 (Shopping Assistant lists the Buyer's orders)

**Status:** ready-for-agent

- [ ] Fast tests at the `AIFunction` seam (fake outbound `HttpMessageHandler`):
  - [ ] Items carry the `type` and `brand` that Catalog returned
  - [ ] A product Catalog doesn't know yields `null` type and brand
  - [ ] 404 → `not_found`; 401 → `sign_in_required`; 500 → `unavailable`
  - [ ] `today` is present, and the tool has the contract's name and parameter name
- [ ] The Shopping Assistant offers `get_order`
- [ ] Manual: "what was in my last order?" lists the items with their types
- [ ] Manual: asking about one of bob's order numbers while signed in as alice is answered as not found
