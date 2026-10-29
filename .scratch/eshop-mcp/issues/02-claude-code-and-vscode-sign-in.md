# 02: Claude Code and VS Code sign in through Identity.API

**What to build:** A Buyer connects Claude Code (or Copilot in VS Code) to eShop.Mcp, signs in on AdventureWorks' own login page in the browser, approves a consent screen, and can then search the catalog from the client.

- **Identity.API (configuration only, no change to existing clients):**
  - It gets a new API resource and scope, `eshop-mcp`.
  - `claude-code` is a public client: authorization code with PKCE (S256), consent required and remembered, offline access with refresh-token rotation. Its redirect URIs are `http://localhost:{port}/callback` and `http://127.0.0.1:{port}/callback` on one fixed port, which is a setting with a default.
  - `vscode` has the same settings, with redirect URIs `http://127.0.0.1:33418` and `https://vscode.dev/redirect`.
  - Both clients may request `openid`, `profile`, `offline_access`, `eshop-mcp` and `orders`. The token names both `eshop-mcp` and `orders` as audiences.
- **Developer documentation:**
  - The `claude mcp add --transport http --client-id claude-code --callback-port <port> eshop <URL>/mcp` command.
  - A `.vscode/mcp.json` HTTP server entry, using client id `vscode`.
  - Trusting the .NET dev certificate in Claude Code (`NODE_EXTRA_CA_CERTS`), with `ESHOP_USE_HTTP_ENDPOINTS=1` as the fallback.
  - A note that Claude Desktop isn't supported (ADR 0005).

This is where two unverified assumptions get proven: that Duende's exact redirect matching accepts Claude Code's fixed-port loopback URIs, and that the VS Code redirect URIs (taken from third-party guides) are right. If the fixed port doesn't work, the fallback is a loopback-aware redirect validator for these two clients only. Record it in ADR 0005 if it's needed.

**Blocked by:** 01 (eShop.Mcp serves `search_catalog`)

**Status:** ready-for-human

- [ ] The `eshop-mcp` scope and the two clients are added through configuration next to the existing clients, with the existing clients unchanged
- [ ] Manual: from Claude Code, the documented command opens the Identity.API login. Signing in as alice and approving consent succeeds, and a catalog search returns products
- [ ] Manual: reconnecting doesn't ask for consent again
- [ ] Manual: the same flow from Copilot in VS Code, or the actual VS Code redirect URIs recorded and the configuration corrected
- [ ] The results for both unverified assumptions are recorded in the ticket's comments, and in ADR 0005 if the fallback was needed
- [ ] The client setup documentation exists where developers will find it

## Comments

**Implementer (2026-10-29), static checks only. No interactive sign-in was attempted.**

Done: `eshop-mcp` API resource and scope, and the `claude-code` and `vscode` clients, in `src/Identity.API/Configuration/Config.cs`. The callback port is the `ClaudeCodeCallbackPort` setting (default `33419`, in Identity.API's `appsettings.json`). The setup docs are in `README.md` under "Optional: Connect Claude Code or VS Code". All existing clients are unchanged.

One change to an existing *resource*: the `orders` ApiResource now lists `Scopes = { "orders" }`. In Duende 7.3.2 the audience comes from the ApiResources whose `Scopes` contain a requested scope, and `orders` listed none, so no token has ever carried `aud: orders`. A probe against Duende 7.3.2's `InMemoryResourcesStore` returned only `eshop-mcp` for the scopes `orders` and `eshop-mcp`. With the change, WebApp and MAUI tokens also gain `aud: orders`. That's harmless: ServiceDefaults sets `ValidateAudience = false`, and `orders` is the audience Ordering.API is configured with anyway.

Assumption 1, Claude Code's fixed-port loopback redirect with Duende's exact matching. Statically this looks OK:
- A probe of Duende 7.3.2's `StrictRedirectUriValidator` accepted `http://localhost:33419/callback` and `http://127.0.0.1:33419/callback`, and also a differently-cased host.
- It rejected another port and a trailing slash. So a Claude Code version that ignores `--callback-port` would fail, and that is the case the loopback-aware validator fallback covers.
- Claude Code's MCP docs (code.claude.com/docs/en/mcp) say it sends `http://localhost:PORT/callback`. v2.1.229 sent the `127.0.0.1` form, and v2.1.231 restored `localhost`. Both forms are registered.
- The fallback was not needed, so ADR 0005 is unchanged.

Assumption 2, the VS Code redirect URIs. These were corrected based on static evidence:
- microsoft/vscode#278512 lists the URIs VS Code registers itself: `http://127.0.0.1:33418/` with a trailing slash, `http://127.0.0.1/`, `https://vscode.dev/redirect` and `https://insiders.vscode.dev/redirect`.
- Duende matches exactly, so `http://127.0.0.1:33418/` is now registered next to the spec's `http://127.0.0.1:33418`. `https://vscode.dev/redirect` is kept.
- The same issue says VS Code falls back to a random port when 33418 is busy, for example when another VS Code window is signing in. That would fail here without a loopback-aware validator. It's documented as a limitation, not fixed.
- Insiders is not registered.

Left for the manual check:
- The Claude Code sign-in as alice, consent, and a catalog search.
- Reconnecting without consent. Note that the persisted grant store is in memory, so remembered consent and refresh tokens last only until Identity.API restarts.
- The VS Code flow. Record which redirect URI VS Code actually sends, and drop the URI it doesn't use.
- Whether VS Code prompts for the client ID as documented.

Security review (security-reviewer agent), with no findings above medium. All three are design choices the spec and ADR 0005 mandate, so they are not changed here. They are recorded as follow-ups:
- Medium: no API checks scopes, and `ValidateAudience = false`, so an MCP client's token (and its 30-day refresh token) can also call Ordering write endpoints, Basket and Webhooks. The fix is the deferred hardening ticket: per-API scope policies plus audience validation. A shorter `AbsoluteRefreshTokenLifetime` for the MCP clients is worth considering.
- Low: `https://vscode.dev/redirect` lets any website start a `client_id=vscode` flow. Remove it if the manual check shows VS Code desktop doesn't use it.
- Low: with fixed loopback ports and remembered consent, local malware that binds the port first can get a token silently. RFC 8252 accepts this risk.
