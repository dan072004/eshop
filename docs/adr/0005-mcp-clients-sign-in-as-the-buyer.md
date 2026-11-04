# MCP clients sign in as the Buyer through Identity.API

eShop.Mcp is an OAuth protected resource with Identity.API as its authorization server, and every request must carry a Buyer's token. Local MCP clients (Claude Code, and GitHub Copilot in VS Code) are pre-registered in Identity.API as public PKCE clients, one per client app, with fixed loopback redirect URIs and a consent screen. They name eShop.Mcp by its URL in an RFC 8707 resource indicator, so Identity.API's API resource for eShop.Mcp is named with that URL, and the token they get has that URL as its only audience. eShop.Mcp never forwards that token. For each order-tool call it exchanges it (RFC 8693) for a short-lived token for the same Buyer with audience `orders`, and sends only that to Ordering.API, which keeps doing the ownership check. Sessions are stateless, so every tool call is authorised by the token on that request. "My order" therefore always means the orders of the `sub` in a token the Buyer obtained by signing in themselves. No tool takes a user id.

## Considered Options

- **A fixed dev Buyer** (eShop.Mcp signs in as `alice`, or a bearer token is pasted into client config). Rejected: the demo would prove nothing about identity, and one config flag would hand every caller the same Buyer's orders.
- **One token naming both eShop.Mcp and `orders`** (the original design). Impossible with spec-compliant clients: when a request names a resource, Duende 7.3.2 restricts the token's audience to that resource. A prototype (ticket 02) showed `aud=<MCP URL>` only, whatever the API resources' scopes.
- **Token passthrough** (forward the caller's token, checking only that its `scope` contains `orders`). Rejected: the MCP authorization spec forbids it, and it would work only because ServiceDefaults sets `ValidateAudience = false`.
- **Naming the resource `eshop-mcp` and advertising that as the metadata `resource`.** Rejected: the MCP spec requires the canonical server URL, and clients may refuse metadata that doesn't match the URL they connected to.
- **Caching exchanged tokens.** Not chosen: one extra local round trip per order-tool call is cheap, and without a cache no state outlives the request.
- **Dynamic Client Registration.** Rejected: a new Duende package with its own store and licence, and a new client row on every fresh connection. Pre-registered clients live in `Config.cs` next to `webapp`.
- **Client ID Metadata Documents.** Not chosen: support in Duende 7.3 is unconfirmed.
- **Claude Desktop / claude.ai custom connectors.** Rejected: they connect from Anthropic's cloud, so eShop.Mcp and Identity.API would need a public tunnel, and the tunnel's issuer would not match the `ValidIssuers` every existing API validates against.
- **Stateful MCP sessions.** Rejected: they tie the caller's identity to session state that outlives a request, and none of the tools needs server-to-client features.

## Consequences

- Identity.API must know eShop.Mcp's URL. The AppHost injects the URL of the active launch profile (http or https) into both Identity.API, as the API resource's name, and eShop.Mcp, which validates it as its audience and advertises exactly it as the metadata `resource`. A client that connects through another host form (`127.0.0.1` instead of `localhost`) is refused by the client itself, so the docs give the exact URL.
- The scope stays `eshop-mcp`. The MCP clients `claude-code` (redirects `http://localhost:<port>/callback` and `http://127.0.0.1:<port>/callback` on one fixed port) and `vscode` (`http://127.0.0.1:33418`, `http://127.0.0.1:33418/`, `https://vscode.dev/redirect`) may request `openid`, `profile`, `offline_access` and `eshop-mcp`, but not `orders`: with a resource indicator it would be dropped from their token anyway. The consent text for `eshop-mcp` says it includes reading the Buyer's orders.
- The consent screen appears on every fresh sign-in, even after "Remember My Decision". Duende 7.3.2 always requires consent when `offline_access` is requested ("Scopes contains offline_access, consent is required"), and both clients request it for a refresh token. Reconnecting with the refresh token asks for nothing. We accept this: dropping `offline_access` would mean signing in again whenever the access token expires, and replacing Duende's consent service is security code kept only to save a click.
- eShop.Mcp becomes a confidential client, `eshop-mcp-server`, with an Aspire-generated secret. It may use only the token-exchange grant, for only the `orders` scope. The grant accepts a subject token only if it is unexpired, names eShop.Mcp's URL as audience and carries `eshop-mcp`. It issues a 5-minute token for the same `sub` with `aud: orders`, `scope: orders` and `act: { client_id: eshop-mcp-server }` (delegation), and no refresh token. If the exchange is refused, the order tools return `sign_in_required` and Ordering receives no request.
- The `orders` API resource lists `Scopes = { "orders" }`. Duende 7 derives a token's audiences from the API resources whose `Scopes` contain a requested scope, so WebApp's and the MAUI app's tokens also name `orders`. That is harmless: it is the audience Ordering.API is configured with.
- Resource indicators use Duende's resource isolation, an Enterprise edition feature. Without a licence, Identity.API runs in trial mode, which in 7.3.2 only logs a warning after 500 protocol requests. A future Duende version is expected to stop at that limit. Outside development, MCP sign-in needs an Enterprise or Community Edition licence.
- Claude Desktop is not a supported client. The week's acceptance check runs in Claude Code.
- `ValidateAudience = false` in ServiceDefaults remains a gap that this design does not rely on. Closing it deserves its own hardening ticket.
- Catalog and policy tools also require sign-in. Letting them work before sign-in ("lazy authentication") would mean answering only order-tool calls with an HTTP 401.

## Prior art

- MCP specification, Authorization (2025-11-25): protected resource metadata (RFC 9728), resource indicators (RFC 8707), PKCE, the ban on token passthrough, and the warning about loopback-only redirects.
- RFC 8252, OAuth 2.0 for Native Apps: loopback redirects for desktop and CLI clients.
- RFC 8693, OAuth 2.0 Token Exchange: the downstream `orders` token, and the `act` claim for delegation.
- Duende IdentityServer: resource isolation and extension grants.
- Anthropic, "Authentication for connectors": Claude's client registration, callback URLs, and where requests come from.
