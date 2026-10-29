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

**Status:** ready-for-agent

- [ ] The `eshop-mcp` scope and the two clients are added through configuration next to the existing clients, with the existing clients unchanged
- [ ] Manual: from Claude Code, the documented command opens the Identity.API login. Signing in as alice and approving consent succeeds, and a catalog search returns products
- [ ] Manual: reconnecting doesn't ask for consent again
- [ ] Manual: the same flow from Copilot in VS Code, or the actual VS Code redirect URIs recorded and the configuration corrected
- [ ] The results for both unverified assumptions are recorded in the ticket's comments, and in ADR 0005 if the fallback was needed
- [ ] The client setup documentation exists where developers will find it
