# 01: eShop.Mcp serves `search_catalog` to Buyers with valid tokens

**What to build:** The tracer bullet for eShop.Mcp. It has a new Aspire project, a protected MCP endpoint, and one real tool, so the hosting and auth path works before the Policy and order tools exist.

- **Hosting:** new project and Aspire resource `eshop-mcp`, added to `eShop.Web.slnf`, with service defaults and a health check. It uses `ModelContextProtocol.AspNetCore` (version in central package management) with Streamable HTTP in **stateless** mode at `/mcp`, which requires authorisation. Health endpoints stay anonymous.
- **AppHost:** `eshop-mcp` references catalog-api. It waits for Identity.API and gets `Identity__Url` set to the same endpoint the APIs get. It follows the http/https launch-profile switch, has fixed ports in its launch settings, and has no external endpoints. Identity.API receives eShop.Mcp's URL, joining the existing reference cycle.
- **Authentication (ADR 0005):** eShop.Mcp configures JWT bearer itself, **validating the `eshop-mcp` audience**, with the injected Identity URL as issuer and `SaveToken` on. Protected resource metadata (RFC 9728) comes through the SDK's MCP authentication handler: `resource` is eShop.Mcp's own URL, `authorization_servers` is only the Identity URL, and `scopes_supported` lists `openid`, `profile`, `offline_access`, `eshop-mcp` and `orders`. A request without a valid token gets 401 with `WWW-Authenticate: Bearer resource_metadata="…"`.
- **Tool:** the library's `search_catalog` is wrapped with `McpServerTool.Create(AIFunction, …)`, keeping its name, description and parameters. No picture URL function is supplied, so results have no `pictureUrl`. Results are the library's JSON text, with no `outputSchema`. This step also checks the spec's unverified assumption that the SDK can wrap an `AIFunction`.

See the eshop-mcp spec's "eShop.Mcp" and "AppHost" sections.

**Blocked by:** assistant-policies-and-orders 01 (shared tools library, starting with `search_catalog`)

**Status:** ready-for-agent

- [ ] New `Mcp.FunctionalTests` (xUnit v3, eShop.Mcp in a `WebApplicationFactory`). It uses the real MCP SDK client, JWT validation trusting a test signing key with tokens minted per test, and a faked outbound `HttpMessageHandler`:
  - [ ] No token → 401, and `WWW-Authenticate` carries `resource_metadata`. The metadata's `resource` is the server URL, and `authorization_servers` is exactly the configured Identity URL
  - [ ] A token without the `eshop-mcp` audience (e.g. only `orders`) → 401
  - [ ] An expired token → 401
  - [ ] `tools/list` returns `search_catalog` with the contract's parameter name
  - [ ] Calling `search_catalog` reaches Catalog with no `Authorization` header, and the result has no `pictureUrl`
- [ ] AppHost unit tests: `eshop-mcp` exists and references catalog-api; its `Identity__Url` matches Ordering.API's; Identity.API receives its URL; it uses the http profile when `ESHOP_USE_HTTP_ENDPOINTS=1`
- [ ] `eshop-mcp` shows as healthy in the Aspire dashboard
