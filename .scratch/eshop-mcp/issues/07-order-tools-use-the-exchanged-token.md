# 07: The order tools use the exchanged token

**What to build:** A signed-in Buyer asks Claude Code about their orders, and `list_my_orders` and `get_order` work again. eShop.Mcp exchanges the Buyer's token for an Ordering token on each call (ticket 06), and Ordering.API only ever sees that exchanged token, never the caller's (no passthrough, ADR 0005). See the eshop-mcp spec, "Token exchange".

- **The Ordering client's handler**, on every request:
  - Posts the token-exchange request to Identity.API's token endpoint under the injected Identity URL. The request carries the caller's validated access token as `subject_token`, `subject_token_type=urn:ietf:params:oauth:token-type:access_token` and `scope=orders`, and authenticates as `eshop-mcp-server` with the AppHost-supplied secret.
  - Sends the issued token as Bearer.
  - Caches nothing between calls.
- **Failures:**
  - A refused exchange (any 400, such as `invalid_grant` or `invalid_client`) makes the order tools return `sign_in_required`.
  - Identity.API unreachable, or answering 5xx, makes them return `unavailable`.
  - In both cases Ordering receives no request.
- **What gets deleted:** the handler that forwards the caller's token when it names `orders`, and its tests.
- **Unchanged:** Policy and catalog calls send no token and trigger no exchange.

**Blocked by:** 05 (tokens name eShop.Mcp's URL), 06 (Identity.API's token-exchange grant)

**Status:** ready-for-agent

- [ ] `Mcp.FunctionalTests`. The fake outbound handler also plays Identity.API's token endpoint, returning a distinct exchanged token per subject token:
  - [ ] `get_order` for Buyer A: the exchange request carries Buyer A's token as `subject_token`, the token-exchange grant type and `scope=orders`, and authenticates as `eshop-mcp-server`. Ordering receives the exchanged token as Bearer, never Buyer A's token
  - [ ] Buyer A's and then Buyer B's calls on the same client each exchange their own token, and Ordering receives each Buyer's exchanged token
  - [ ] Two order-tool calls with the same caller token make two exchange requests
  - [ ] Exchange refused (400 `invalid_grant`) → `list_my_orders` and `get_order` return `sign_in_required`, and Ordering receives no request
  - [ ] Token endpoint 5xx → `unavailable`, and Ordering receives no request
  - [ ] Ordering's 404 still comes back as `not_found`
  - [ ] `search_policies` and `search_catalog` trigger no exchange
- [ ] The interim `sign_in_required` cases from 05, and the old forwarding tests, are deleted in this change
- [ ] Manual smoke test with `aspire run` and `ESHOP_USE_HTTP_ENDPOINTS=1`: in Claude Code signed in as alice, `list_my_orders` returns her orders, and the Aspire traces show a token-exchange call before each Ordering call
