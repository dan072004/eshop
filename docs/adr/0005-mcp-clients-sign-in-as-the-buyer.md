# MCP clients sign in as the Buyer through Identity.API

eShop.Mcp is an OAuth protected resource with Identity.API as its authorization server, and every request must carry a Buyer's token. Local MCP clients (Claude Code, and GitHub Copilot in VS Code) are pre-registered in Identity.API as public PKCE clients, one per client app, with fixed loopback redirect URIs and a consent screen. The token they get names two audiences, `eshop-mcp` and `orders`: eShop.Mcp checks that its own audience is present and forwards the same token to Ordering.API, which keeps doing the ownership check. Sessions are stateless, so every tool call is authorised by the token on that request. "My order" therefore always means the orders of the `sub` in a token the Buyer obtained by signing in themselves. No tool takes a user id, and eShop.Mcp never uses a token other than the caller's.

## Considered Options

- **A fixed dev Buyer** (eShop.Mcp signs in as `alice`, or a bearer token is pasted into client config). Rejected: the demo would prove nothing about identity, and one config flag would hand every caller the same Buyer's orders.
- **Token passthrough** (forward a token issued only for eShop.Mcp). Rejected: the MCP authorization spec forbids it, and it would work only because ServiceDefaults sets `ValidateAudience = false`.
- **Token exchange (RFC 8693)** for a downstream `orders` token. Rejected for now: it needs a custom extension grant in Identity.API and a confidential client for eShop.Mcp. It is the next step if eShop.Mcp ever needs narrower downstream tokens.
- **Dynamic Client Registration.** Rejected: a new Duende package with its own store and licence, and a new client row on every fresh connection. Pre-registered clients live in `Config.cs` next to `webapp`.
- **Client ID Metadata Documents.** Not chosen: support in Duende 7.3 is unconfirmed.
- **Claude Desktop / claude.ai custom connectors.** Rejected: they connect from Anthropic's cloud, so eShop.Mcp and Identity.API would need a public tunnel, and the tunnel's issuer would not match the `ValidIssuers` every existing API validates against.
- **Stateful MCP sessions.** Rejected: they tie the caller's identity to session state that outlives a request, and none of the tools needs server-to-client features.

## Consequences

- Identity.API gains an `eshop-mcp` API scope and resource, plus the clients `claude-code` (redirects `http://localhost:<port>/callback` and `http://127.0.0.1:<port>/callback` on one fixed port) and `vscode` (`http://127.0.0.1:33418`, `https://vscode.dev/redirect`). Both are added through configuration, with no change to existing clients.
- eShop.Mcp's protected resource metadata must name exactly the Identity URL the AppHost injects into the other services, or Ordering rejects the tokens on issuer.
- Claude Desktop is not a supported client. The week's acceptance check runs in Claude Code.
- `ValidateAudience = false` in ServiceDefaults remains a gap that this design does not rely on. Closing it deserves its own hardening ticket.
- Catalog and policy tools also require sign-in. Letting them work before sign-in ("lazy authentication") would mean answering only order-tool calls with an HTTP 401.

## Prior art

- MCP specification, Authorization (2025-11-25): protected resource metadata (RFC 9728), PKCE, the ban on token passthrough, and the warning about loopback-only redirects.
- RFC 8252, OAuth 2.0 for Native Apps: loopback redirects for desktop and CLI clients.
- RFC 8693, OAuth 2.0 Token Exchange: the rejected downstream alternative.
- Anthropic, "Authentication for connectors": Claude's client registration, callback URLs, and where requests come from.
