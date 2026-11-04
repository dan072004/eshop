# 06: Identity.API exchanges a Buyer's eShop.Mcp token for an Ordering token

**What to build:** eShop.Mcp can post a Buyer's eShop.Mcp token to Identity.API's token endpoint and get back a short-lived token for the same Buyer that Ordering.API should accept. This is RFC 8693 token exchange as delegation, ADR 0005. Nothing calls it yet: 07 wires it into eShop.Mcp. See the eshop-mcp spec, "Identity.API" and "Testing Decisions".

- **eShop.Mcp's URL.** The grant checks the subject token's audience against eShop.Mcp's `/mcp` URL, read from the setting `EshopMcpUrl`. Ticket 05 introduces the same setting and runs in parallel: whichever lands first adds the AppHost wiring, and the other reuses it.
- **Confidential client `eshop-mcp-server`:**
  - Its secret is an Aspire-generated secret parameter, passed by the AppHost to both Identity.API and eShop.Mcp.
  - Its only allowed grant is token exchange, and its only allowed scope is `orders`.
  - Access tokens last 5 minutes, with no refresh tokens and no consent.
  - Existing clients are unchanged.
- **Token-exchange grant** (a Duende extension grant for `urn:ietf:params:oauth:grant-type:token-exchange`, with `subject_token_type=urn:ietf:params:oauth:token-type:access_token`):
  - The subject token is validated as an Identity.API access token. It must be unexpired, name eShop.Mcp's URL as an audience and carry the `eshop-mcp` scope. Anything else gets `invalid_grant`.
  - It issues a token for the subject's `sub` with `scope: orders` (so `aud: orders`) and `act: { "client_id": "eshop-mcp-server" }`.
  - The existing profile service still requires the Buyer to exist and be active.
- **New test project `Identity.FunctionalTests`** (xUnit v3), the first test project for Identity.API:
  - The real Identity.API runs in a `WebApplicationFactory` with a real Postgres container through Aspire, like the other functional tests, and seeds its users.
  - Subject tokens are minted inside the test host with Identity.API's own token tools, so they use the real signing key, with a chosen `sub`, audience, scope and expiry.
  - Requests go to `/connect/token` over HTTP.
  - The project is added to `eShop.Web.slnf`.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] A valid exchange for alice: `sub` is alice's, `aud` is `orders`, `scope` is `orders`, `act.client_id` is `eshop-mcp-server`, `exp - iat` is 300 seconds, and the response has no refresh token
- [ ] Each of these is refused, each request valid except for one thing:
  - [ ] the subject token's audience isn't eShop.Mcp's URL (for example `orders`)
  - [ ] the subject token lacks the `eshop-mcp` scope
  - [ ] the subject token is expired
  - [ ] the subject token is signed by another key
  - [ ] the request comes from `claude-code`
  - [ ] the `eshop-mcp-server` secret is wrong
  - [ ] the requested scope is something other than `orders`
  - [ ] the subject's user doesn't exist
- [ ] AppHost unit tests: Identity.API and eShop.Mcp receive the same `eshop-mcp-server` secret parameter, and both receive `EshopMcpUrl`
- [ ] Security review of the grant, run with the security-reviewer agent as the verify skill requires for Identity.API changes, with findings recorded in the comments
