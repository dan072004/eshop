# Spec: eShop.Mcp, the store's tools for local MCP clients

Status: ready-for-agent

## Problem Statement

A Buyer who works in Claude Code or GitHub Copilot in VS Code can't ask those assistants about AdventureWorks. To learn the Return Window for their last order, they have to leave the editor, open the store, find the order, open the Policies and count days themselves.

- **The store's abilities live only inside WebApp.** Catalog search, Policy search and order lookup are being built as Shopping Assistant tools. No other AI client can call them.
- **An outside client has no way to say who the Buyer is.** WebApp signs the Buyer in with a cookie. An MCP client has no cookie, and Ordering.API only shows orders to the Buyer whose token it receives.
- **A shortcut would leak orders.** Wiring an MCP server to a fixed test user would answer "my last order" with someone else's order for every caller.
- **Claude Desktop can't reach a local server.** Its custom connectors call MCP servers from Anthropic's cloud, so a server and an Identity.API running on a laptop are unreachable from there.

## Solution

A new Aspire project, eShop.Mcp, exposes the same four tools the Shopping Assistant uses (Policy search, catalog search, list my orders, get one order) as an MCP server over HTTP. Claude Code and Copilot in VS Code connect to it on localhost.

- **The Buyer signs in themselves.** On first use, the client opens Identity.API's login page in the browser. The Buyer signs in and approves the client on a consent screen. From then on, every tool call carries that Buyer's token.
- **"My orders" means the signed-in Buyer's orders, and nothing else.** No tool accepts a user id. Ordering.API decides what the Buyer may see, and an order that isn't theirs is reported as not found.
- **Answers carry Citations.** The tools and their descriptions come from the shared tools library, so any model that calls them is told to state Policy rules only from results and to cite each one.
- **The server is a thin host.** It does only hosting, sign-in and tool registration. Composition lives in the shared library, and rules live in the Policies and in Ordering.API.

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
9. As a Buyer, I want a consent screen that names the client and what it will read, so that I know what access I'm granting to an AI client.
10. As a Buyer, I want my consent remembered, so that I'm not asked again on every connection.
11. As a Buyer, I want my session to keep working without signing in every two hours, so that long sessions aren't interrupted.
12. As a Buyer, I want an order number that isn't mine to be treated as unknown, so that nobody can read my orders through an AI client.
13. As a Buyer, I want to be told to sign in again when my sign-in has expired or been revoked, so that I understand why order questions stopped working.
14. As a Buyer, I want the AI to be told when the store is unavailable, rather than given an empty result, so that it doesn't tell me I have no orders.
15. As a maintainer, I want no tool to accept a user id or email, so that a model can't be talked into asking for another Buyer's orders.
16. As a maintainer, I want eShop.Mcp to forward only the caller's own token, and only after checking it was issued for eShop.Mcp, so that a token meant for another service can't be replayed through it.
17. As a maintainer, I want every tool call authorised by the token on that request, so that one Buyer's calls can never run with another Buyer's identity.
18. As a maintainer, I want order ownership still checked only in Ordering.API's endpoints, so that there's one place that decides it.
19. As a maintainer, I want the MCP tools to be the shared library's tools, wrapped unchanged, so that MCP and WebApp can't drift apart.
20. As a maintainer, I want the tool names and parameters exactly as the shared contract defines them, so that both hosts expose one contract.
21. As a maintainer, I want eShop.Mcp to contain no business rules, so that a rule change never needs an MCP release.
22. As a maintainer, I want no existing service to change behaviour, so that this work can't break WebApp, mobile-bff or the APIs.
23. As a maintainer, I want the new Identity.API clients added next to the existing ones, through configuration only, so that existing clients aren't touched.
24. As a maintainer, I want the reasons for this sign-in design recorded in ADR 0005, so that nobody "simplifies" it into token passthrough or a fixed user.
25. As a developer, I want eShop.Mcp to appear in the Aspire dashboard with its health check, logs and traces, so that I can watch tool calls.
26. As a developer, I want eShop.Mcp on a fixed localhost port, so that my client configuration keeps working across restarts.
27. As a developer, I want eShop.Mcp to follow the same http/https switch as the other front ends, so that its tokens have the same issuer every API expects.
28. As a developer, I want the server to tell clients where to sign in (protected resource metadata), so that Claude Code and VS Code can start sign-in with no manual URLs.
29. As a developer, I want a documented `claude mcp add` command, so that I can connect Claude Code in one step.
30. As a developer, I want a documented VS Code configuration, so that I can connect Copilot in one step.
31. As a developer, I want to know how to make Claude Code trust the local HTTPS certificate, with plain HTTP as the documented fallback, so that a TLS error doesn't block the demo.
32. As a developer, I want Policy and catalog tools to keep working when no embedding provider is configured, so that a fresh clone can run the demo.
33. As a developer, I want catalog results without WebApp-relative picture paths, so that the model isn't given links that lead nowhere.
34. As a maintainer, I want the server's sign-in and token forwarding tested through the real MCP protocol, so that what's tested is what clients use.
35. As a maintainer, I want CI to need no Identity.API login, no LLM and no MCP client app, so that tests stay fast and deterministic.
36. As a maintainer, I want a manual acceptance check with real sign-in in Claude Code, so that the whole chain is proven once per change.

## Implementation Decisions

**Decisions recorded in ADR 0005** (read it before changing any of them): per-Buyer OAuth through Identity.API; pre-registered public PKCE clients; one token naming both `eshop-mcp` and `orders`; stateless sessions; local clients only.

**eShop.Mcp (new project)**

- New ASP.NET Core project, Aspire resource `eshop-mcp`, added to `eShop.Web.slnf`, with the standard service defaults and health check.
- Uses `ModelContextProtocol.AspNetCore` (2.3.0, version in central package management) with the Streamable HTTP transport, in **stateless** mode. The MCP endpoint is mapped at `/mcp` and requires authorisation. The health endpoints stay anonymous.
- **Tools.** The four tools come from the shared tools library (`eShop.Assistant.Tools`, specified in the assistant-policies-and-orders spec). Each `AIFunction` is wrapped with `McpServerTool.Create(AIFunction, …)`, keeping its name, description and parameters. eShop.Mcp adds no tools, resources or prompts, and does not rename or prefix them.
  - `search_policies(question)`, `search_catalog(productDescription)`, `list_my_orders()`, `get_order(orderNumber)`.
  - Results are returned as the JSON text the library produces, with no `outputSchema` and no `structuredContent`.
  - Error results stay tool output, as the contract defines: `not_found`, `sign_in_required`, `unavailable`.
  - `get_order`: a product Catalog.API doesn't know gets `null` `type` and `brand`; a Catalog.API failure returns `unavailable` for the whole call, so no Return Window is given without item types.
- **Catalog picture URLs (contract change, already written into the other spec).** The library lets the host supply a product-id → image URL function. eShop.Mcp supplies none, so `pictureUrl` is left out of its catalog results.
- **Authentication.**
  - JWT bearer authentication against Identity.API, configured by eShop.Mcp itself rather than through the shared `AddDefaultAuthentication`, because it must **validate the audience** (`eshop-mcp`). The shared default turns audience validation off.
  - Valid issuer: the Identity URL the AppHost injects, the same value Ordering.API uses.
  - `SaveToken` is on, so the existing token-forwarding handler (`AddAuthToken`) can read the validated token from the request.
  - Protected resource metadata (RFC 9728) through the SDK's MCP authentication handler. `resource` is eShop.Mcp's own URL, exactly as clients enter it. `authorization_servers` holds only the injected Identity URL. `scopes_supported` lists `openid`, `profile`, `offline_access`, `eshop-mcp` and `orders`.
  - An unauthenticated or rejected request gets HTTP 401 with `WWW-Authenticate: Bearer resource_metadata="…"`.
- **Outbound HTTP.** The library's typed clients reach `policy-api`, `catalog-api` (v2) and `ordering-api` (v1) by Aspire service name. eShop.Mcp attaches `AddAuthToken()` to the Ordering client only. The Policy and catalog clients send no token.
- eShop.Mcp has no database, no event bus subscription and no state of its own.

**Identity.API (configuration only)**

- New API resource and scope `eshop-mcp`.
- Two new clients, with no change to existing ones:
  - `claude-code`: public client (no secret), authorization code with PKCE (S256), consent required and remembered, offline access with refresh-token rotation. Redirect URIs `http://localhost:{port}/callback` and `http://127.0.0.1:{port}/callback` on one fixed port (the earlier Claude Code release used the `127.0.0.1` form).
  - `vscode`: the same settings, with redirect URIs `http://127.0.0.1:33418` and `https://vscode.dev/redirect`.
  - Allowed scopes for both: `openid`, `profile`, `offline_access`, `eshop-mcp`, `orders`.
- The Claude Code callback port is a setting with a default, so a developer whose port is taken can change it in one place.
- The resulting access token names both `eshop-mcp` and `orders` as audiences. Ordering.API needs no change to accept it.

**AppHost**

- Add `eshop-mcp` with references to `policy-api`, `catalog-api` and `ordering-api`. It waits for Identity.API and receives `Identity__Url` set to the same Identity endpoint as the APIs.
- It uses the shared http/https launch-profile switch (`ESHOP_USE_HTTP_ENDPOINTS`), like WebApp. Its launch settings fix its ports, so client configuration stays valid.
- It is not marked for external endpoints and not added to mobile-bff. Its clients run on the same machine.
- Identity.API receives eShop.Mcp's URL, the same way it receives the other client URLs. That adds eShop.Mcp to the existing deliberate reference cycle.

**Client setup (developer documentation)**

- Claude Code: `claude mcp add --transport http --client-id claude-code --callback-port <port> eshop <eShop.Mcp URL>/mcp`.
- VS Code: an HTTP server entry in `.vscode/mcp.json`. When VS Code asks for a client id, enter `vscode`.
- HTTPS: export the .NET dev certificate and point `NODE_EXTRA_CA_CERTS` at it for Claude Code. Fallback: run with `ESHOP_USE_HTTP_ENDPOINTS=1`.
- Claude Desktop is not supported (ADR 0005).

**Domain language**

The MCP caller is a **Buyer**. eShop.Mcp is not the **Shopping Assistant**, which is WebApp's helper. Policy, Policy Section, Citation, Return Window and Order Date keep their `GLOSSARY.md` meanings.

## Testing Decisions

- **What makes a good test here:** drive eShop.Mcp through its public seam, the MCP endpoint over HTTP, using the real MCP SDK client. Assert on what comes out: the HTTP status and headers, the tool list, the tool result, and the request that reached the backend. Fake only boundaries we don't own (ADR 0002). Every test asserts values, never only that something non-empty came back (ADR 0003).
- **MCP endpoint: functional tests in a new `Mcp.FunctionalTests`** (xUnit v3). eShop.Mcp runs in a `WebApplicationFactory`.
  - **Token issuer faked.** JWT validation trusts a test signing key, and tests mint tokens with chosen `sub`, audiences and expiry. Identity.API is not started: getting a real Buyer token needs the interactive code flow, and enabling a password grant only for tests would change an existing service.
  - **Outbound HTTP faked.** The `HttpMessageHandler` behind the Ordering, Catalog and policy-api clients is replaced (prior art: `WebhookTests`). It records each request and returns canned responses.
  - Cases:
    - No token → 401, and `WWW-Authenticate` carries `resource_metadata`. The metadata document's `resource` is the server URL, and `authorization_servers` is exactly the configured Identity URL.
    - A token signed by the test key but without the `eshop-mcp` audience (for example only `orders`) → 401. Ordering would accept that token; eShop.Mcp must not.
    - An expired token → 401.
    - `tools/list` returns exactly `search_policies`, `search_catalog`, `list_my_orders` and `get_order`, with the contract's parameter names.
    - `get_order` for Buyer A reaches Ordering with Buyer A's token as Bearer. Calls with Buyer A's and Buyer B's tokens, made one after the other on the same client, each reach Ordering with their own token.
    - Ordering's 404 comes back as the `not_found` error result.
    - `search_policies` and `search_catalog` reach their backends with no `Authorization` header.
    - Catalog results through eShop.Mcp have no `pictureUrl`.
  - Each tool's result shape is not tested again here. It is covered by the library's tests at the `AIFunction` seam.
- **AppHost wiring: fast tests in `eShop.AppHost.UnitTests`** (prior art: `AppHostConfigurationTests`).
  - `eshop-mcp` exists and references policy-api, catalog-api and ordering-api.
  - Its `Identity__Url` is the same endpoint Ordering.API gets.
  - Identity.API receives eShop.Mcp's URL.
  - It uses the http launch profile when `ESHOP_USE_HTTP_ENDPOINTS=1`.
- **Identity.API clients: no automated test.** They are configuration, Identity.API has no test project, and a test would only repeat the configured values. The manual acceptance check exercises them.
- **Acceptance check: manual, not in CI.** It runs after the Shopping Assistant's acceptance check passes, since that is the gate.
  1. `aspire run` with `EmbeddingProvider=Ollama`. No Chat Provider is needed, because the client's own model answers.
  2. Connect Claude Code with the documented command. Sign in as `alice` in the browser and approve the consent screen.
  3. Place an order containing a Navigation item and a non-navigation item in WebApp, and ship it through Ordering.API's ship endpoint.
  4. In Claude Code, ask "what's the return window for my last order?" Expected: 30 days for the Navigation item and 60 days for the rest, with end dates counted from the Order Date, citing *Returns Exceptions › GPS watches, navigators and bike computers* and *Returns Policy › Return window*.
  5. Ask about one of `bob`'s order numbers. Expected: treated as not found.
  6. Repeat step 4 from Copilot in VS Code, signing in as `alice`.
  7. Repeat step 4 with `EmbeddingProvider=None`, which is keyword mode, and expect the same Citations.
- **CI** runs with no Chat Provider, no embedding provider and no Identity.API login. No test calls an LLM.

## Out of Scope

- Claude Desktop and claude.ai custom connectors, and any public tunnel.
- Dynamic Client Registration and Client ID Metadata Documents at Identity.API.
- Token exchange for downstream tokens.
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
- **`ValidateAudience = false`** in the shared service defaults means Ordering, Basket and Webhooks accept any token Identity.API issues. This design doesn't rely on that. eShop.Mcp validates its own audience, and the forwarded token names `orders` anyway.
- **Unverified:** that Duende's redirect validation, with exact matching, accepts the Claude Code redirect URIs as registered on a fixed port. If Claude Code ignores the fixed port in some version, a loopback-aware redirect validator for these two clients is the fallback. It counts as a change to Identity.API, so record it in ADR 0005 if it's needed.
- **Unverified:** the VS Code redirect URIs were taken from third-party setup guides, not VS Code's own documentation. Check them on first connection.
- **Session length.** Access tokens last two hours, like the other clients. Refresh tokens keep Buyers signed in for longer.
- `TreatWarningsAsErrors` is on, and the new projects follow it.
