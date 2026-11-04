# Spec: eShop.Mcp, the store's tools for local MCP clients

Status: ready-for-agent

## Problem Statement

A Buyer who works in Claude Code or GitHub Copilot in VS Code can't ask those assistants about AdventureWorks. To learn the Return Window for their last order, they have to leave the editor, open the store, find the order, open the Policies and count days themselves.

- **The store's abilities live only inside WebApp.** Catalog search, Policy search and order lookup are being built as Shopping Assistant tools. No other AI client can call them.
- **An outside client has no way to say who the Buyer is.** WebApp signs the Buyer in with a cookie. An MCP client has no cookie, and Ordering.API only shows orders to the Buyer whose token it receives.
- **A shortcut would leak orders.** Wiring an MCP server to a fixed test user would answer "my last order" with someone else's order for every caller.
- **Claude Desktop can't reach a local server.** Its custom connectors call MCP servers from Anthropic's cloud, so a server and an Identity.API running on a laptop are unreachable from there.
- **Sign-in as first built doesn't work.** MCP clients name the server they want a token for by its URL (an RFC 8707 resource indicator, required by the MCP authorization spec). Identity.API knows eShop.Mcp only as `eshop-mcp`, so it refuses the sign-in with `invalid_target`. And when a client does name a resource, Identity.API issues a token for that resource only, so the planned single token for both eShop.Mcp and Ordering can't exist (prototype on ticket 02).

## Solution

A new Aspire project, eShop.Mcp, exposes the same four tools the Shopping Assistant uses (Policy search, catalog search, list my orders, get one order) as an MCP server over HTTP. Claude Code and Copilot in VS Code connect to it on localhost.

- **The Buyer signs in themselves.** On first use, the client opens Identity.API's login page in the browser. The Buyer signs in and approves the client on a consent screen. From then on, every tool call carries that Buyer's token, issued for eShop.Mcp alone.
- **"My orders" means the signed-in Buyer's orders, and nothing else.** No tool accepts a user id. For each order question, eShop.Mcp asks Identity.API for a short-lived Ordering token on the Buyer's behalf, and Ordering.API decides what the Buyer may see. An order that isn't theirs is reported as not found.
- **Answers carry Citations.** The tools and their descriptions come from the shared tools library, so any model that calls them is told to state Policy rules only from results and to cite each one.
- **The server is a thin host.** It does only hosting, sign-in, the token exchange and tool registration. Composition lives in the shared library, and rules live in the Policies and in Ordering.API.

For "what's the return window for my last order?" in Claude Code, on a shipped order containing a navigation item, the answer is the same as in the Shopping Assistant: 30 days for that item and 60 for the rest, with end dates, citing *Returns Exceptions › GPS watches, navigators and bike computers* and *Returns Policy › Return window*.

## User Stories

1. As a Buyer using Claude Code, I want to ask about AdventureWorks Policies from my terminal, so that I don't have to open the store.
2. As a Buyer using Claude Code, I want each Policy rule in the answer to come with a Citation, so that I can check where it came from.
3. As a Buyer using Claude Code, I want to ask "what's the return window for my last order?", so that I know how long I have without working it out myself.
4. As a Buyer using Claude Code, I want the Return Window worked out per item and given as end dates from the Order Date, so that I get the same answer the Shopping Assistant gives.
5. As a Buyer using Claude Code, I want to ask about one order by its number, so that I can check an older order.
6. As a Buyer using Claude Code, I want to search the catalog by description, so that I can find a product while I'm working.
7. As a Buyer using Copilot in VS Code, I want the same tools and answers, so that the store doesn't depend on one AI vendor.
8. As a Buyer, I want to sign in at AdventureWorks' own login page in my browser, so that I never type my password into an AI client.
9. As a Buyer, I want a consent screen that names the client and says it will search the store and read my orders, so that I know what access I'm granting to an AI client.
10. As a Buyer, I want my consent remembered, so that I'm not asked again on every connection.
11. As a Buyer, I want my session to keep working without signing in every two hours, so that long sessions aren't interrupted.
12. As a Buyer, I want an order number that isn't mine to be treated as unknown, so that nobody can read my orders through an AI client.
13. As a Buyer, I want to be told to sign in again when my sign-in has expired or been revoked, so that I understand why order questions stopped working.
14. As a Buyer, I want the AI to be told when the store is unavailable, rather than given an empty result, so that it doesn't tell me I have no orders.
15. As a Buyer, I want sign-in to succeed from a client that follows the MCP authorization spec, so that I'm not shown an `invalid_target` error page.
16. As a maintainer, I want no tool to accept a user id or email, so that a model can't be talked into asking for another Buyer's orders.
17. As a maintainer, I want eShop.Mcp to accept only tokens issued for its own URL, so that a token meant for another service can't be replayed through it.
18. As a maintainer, I want eShop.Mcp never to forward the caller's token to another service, so that the MCP spec's ban on token passthrough holds.
19. As a maintainer, I want Ordering.API to receive only a token issued for `orders`, for the same Buyer, so that Ordering never depends on its missing audience check.
20. As a maintainer, I want only eShop.Mcp, authenticated with its own secret, to be able to exchange a Buyer's token, so that a stolen MCP-client token can't be turned into an Ordering token by anyone else.
21. As a maintainer, I want the exchange to accept only an unexpired token issued for eShop.Mcp with the `eshop-mcp` scope, so that WebApp's or another client's tokens can't be exchanged.
22. As a maintainer, I want exchanged tokens to last only minutes and come without a refresh token, so that a leaked one is useless soon.
23. As a maintainer, I want exchanged tokens to record that eShop.Mcp acted for the Buyer, so that Ordering's logs and traces can tell MCP calls from the Buyer's own.
24. As a maintainer, I want every tool call authorised by the token on that request, and no exchanged token kept between calls, so that one Buyer's calls can never run with another Buyer's identity.
25. As a maintainer, I want order ownership still checked only in Ordering.API's endpoints, so that there's one place that decides it.
26. As a maintainer, I want the MCP tools to be the shared library's tools, wrapped unchanged, so that MCP and WebApp can't drift apart.
27. As a maintainer, I want the tool names and parameters exactly as the shared contract defines them, so that both hosts expose one contract.
28. As a maintainer, I want eShop.Mcp to contain no business rules, so that a rule change never needs an MCP release.
29. As a maintainer, I want no existing service to change behaviour, so that this work can't break WebApp, mobile-bff or the APIs.
30. As a maintainer, I want the new Identity.API clients, resource and grant added next to the existing ones, with existing clients unchanged, so that WebApp and the MAUI app keep signing in as before.
31. As a maintainer, I want the reasons for this sign-in design recorded in ADR 0005, so that nobody "simplifies" it into token passthrough, a two-audience token or a fixed user.
32. As a maintainer, I want Identity.API and eShop.Mcp to read eShop.Mcp's URL from one AppHost value, so that the resource name, the audience and the advertised metadata can't disagree.
33. As a maintainer, I want the Duende licence requirement for resource indicators documented, so that nobody deploys this unlicensed by surprise.
34. As a developer, I want eShop.Mcp to appear in the Aspire dashboard with its health check, logs and traces, so that I can watch tool calls and token exchanges.
35. As a developer, I want eShop.Mcp on a fixed localhost port, so that my client configuration keeps working across restarts.
36. As a developer, I want eShop.Mcp to follow the same http/https switch as the other front ends, so that its tokens have the same issuer every API expects.
37. As a developer, I want the server to tell clients where to sign in (protected resource metadata), so that Claude Code and VS Code can start sign-in with no manual URLs.
38. As a developer, I want a documented `claude mcp add` command with the exact server URL, so that I can connect Claude Code in one step.
39. As a developer, I want a documented VS Code configuration, so that I can connect Copilot in one step.
40. As a developer, I want plain HTTP (`ESHOP_USE_HTTP_ENDPOINTS=1`) documented as the way to run with Claude Code, with a warning about the Cosmos DB Emulator's certificates, so that a TLS error doesn't block the demo.
41. As a developer, I want Policy and catalog tools to keep working when no embedding provider is configured, so that a fresh clone can run the demo.
42. As a developer, I want catalog results without WebApp-relative picture paths, so that the model isn't given links that lead nowhere.
43. As a maintainer, I want the server's sign-in, token exchange and forwarding tested through the real MCP protocol, so that what's tested is what clients use.
44. As a maintainer, I want the exchange grant tested against the real Identity.API, so that Duende's pipeline, not a mock of it, decides what passes.
45. As a maintainer, I want CI to need no interactive login, no LLM and no MCP client app, so that tests stay fast and deterministic.
46. As a maintainer, I want a manual acceptance check with real sign-in in Claude Code, so that the whole chain is proven once per change.

## Implementation Decisions

**Decisions recorded in ADR 0005** (read it before changing any of them): per-Buyer OAuth through Identity.API; pre-registered public PKCE clients; eShop.Mcp's API resource named by its URL; a token exchange (RFC 8693, delegation) for each order-tool call, with no passthrough and no cache; stateless sessions; local clients only.

**eShop.Mcp (new project)**

- New ASP.NET Core project, Aspire resource `eshop-mcp`, added to `eShop.Web.slnf`, with the standard service defaults and health check.
- Uses `ModelContextProtocol.AspNetCore` (2.3.0, version in central package management) with the Streamable HTTP transport, in **stateless** mode. The MCP endpoint is mapped at `/mcp` and requires authorisation. The health endpoints stay anonymous.
- **Tools.** The four tools come from the shared tools library (`eShop.Assistant.Tools`, specified in the assistant-policies-and-orders spec). Each `AIFunction` is wrapped with `McpServerTool.Create(AIFunction, …)`, keeping its name, description and parameters. eShop.Mcp adds no tools, resources or prompts, and does not rename or prefix them.
  - `search_policies(question)`, `search_catalog(productDescription)`, `list_my_orders()`, `get_order(orderNumber)`.
  - Results are returned as the JSON text the library produces, with no `outputSchema` and no `structuredContent`.
  - Error results stay tool output, as the contract defines: `not_found`, `sign_in_required`, `unavailable`.
  - `get_order`: a product Catalog.API doesn't know gets `null` `type` and `brand`; a Catalog.API failure returns `unavailable` for the whole call, so no Return Window is given without item types.
  - **Descriptions (contract change from assistant ticket 08, already written into the other spec).** `list_my_orders` says it has no items and points to `get_order`. `get_order` tells the model to name the item types when searching the Policies. Without that, the model gave the standard Return Window to a Navigation item.
- **Catalog picture URLs (contract change, already written into the other spec).** The library lets the host supply a product-id → image URL function. eShop.Mcp supplies none, so `pictureUrl` is left out of its catalog results.
- **eShop.Mcp's URL.** One value from the AppHost: the `/mcp` endpoint URL of the active launch profile (for example `http://localhost:5229/mcp`). eShop.Mcp uses it as both its accepted audience and its advertised `resource`. It is no longer derived from the incoming request.
- **Authentication.**
  - JWT bearer authentication against Identity.API, configured by eShop.Mcp itself rather than through the shared `AddDefaultAuthentication`, because it must **validate the audience**, which is eShop.Mcp's URL. The shared default turns audience validation off. A token whose audience is the old name `eshop-mcp`, or `orders`, is rejected.
  - Valid issuer: the Identity URL the AppHost injects, the same value Ordering.API uses.
  - `SaveToken` is on, so the exchange can read the caller's validated token from the request.
  - Protected resource metadata (RFC 9728) through the SDK's MCP authentication handler. `resource` is exactly the configured URL. `authorization_servers` holds only the injected Identity URL. `scopes_supported` lists `openid`, `profile`, `offline_access` and `eshop-mcp`, but not `orders`. A client that connects through another host form (`127.0.0.1` instead of `localhost`) sees a `resource` that doesn't match its URL and refuses to sign in. That is intended: it fails before the browser opens instead of at Identity.API.
  - An unauthenticated or rejected request gets HTTP 401 with `WWW-Authenticate: Bearer resource_metadata="…"`.
- **Token exchange (replaces forwarding the caller's token).** The Ordering client gets a delegating handler that, on every request:
  - Posts an RFC 8693 request to Identity.API's token endpoint, under the injected Identity URL. The request has `grant_type=urn:ietf:params:oauth:grant-type:token-exchange`, `subject_token=<caller's access token>`, `subject_token_type=urn:ietf:params:oauth:token-type:access_token` and `scope=orders`. It authenticates as the confidential client `eshop-mcp-server` with its secret.
  - Sends the issued token as Bearer to Ordering. The caller's token never reaches Ordering.
  - Keeps nothing. Each order-tool call exchanges again, and no exchanged token is cached.
  - On a refused exchange (`invalid_grant`, `invalid_client` or any 400), the order tools return `sign_in_required` and Ordering receives no request. If Identity.API is unreachable or answers 5xx, they return `unavailable`.
- **Outbound HTTP.** The library's typed clients reach `policy-api`, `catalog-api` (v2) and `ordering-api` (v1) by Aspire service name. Only the Ordering client carries a token, the exchanged one. The Policy and catalog clients send no token.
- eShop.Mcp has no database, no event bus subscription and no state of its own.

**Identity.API**

- **eShop.Mcp's API resource is named by eShop.Mcp's URL**, which Identity.API receives from the AppHost as a setting, like the `*Client` URLs. It lists the scope `eshop-mcp`. There is no resource named `eshop-mcp` any more. The scope `eshop-mcp` keeps its consent text, "AdventureWorks MCP Server: search the store and read your orders".
- Prototype result (Duende 7.3.2, branch `prototype/resource-indicators`). With `resource=<MCP URL>` on authorize, code exchange and refresh:

  | API resources | Authorize | Access token |
  |---|---|---|
  | `eshop-mcp` [eshop-mcp], `orders` [orders] | `invalid_target` | (none) |
  | `<MCP URL>` [eshop-mcp], `orders` [orders] | OK | `aud=<MCP URL>`, `scope=[eshop-mcp, offline_access]` |
  | `<MCP URL>` [eshop-mcp, orders], `orders` [orders] | OK | `aud=<MCP URL>`, `scope=[eshop-mcp, orders, offline_access]` |

  The second row is this design.
- The existing `orders` API resource lists `Scopes = { "orders" }`, so a token for the `orders` scope names `orders` as its audience. Side effect: WebApp and MAUI tokens also gain `aud: orders`. That is harmless: it is the audience Ordering.API is configured with.
- **Public MCP clients**, with no change to existing clients:
  - `claude-code`: public client (no secret), authorization code with PKCE (S256), consent required and remembered, offline access with refresh-token rotation. Redirect URIs `http://localhost:{port}/callback` and `http://127.0.0.1:{port}/callback` on one fixed port.
  - `vscode`: the same settings, with redirect URIs `http://127.0.0.1:33418`, `http://127.0.0.1:33418/` and `https://vscode.dev/redirect`.
  - Allowed scopes for both: `openid`, `profile`, `offline_access`, `eshop-mcp`. Not `orders`: with a resource indicator it would be dropped from their token anyway.
  - The Claude Code callback port is a setting with a default.
- **Confidential client `eshop-mcp-server`**:
  - Its secret is supplied by the AppHost.
  - Its only allowed grant is token exchange, and its only allowed scope is `orders`.
  - Access token lifetime 5 minutes, no refresh tokens, no consent.
- **Token-exchange grant** (a Duende extension grant validator for `urn:ietf:params:oauth:grant-type:token-exchange`):
  - The request must come from `eshop-mcp-server`. Duende enforces this through the client's allowed grant types.
  - The subject token is validated as an access token issued by Identity.API. It must be unexpired, name eShop.Mcp's URL as an audience and carry the `eshop-mcp` scope. Anything else is refused with `invalid_grant`.
  - It issues a token for the subject token's `sub`, with `scope: orders` (so `aud: orders`). It adds an `act` claim `{ "client_id": "eshop-mcp-server" }` (delegation).
  - The Buyer must still exist and be active. The existing profile service already checks this for every issued token.
- **Licence.** Resource indicators use Duende's resource isolation, an Enterprise edition feature. Unlicensed, Identity.API runs in trial mode, which in 7.3.2 logs a warning after 500 protocol requests and keeps running. No licence key is added by this spec.

**AppHost**

- Add `eshop-mcp` with references to `policy-api`, `catalog-api` and `ordering-api`. It waits for Identity.API and receives `Identity__Url` set to the same Identity endpoint as the APIs.
- It uses the shared http/https launch-profile switch (`ESHOP_USE_HTTP_ENDPOINTS`), like WebApp. Its launch settings fix its ports, so client configuration stays valid.
- eShop.Mcp's `/mcp` URL, for the active launch profile, goes to both Identity.API and eShop.Mcp. Identity.API therefore references eShop.Mcp's endpoint and joins the deliberate reference cycle, like WebApp. This reverts the earlier decision to keep eShop.Mcp out of the cycle.
- A generated secret parameter for `eshop-mcp-server` goes to both Identity.API and eShop.Mcp.
- It is not marked for external endpoints and not added to mobile-bff. Its clients run on the same machine.

**Client setup (developer documentation)**

- Claude Code: `claude mcp add --transport http --client-id claude-code --callback-port <port> eshop http://localhost:5229/mcp`. Use exactly this URL, with `localhost`.
- VS Code: an HTTP server entry in `.vscode/mcp.json`. When VS Code asks for a client id, enter `vscode`.
- TLS:
  - Run the AppHost with `ESHOP_USE_HTTP_ENDPOINTS=1`. That is the documented path for Claude Code, and it must cover the whole AppHost, because Identity.API is reached during sign-in.
  - `NODE_EXTRA_CA_CERTS` with the exported dev certificate is mentioned as unreliable: Claude Code 2.1.286 ignores it on its reconnect path.
  - A note that the Cosmos DB Emulator's self-signed `CN=localhost` root certificates break dev-certificate trust in Claude Code.
- A note on the Duende licence requirement outside development.
- Claude Desktop is not supported (ADR 0005).

**Domain language**

The MCP caller is a **Buyer**. eShop.Mcp is not the **Shopping Assistant**, which is WebApp's helper. Policy, Policy Section, Citation, Return Window and Order Date keep their `GLOSSARY.md` meanings.

## Testing Decisions

- **What makes a good test here:** drive each service through its public seam over HTTP and assert on what comes out: the status and headers, the tool list, the tool result, the issued token's claims, and the request that reached the backend. Fake only boundaries we don't own (ADR 0002). Every test asserts values, never only that something non-empty came back (ADR 0003).
- **MCP endpoint: functional tests in `Mcp.FunctionalTests`** (xUnit v3). eShop.Mcp runs in a `WebApplicationFactory`, and tests use the real MCP SDK client.
  - **Token issuer faked.** JWT validation trusts a test signing key, and tests mint caller tokens with chosen `sub`, audiences and expiry. The configured eShop.Mcp URL is a test value.
  - **Outbound HTTP faked.** The `HttpMessageHandler` behind the Ordering, Catalog and policy-api clients, and behind the token-exchange call to Identity.API, is replaced (prior art: `WebhookTests`). It records each request and returns canned responses. The fake token endpoint returns an exchanged token per subject token, so tests can tell which token reached Ordering.
  - Cases:
    - No token → 401, and `WWW-Authenticate` carries `resource_metadata`. The metadata's `resource` is exactly the configured URL, also for a request on another host form. `authorization_servers` is exactly the configured Identity URL, and `scopes_supported` has no `orders`.
    - A token whose audience is `eshop-mcp` or `orders`, but not the configured URL → 401.
    - An expired token → 401.
    - `tools/list` returns exactly `search_policies`, `search_catalog`, `list_my_orders` and `get_order`, with the contract's parameter names.
    - `get_order` for Buyer A:
      - The exchange request carries Buyer A's token as `subject_token`, the token-exchange grant type and `scope=orders`, and authenticates as `eshop-mcp-server`.
      - Ordering receives the exchanged token as Bearer, never Buyer A's token.
      - Calls with Buyer A's and Buyer B's tokens, made one after the other on the same client, each exchange their own token. Ordering receives each Buyer's exchanged token.
    - Two order-tool calls with the same caller token make two exchange requests (no cache).
    - The exchange refused (400 `invalid_grant`) → `list_my_orders` and `get_order` return `sign_in_required`, and Ordering receives no request.
    - The token endpoint answers 5xx → `unavailable`, and Ordering receives no request.
    - Ordering's 404 comes back as the `not_found` error result.
    - `search_policies` and `search_catalog` reach their backends with no `Authorization` header and trigger no exchange.
    - Catalog results through eShop.Mcp have no `pictureUrl`.
  - These replace the existing tests that forward the caller's token and check the `orders` audience. Delete those in the same change.
  - Each tool's result shape is not tested again here. It is covered by the library's tests at the `AIFunction` seam.
- **Token exchange: functional tests in a new `Identity.FunctionalTests`** (xUnit v3), which is the first test project for Identity.API.
  - The real Identity.API runs in a `WebApplicationFactory` with a real Postgres container through Aspire, like the other functional tests, with the seeded users. A real database is needed because the profile service looks up the Buyer on every issued token.
  - Subject tokens are minted inside the test host with Identity.API's own token tools (real signing key), with chosen `sub`, audience, scope and expiry. No interactive sign-in is needed.
  - Requests go to `/connect/token` over HTTP. Assertions decode the issued token.
  - Cases:
    - A valid exchange for `alice` → `sub` is alice's, `aud` is `orders`, `scope` is `orders`, `act.client_id` is `eshop-mcp-server`, the lifetime is 5 minutes, and there is no refresh token.
    - Each of these is refused, each with exactly one thing wrong:
      - a subject token whose audience isn't eShop.Mcp's URL;
      - a subject token without the `eshop-mcp` scope;
      - an expired subject token;
      - a subject token signed by another key;
      - a request from `claude-code`;
      - a wrong `eshop-mcp-server` secret;
      - a requested scope other than `orders`;
      - a subject whose user doesn't exist.
  - Optional, if a sign-in cookie can be obtained without driving the real login UI: an authorize request from `claude-code` with `resource=<MCP URL>` is not refused with `invalid_target`. Otherwise the manual acceptance check covers it.
- **AppHost wiring: fast tests in `eShop.AppHost.UnitTests`** (prior art: `AppHostConfigurationTests` and the eShop.Mcp wiring tests).
  - `eshop-mcp` exists and references policy-api, catalog-api and ordering-api.
  - Its `Identity__Url` is the same endpoint Ordering.API gets.
  - Identity.API and eShop.Mcp receive the same eShop.Mcp URL, for both the https profile and `ESHOP_USE_HTTP_ENDPOINTS=1`.
  - Identity.API and eShop.Mcp receive the same `eshop-mcp-server` secret parameter.
- **Public client configuration: no automated test of its own.** The redirect URIs and allowed scopes are configuration, and a test would only repeat the configured values. The manual acceptance check exercises them.
- **Acceptance check: manual, not in CI.** It runs after the Shopping Assistant's acceptance check passes, since that is the gate.
  1. `aspire run` with `ESHOP_USE_HTTP_ENDPOINTS=1` and `EmbeddingProvider=Ollama`. No Chat Provider is needed, because the client's own model answers.
  2. Connect Claude Code with the documented command. Sign in as `alice` in the browser and approve the consent screen. Expected: no `invalid_target`.
  3. Place an order containing a Navigation item and a non-navigation item in WebApp, and ship it through Ordering.API's ship endpoint.
  4. In Claude Code, ask "what's the return window for my last order?" Expected: 30 days for the Navigation item and 60 days for the rest, with end dates counted from the Order Date, citing *Returns Exceptions › GPS watches, navigators and bike computers* and *Returns Policy › Return window*. The Aspire traces show a token-exchange call before each Ordering call.
  5. Ask about one of `bob`'s order numbers. Expected: treated as not found.
  6. Reconnect Claude Code. Expected: no new consent prompt.
  7. Repeat step 4 from Copilot in VS Code, signing in as `alice`.
  8. Repeat step 4 with `EmbeddingProvider=None`, which is keyword mode, and expect the same Citations.
- **CI** runs with no Chat Provider, no embedding provider and no interactive login. No test calls an LLM.

## Out of Scope

- Claude Desktop and claude.ai custom connectors, and any public tunnel.
- Dynamic Client Registration and Client ID Metadata Documents at Identity.API.
- Caching exchanged tokens.
- Exchanged tokens for anything other than Ordering (Policy and catalog calls send no token).
- A Duende licence key, and deploying this outside development.
- Supporting clients that don't send a resource indicator, or a second `eshop-mcp`-named resource for them.
- Tools that work before sign-in (lazy authentication). In this spec, every tool needs a signed-in Buyer.
- Turning on audience validation in the shared service defaults. That's a hardening ticket of its own.
- MCP resources, prompts, sampling, elicitation and notifications.
- Write tools, such as adding to the basket or cancelling an order.
- Structured tool output (`outputSchema`).
- Any change to the tool contract beyond `pictureUrl`.
- Deployment of eShop.Mcp to Azure Container Apps.
- The MAUI ClientApp and HybridApp.

## Further Notes

- **Depends on** the shared tools library from the assistant-policies-and-orders spec (build order step 2). The two specs share the tool contract: change it only on purpose, and update both specs when you do.
- **Already built.** Tickets 01 and 03 implemented the first design: an `eshop-mcp`-named resource, `orders` allowed for the public clients, and the caller's token forwarded when it names `orders`. The `orders` audience check in eShop.Mcp's Ordering handler is replaced by the exchange. The resource rename, the clients' scopes and the metadata change replace the corresponding parts of 01 and 02.
- **`ValidateAudience = false`** in the shared service defaults means Ordering, Basket and Webhooks accept any token Identity.API issues. This design doesn't rely on that: Ordering receives only tokens whose audience is `orders`.
- **Duende trial mode.** A future Duende version is expected to stop at the 500-request trial limit instead of logging. Check this when upgrading Duende.
- **Unverified:** that Duende's exact redirect matching accepts the Claude Code redirect URIs as registered on a fixed port. The sign-in failed before the redirect in the first manual check. If Claude Code ignores the fixed port in some version, a loopback-aware redirect validator for these two clients is the fallback. Record it in ADR 0005 if it's needed.
- **Unverified:** the VS Code redirect URIs, and whether VS Code sends a resource indicator. Check them on first connection.
- **Claude Code TLS bug.** On Claude Code 2.1.286, the MCP HTTP transport's reconnect path ignored `NODE_EXTRA_CA_CERTS` (ticket 02). That is why plain HTTP is the documented path.
- **Session length.** Buyer access tokens last two hours, like the other clients. Refresh tokens keep Buyers signed in for longer. Exchanged tokens last five minutes.
- `TreatWarningsAsErrors` is on, and the new projects follow it.
