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

**Status:** done

- [x] `Mcp.FunctionalTests`. The fake outbound handler also plays Identity.API's token endpoint, returning a distinct exchanged token per subject token:
  - [x] `get_order` for Buyer A: the exchange request carries Buyer A's token as `subject_token`, the token-exchange grant type and `scope=orders`, and authenticates as `eshop-mcp-server`. Ordering receives the exchanged token as Bearer, never Buyer A's token
  - [x] Buyer A's and then Buyer B's calls on the same client each exchange their own token, and Ordering receives each Buyer's exchanged token
  - [x] Two order-tool calls with the same caller token make two exchange requests
  - [x] Exchange refused (400 `invalid_grant`) → `list_my_orders` and `get_order` return `sign_in_required`, and Ordering receives no request
  - [x] Token endpoint 5xx → `unavailable`, and Ordering receives no request
  - [x] Ordering's 404 still comes back as `not_found`
  - [x] `search_policies` and `search_catalog` trigger no exchange
- [x] The interim `sign_in_required` cases from 05, and the old forwarding tests, are deleted in this change
- [ ] Manual smoke test with `aspire run` and `ESHOP_USE_HTTP_ENDPOINTS=1`: in Claude Code signed in as alice, `list_my_orders` returns her orders, and the Aspire traces show a token-exchange call before each Ordering call

## Comments

- Done (agent). `OrdersTokenExchangeHandler` on the Ordering client calls `TokenExchangeClient` on every request. The client posts the RFC 8693 request to `{Identity:Url}/connect/token` (client_secret_post as `eshop-mcp-server`, secret from `EshopMcpServerSecret`) and sends only the issued token as Bearer. A 400 or 401 from the token endpoint, or a missing caller token, gets a synthetic 401 without sending, so the tools return `sign_in_required`. An unreachable endpoint, a 5xx or an unreadable answer throws `TokenExchangeUnavailableException`, so they return `unavailable`. That exception is deliberately not an `HttpRequestException`, so the Ordering client's resilience pipeline doesn't retry an exchange the token client has already retried. `OrdersAudienceHandler` and `AddAuthToken` on the Ordering client are deleted. In `Mcp.FunctionalTests`, the interim `sign_in_required` test is replaced by 7 tests (one is a theory with two cases: unreachable and 5xx), and the fake token endpoint returns a distinct token per subject token. The fixture sets ServiceDefaults' shared `-standard` retry delay to zero, so the failure cases run in milliseconds.
- Security review (security-reviewer agent): no findings. It checked passthrough, tokens on the Policy and Catalog clients and on the exchange call, mixing up users across the handler's lifetime, leaks of the secret or token through logs or errors, SSRF (the token URL comes only from config), and the error bodies sent to the client.
- Manual smoke test (Claude Code as alice, a token-exchange span before each Ordering call) not run here. It is pending and belongs to ticket 04's acceptance pass.
