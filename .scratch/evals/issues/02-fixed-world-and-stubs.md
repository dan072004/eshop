# 02: The fixed world and its stubs

**What to build:** The eval project's skeleton and the world every case is written against. It contains no model calls yet.

- **Project:** a new xUnit v3 test project for the Shopping Assistant evals, added to `eShop.Web.slnf`. Model-calling tests are skipped unless `ESHOP_EVALS=1`. With `ESHOP_EVALS=1` and no Anthropic API key in configuration (`ANTHROPIC_API_KEY` or user-secrets), they fail with a message saying to set it. There is no fallback to an SDK login profile. The fast tests in this ticket always run.
- **World file:** one data file holding Buyers, orders and the products in them. Cases refer to orders by number.
  - **Buyers:** alice and bob, with their claims from Identity's seed (name, last name, address, email, phone).
  - **Orders:**

    | Buyer | Order | Order Date | Status | Items |
    |---|---|---|---|---|
    | alice | 101 | 2026-10-20 | Shipped | GPS watch (Navigation) + jacket |
    | alice | 102 | 2026-10-30 | AwaitingValidation | helmet |
    | alice | 103 | 2026-07-15 | Shipped | boots (outside its Return Window) |
    | alice | 104 | 2026-10-25 | Cancelled | backpack |
    | bob | 201 | 2026-10-28 | Paid | skis |
    | bob | 202 | 2026-10-31 | Submitted | tent |

  - **Products:** real names, types and brands from Catalog's seed data.
- **Clock:** `TimeProvider` fixed at 2026-11-01.
- **Ordering stub,** at the outbound `HttpMessageHandler` of the tools library's Ordering client. It answers by bearer token: `alice`, `bob`, or none.
  - A Buyer's own list request returns their orders newest first; their own detail request returns that order.
  - Any other order number returns 404.
  - No token returns 401.
  - Response shapes are Ordering.API's.
- **Catalog stub:**
  - It answers the item lookups `get_order` makes, giving type and brand for the world's products.
  - For catalog search, it returns the results the current case lists, or an empty list.
- **Basket stub:** throws, so the conversation class returns "Unable to…" if a basket tool is called.
- **The tools library** is registered for real with these stubs.
- These are canned-answer stubs at a boundary, so they need no contract test.

See the evals spec's "The fixed world".

**Blocked by:** None (can start immediately)

**Status:** done

- [x] Fast tests through the tools library's tool functions, invoked as a model would, with JSON compared as literals:
  - [x] alice's `list_my_orders` returns `today` 2026-11-01 and orders 102, 104, 101, 103 in that order (newest first), with their statuses
  - [x] alice's `get_order(101)` returns both items with their catalog `type` and `brand`
  - [x] alice's `get_order(201)` returns `not_found`
  - [x] a signed-out `list_my_orders` returns `sign_in_required`
  - [x] bob's `list_my_orders` returns only 202 and 201
- [x] Without `ESHOP_EVALS`, a placeholder model-calling test is reported as skipped; with it and no key, it fails naming `ANTHROPIC_API_KEY`
- [x] `dotnet test --solution eShop.Web.slnf` runs the fast tests with no key and no Docker

## Comments

- Catalog type and brand ids are numbered by the stub (from the world's products), not Catalog's real seed ids.
- `catalogResults` in a case uses the `WorldProduct` shape: id, name, description, price, type, brand.
