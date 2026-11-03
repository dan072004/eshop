# 07: Shopping Assistant reads one order, with item type and brand

**What to build:** The Shopping Assistant can open one of the Buyer's orders and see what was in it, including each item's catalog type and brand. That lets it apply per-type Policy rules (e.g. the 30-day Return Window for Navigation) and per-brand warranty lengths.

- `get_order(orderNumber)` in the library returns `{ today, orderNumber, orderDate, status, items: [ { productId, productName, units, unitPrice, type, brand } ] }`.
- `type` and `brand` come from Catalog.API by `productId`. An item Catalog doesn't know gets `null` for both, and the tool doesn't fail.
- Errors are tool output: 404 → `not_found` (covers unknown orders and orders that aren't yours), 401 or no token → `sign_in_required`, anything else → `unavailable`.
- The tool passes any order number through. Ordering.API's endpoint does the ownership check, and the tool adds none.

**Blocked by:** 06 (Shopping Assistant lists the Buyer's orders)

**Status:** done

- [x] Fast tests at the `AIFunction` seam (fake outbound `HttpMessageHandler`):
  - [x] Items carry the `type` and `brand` that Catalog returned
  - [x] A product Catalog doesn't know yields `null` type and brand
  - [x] 404 → `not_found`; 401 → `sign_in_required`; 500 → `unavailable`
  - [x] `today` is present, and the tool has the contract's name and parameter name
- [x] The Shopping Assistant offers `get_order`
- [ ] Manual: "what was in my last order?" lists the items with their types
- [ ] Manual: asking about one of bob's order numbers while signed in as alice is answered as not found

## Comments

- Type and brand: one call to Catalog.API `items/by?ids=...` (it returns `catalogTypeId`/`catalogBrandId` without names) plus `catalogtypes` and `catalogbrands`, in parallel; no Catalog.API change. New `CatalogClient` methods: `GetItemsByIdsAsync`, `GetTypesAsync`, `GetBrandsAsync`.
- A product Catalog doesn't know gets `type: null, brand: null`, written explicitly (`JsonIgnoreCondition.Never`): the AI serializer otherwise drops nulls.
- Decision: if Catalog itself fails (non-2xx, unreachable), `get_order` returns `unavailable` ("Error accessing catalog."), not null types, so the model can't apply the default Return Window to a Navigation item by mistake.
- Added `ToolError.NotFound` (`not_found`); `OrderingError` maps 404 to it for every order tool. `OrderingClient.GetOrderAsync(orderNumber, ct)` returns `OrderingOrder`.
- `AssistantTools.All` now matches the contract order: `search_policies, list_my_orders, get_order, search_catalog`. `ShoppingAssistantTests` now also expects `list_my_orders` (06 hadn't added it).
