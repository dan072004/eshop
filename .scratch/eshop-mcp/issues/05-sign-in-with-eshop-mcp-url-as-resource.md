# 05: Clients sign in with eShop.Mcp's URL as the resource

**What to build:** A Buyer connects Claude Code to eShop.Mcp, signs in as alice and approves consent without Identity.API answering `invalid_target`, then searches the catalog. Today the sign-in fails because Claude Code names eShop.Mcp by its URL (an RFC 8707 resource indicator, as the MCP authorization spec requires), and Identity.API only knows a resource named `eshop-mcp`. See the eshop-mcp spec and ADR 0005, and the prototype result on ticket 02.

- **One URL value.** The AppHost passes eShop.Mcp's `/mcp` URL for the active launch profile (for example `http://localhost:5229/mcp`) to both Identity.API and eShop.Mcp, as the setting `EshopMcpUrl`. Identity.API therefore rejoins the deliberate reference cycle, as before `aa8d7c1`. Ticket 06 reads the same setting and runs in parallel: whichever ticket lands first adds the AppHost wiring, and the other reuses it.
- **Identity.API:**
  - The API resource for eShop.Mcp is named by that URL and lists the scope `eshop-mcp`. No resource named `eshop-mcp` remains.
  - `claude-code` and `vscode` may request `openid`, `profile`, `offline_access` and `eshop-mcp`. They lose `orders`.
  - The `eshop-mcp` scope keeps its consent text ("search the store and read your orders").
- **eShop.Mcp:**
  - It validates that URL as the audience.
  - It advertises exactly that URL as the protected resource metadata's `resource`, no longer derived from the incoming request.
  - `scopes_supported` drops `orders`.
- **Order tools in between.** Until 07, the order tools return `sign_in_required`, because the Buyer's token no longer names `orders`. No real client could sign in before this ticket, so nothing that worked stops working.
- **README** ("Optional: Connect Claude Code or VS Code"):
  - The Claude Code command uses exactly `http://localhost:5229/mcp`.
  - `ESHOP_USE_HTTP_ENDPOINTS=1` is the documented path for Claude Code, for the whole AppHost.
  - `NODE_EXTRA_CA_CERTS` is described as unreliable (Claude Code 2.1.286 ignores it on its reconnect path).
  - A warning that the Cosmos DB Emulator's self-signed `CN=localhost` roots break dev-certificate trust.
  - A note that resource indicators need a Duende Enterprise or Community Edition licence outside development (trial mode logs a warning after 500 requests).
  - The README already has an uncommitted change by the user. Keep it.

**Blocked by:** None (can start immediately)

**Status:** done

- [x] `Mcp.FunctionalTests`, with tokens minted for the configured test URL:
  - [x] No token → 401 with `resource_metadata`. The metadata's `resource` is exactly the configured URL, also for a request made on another host form. `authorization_servers` is exactly the configured Identity URL. `scopes_supported` is `openid`, `profile`, `offline_access`, `eshop-mcp`
  - [x] A token whose audience is `eshop-mcp`, but not the configured URL → 401
  - [x] A token whose audience is only `orders` → 401
  - [x] Existing cases (expired token, `tools/list`, catalog and policy calls without `Authorization`) pass with the URL audience
  - [x] The order-tool cases that relied on `aud: orders` are changed to expect `sign_in_required` with no request to Ordering (07 replaces them)
- [x] AppHost unit tests: Identity.API and eShop.Mcp receive the same `EshopMcpUrl`, under both the https profile and `ESHOP_USE_HTTP_ENDPOINTS=1`. This replaces the test removed in `aa8d7c1`
- [x] The `claude-code` and `vscode` clients and the `orders` resource are otherwise unchanged. WebApp, MAUI and Webhooks clients are untouched
- [x] README updated as above
- [x] The spec's "Identity.API does not receive eShop.Mcp's URL" no longer holds anywhere in code or docs

## Comments

- Done (agent). The AppHost passes `EshopMcpUrl` (`{eshop-mcp endpoint of the active profile}/mcp`) to Identity.API and eShop.Mcp, and Identity.API rejoins the reference cycle. Identity.API names eShop.Mcp's resource by that URL, with the scope `eshop-mcp`. `claude-code` and `vscode` lose `orders`. eShop.Mcp validates the URL as audience and advertises it as the metadata `resource`, and `scopes_supported` drops `orders`. The order tools now return `sign_in_required`: one interim test replaces the three forwarding and not-found cases, and 07 restores them. `IdentityConfigurationTests` (Application.UnitTests) had to change because `GetApis` now takes configuration. It also pins the MCP clients' scopes. README: HTTP is the Claude Code path, `NODE_EXTRA_CA_CERTS` is marked unreliable, and warnings about the Cosmos DB Emulator certificate and the Duende licence were added. The main checkout had no uncommitted README change to keep.
- Security review (security-reviewer agent): no findings. The metadata `resource` no longer reflects the request's Host header, which removes a host-reflection risk. Ordering.API's `ValidateAudience=false` remains the known gap, and this change doesn't make it worse.
- Manual check (sign in as alice in Claude Code without `invalid_target`) not run here. It belongs to the acceptance pass.
