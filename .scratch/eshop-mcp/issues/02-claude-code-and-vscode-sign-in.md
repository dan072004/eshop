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

**Blocked by:** 01 (eShop.Mcp serves `search_catalog`), 05 (clients sign in with eShop.Mcp's URL as the resource)

**Status:** done

- [x] The `eshop-mcp` scope and the two clients are added through configuration next to the existing clients, with the existing clients unchanged
- [x] Manual: from Claude Code, the documented command opens the Identity.API login. Signing in as alice and approving consent succeeds, and a catalog search returns products
- [x] Manual: reconnecting doesn't ask for consent again
- [x] Manual: the same flow from Copilot in VS Code, or the actual VS Code redirect URIs recorded and the configuration corrected
- [x] The results for both unverified assumptions are recorded in the ticket's comments, and in ADR 0005 if the fallback was needed
- [x] The client setup documentation exists where developers will find it

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

**Manual check, Claude Code 2.1.286 (2026-11-03): sign-in blocked, first by TLS, then by `invalid_target`.**

TLS. The documented `NODE_EXTRA_CA_CERTS` fix does not work on this version:
- The dev cert served on 7229 is valid (signature, SANs, expiry 2027-09-22) and trusted in `CurrentUser\Root`.
- First error, `CERT_SIGNATURE_FAILURE`: the Azure Cosmos DB Emulator certs (`DocumentDbEmulatorCertificate`, `CosmosEmulatorSecrets`) are also self-signed `CN=localhost` roots without key identifiers. Claude Code's runtime picked one of them as the dev cert's issuer. Reproduced with Bun 1.4.4 `--use-system-ca`, and cleared by removing both from the trusted roots. Any developer with the emulator installed hits this.
- Then `UNABLE_TO_VERIFY_LEAF_SIGNATURE`, with `NODE_EXTRA_CA_CERTS` set. The debug log (`claude --debug mcp get eshop`) confirms the PEM was appended. The failure comes after `version negotiation probe failed on the http transport; reconnecting pinned legacy`, so that reconnect path appears to ignore the custom CA list. Bun alone, with the same environment, connects fine (401). This looks like a Claude Code bug.
- The `ESHOP_USE_HTTP_ENDPOINTS=1` fallback works, but only when the whole AppHost runs on HTTP. With eShop.Mcp on `http://localhost:5229/mcp` alone, the OAuth calls to Identity.API on `https://localhost:5243` fail the same way.
- The README's "Trusting the .NET dev certificate" step needs correcting: make `ESHOP_USE_HTTP_ENDPOINTS=1` the primary path for Claude Code, and mention the Cosmos emulator conflict.

Sign-in. With everything on HTTP, the browser reaches Identity.API, which answers `invalid_target` / "Invalid resource indicator" (request id `0HNPPT1SFJ12I:00000001`):
- Claude Code sends an RFC 8707 `resource` parameter, as the MCP authorization spec requires. Its value is the `resource` from eShop.Mcp's protected resource metadata, i.e. the server URL `http://localhost:5229/mcp` (`McpServerExtensions.cs`, `OnResourceMetadataRequest`).
- Duende accepts a `resource` only when it is the name of an `ApiResource`, and ours is named `eshop-mcp` (`Config.cs`). So no resource matches.
- This would fail on HTTPS (`https://localhost:7229/mcp`) as well. It is not caused by the HTTP fallback.
- Assumption 1 (fixed-port loopback redirect) and remembered consent are still unverified. The flow fails before the redirect.

This breaks two design assumptions:
- Identity.API must know eShop.Mcp's URL after all. The resource's name has to be the URL clients send, which depends on the http/https profile. That contradicts `aa8d7c1` ("Stop passing eShop.Mcp's URL to Identity.API") and the spec line it changed.
- The two-audience token from ADR 0005 is at risk. When a request names a resource, Duende restricts the access token to that resource. So `aud: orders`, which eShop.Mcp requires before forwarding to Ordering, is probably dropped. Unverified: probe it before choosing.

Options:
1. Name the `ApiResource` with the eShop.Mcp URL, passed in by the AppHost as before `aa8d7c1`, and have eShop.Mcp validate that URL as its audience. Then decide how Ordering gets a token naming `orders`: put the `orders` scope on the MCP resource and relax the forwarding rule, or use token exchange. Either way ADR 0005 changes. This is the recommended option, because it follows the MCP spec.
2. Advertise `eshop-mcp` as the metadata `resource`. It is not the canonical server URI the spec requires, and Claude Code may refuse metadata whose `resource` doesn't match the server URL. Not recommended.

Next step: probe Duende 7.3.2 with `resource=<url>` plus scopes `eshop-mcp orders` to see which audiences and scopes come back. Then choose an option and update ADR 0005, the spec and the README to match.

**Prototype (2026-11-03): Duende 7.3.2 with resource indicators.** `src/Identity.API.ResourceIndicatorPrototype` on branch `prototype/resource-indicators` (commit `2d9d463`, throwaway, not for merging; run it with `dotnet run --project src/Identity.API.ResourceIndicatorPrototype`) runs the real Duende pipeline in memory: sign-in, authorize with code and PKCE, code exchange, then refresh. Every request carries `resource=http://localhost:5229/mcp`, as Claude Code's do. The client mirrors `claude-code`, without consent.

| ApiResources | Authorize | Access token (code exchange and refresh agree) |
|---|---|---|
| A0: today's, no `resource` (baseline) | OK | `aud=[eshop-mcp, orders]` |
| A: today's (`eshop-mcp`, `orders`) | `invalid_target: Invalid resource indicator` (matches the manual check) | (none) |
| B: `<MCP URL>` [eshop-mcp], `orders` [orders] | OK | `aud=<MCP URL>`, `scope=[eshop-mcp, offline_access]`. `orders` is dropped |
| C: `<MCP URL>` [eshop-mcp, orders], `orders` [orders] | OK | `aud=<MCP URL>`, `scope=[eshop-mcp, orders, offline_access]`. There is no `aud: orders` |

Verdict:
- Naming the resource by its URL fixes the sign-in (B and C).
- With a spec-compliant client, the token names only the requested resource. No configuration yields `aud=[<MCP URL>, orders]`, so ADR 0005's single two-audience token is impossible.
- C gets the `orders` scope onto the token, but not the audience. Forwarding that token to Ordering is token passthrough (a token whose audience is eShop.Mcp, sent to another API). ADR 0005 rejected passthrough, and C would only work because ServiceDefaults sets `ValidateAudience = false`.
- The spec-clean path is B plus a token exchange (RFC 8693): eShop.Mcp swaps the caller's token for one with `aud: orders` and the same `sub`. In Duende that is a custom extension grant (`IExtensionGrantValidator`). This changes ADR 0005 and ticket 03's forwarding.
- Not checked: whether Duende's license edition covers resource isolation (resource indicators) and token exchange outside development. The prototype ran unlicensed in development mode.

**Replanned (2026-11-04).** The fix is ticket 05, which names Identity.API's resource for eShop.Mcp by its URL. The design is in the rewritten ADR 0005 and spec. With 05, `claude-code` and `vscode` lose the `orders` scope, and the two-audience token in this ticket's description no longer applies: order access goes through token exchange (06, 07). This ticket remains the manual sign-in check. After 05 lands, re-run it with `ESHOP_USE_HTTP_ENDPOINTS=1` and `http://localhost:5229/mcp`: sign in as alice, approve consent, run a catalog search, then reconnect without consent. Then run the VS Code flow and record both unverified assumptions here.

- **Claude Code sign-in passed after 05 (2026-11-04).** Over HTTP (`http://localhost:5229/mcp`, integration branch at `06cb2b2`), `/mcp` → Authenticate signed in and reported "Authentication successful. Connected to eshop", with no `invalid_target`. The user saw and approved the consent screen; that was expected, since before 05 no sign-in had ever succeeded, so there was no remembered consent. `search_catalog` returned products (`Alpine` → 4 results; with `EmbeddingProvider=None` it matches by name, so `hiking boots` returns none). `list_my_orders` also worked (see 07). Still open: reconnecting without a consent prompt, and the VS Code flow.
- **VS Code sign-in passed (2026-11-04).** User MCP configuration with `http://localhost:5229/mcp`. The first attempts failed with `unauthorized_client` ("Unknown client or client not enabled") because VS Code had remembered `https://vscode.dev/redirect` as the client ID. After removing the remembered entry (**Authentication: Remove Dynamic Authentication Providers**) and entering `vscode`, the consent screen showed "Visual Studio Code is requesting your permission" with your user identifier, user profile, "AdventureWorks MCP Server: search the store and read your orders" and offline access. No `orders` scope is requested, as 05 intends. The user approved it and the server connected. The README's VS Code section now shows the HTTP URL and the recovery step. Still open: a tool call from Copilot, and reconnecting without consent in both clients.
- **Claude Code reconnect (2026-11-04).** `/mcp` → Reconnect reported "Reconnected to eshop." without a sign-in or consent prompt, and `list_my_orders` then returned the same orders (#11, #1). This reused the stored tokens, so it shows reconnecting works, but not yet that Identity.API remembers consent on a fresh sign-in. That, and the same check in VS Code, are still open.
- **Consent on fresh sign-in is by design (2026-11-04).** After "Clear authentication" and Authenticate, the login page is skipped (Identity.API's session cookie is still in the browser; logging out brings it back), but the consent screen appears every time. Identity.API logs `ConsentRemembered: true`, then `Showing consent: User has not yet consented` on the next sign-in, because Duende 7.3.2 always requires consent when `offline_access` is requested ("Scopes contains offline_access, consent is required", in `Duende.IdentityServer.dll`). Reconnecting with the refresh token (`/mcp` → Reconnect) asks for nothing, which is what the criterion is for, so it is ticked. Recorded in ADR 0005's Consequences; the user accepted keeping `offline_access` over a custom consent service.
- **VS Code callback port, and closing the ticket (2026-11-04).** A later VS Code sign-in failed: Identity.API logged `Invalid redirect_uri: http://127.0.0.1:62116/` for client `vscode`, and the authorize request carried `prompt=consent` (a fresh sign-in, not a refresh). VS Code listens on `127.0.0.1:33418` when that port is free and falls back to a random port when it isn't, for example when another VS Code window or an unfinished sign-in holds it. Identity.API matches redirect URIs exactly, so only 33418 works. The workaround is to close other VS Code windows and start the server again. The real fix is the fallback this ticket names: accept any port on `http://127.0.0.1` for the `vscode` client, as RFC 8252 §7.3 asks of authorization servers. It isn't built and has no ticket yet.

  Assumption results: (1) Claude Code's fixed-port loopback redirect works with Duende's exact matching (`--callback-port 33419`). (2) VS Code's redirect URIs are right, but its port is fixed only while 33418 is free, so the fallback validator would make VS Code reliable. ADR 0005 is unchanged because the fallback wasn't built.

  The user closed the ticket: sign-in and consent from VS Code worked (see above). No eShop tool call from Copilot was checked, and neither was a VS Code reconnect. The `eshop-mcp` scope and both clients were added in configuration by this ticket with the existing clients unchanged (first comment), and the setup docs are in the README's "Optional: Connect Claude Code or VS Code".

- **`https://vscode.dev/redirect` removed (2026-11-04).** The low finding above said to remove it if VS Code desktop doesn't use it. The failed sign-in above asked for `http://127.0.0.1:62116/`, a loopback redirect, so desktop VS Code signs in through loopback. `vscode` now has only `http://127.0.0.1:33418` and `http://127.0.0.1:33418/`. Not yet checked: a VS Code sign-in after the removal.
