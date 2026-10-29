# 03: eShop.Mcp exposes Policy and order tools with the Buyer's token

**What to build:** eShop.Mcp offers the full tool contract: `search_policies`, `search_catalog`, `list_my_orders` and `get_order`. An MCP client can then answer Policy questions with Citations and read the signed-in Buyer's own orders.

- The three remaining library tools are wrapped with `McpServerTool.Create(AIFunction, …)`, unchanged and unprefixed. eShop.Mcp adds no tools, resources or prompts of its own.
- `eshop-mcp` gets AppHost references to policy-api and ordering-api.
- The existing token-forwarding handler (`AddAuthToken`) is attached to the library's Ordering client only. It forwards the caller's own validated token, read from the request. The policy and catalog clients send no token.
- Sessions are stateless, so every tool call is authorised by the token on that request. No tool takes a user id, and Ordering.API keeps the ownership check.

**Blocked by:** 01 (eShop.Mcp serves `search_catalog`), assistant-policies-and-orders 04 (`search_policies` in the library), assistant-policies-and-orders 07 (`get_order` in the library)

**Status:** ready-for-agent

- [ ] `Mcp.FunctionalTests`:
  - [ ] `tools/list` returns exactly `search_policies`, `search_catalog`, `list_my_orders` and `get_order`, with the contract's parameter names
  - [ ] `get_order` with Buyer A's token reaches Ordering with Buyer A's token as Bearer
  - [ ] Calls with Buyer A's and then Buyer B's token on the same client each reach Ordering with their own token
  - [ ] Ordering's 404 comes back as the `not_found` error result
  - [ ] `search_policies` and `search_catalog` reach their backends with no `Authorization` header
- [ ] Each tool's result shape is not tested again here. The library's tests cover it
- [ ] AppHost unit tests: `eshop-mcp` references policy-api, catalog-api and ordering-api
