# 06: Shopping Assistant lists the Buyer's orders

**What to build:** A signed-in Buyer asks "what's my last order?" and the Shopping Assistant answers from their own orders. A signed-out Buyer is told to sign in.

- **First, show the token problem (or show there is none).** The Shopping Assistant runs as an interactive Server component, where `HttpContext` isn't reliably available, so the existing token-forwarding handler may send no token. Record what actually happens in the ticket's comments.
- **Token holder.** While the page is still rendered on the server, WebApp reads the Buyer's access token into a scoped holder the interactive session can use (Microsoft's documented pattern). A `DelegatingHandler` adds it as a Bearer token, and sends no header when the holder is empty.
- **Library.** The library gets an Ordering.API (v1) typed client. The host attaches its own handler to it; the library has no auth code. It also gets `list_my_orders()`, returning `{ today, orders: [ { orderNumber, orderDate, status, total } ] }` newest-first, as Ordering returns them. Errors are tool output: 401 or no token → `sign_in_required`, anything else → `unavailable`.
- **No ownership logic in the tool.** Ordering.API's endpoint decides whose orders come back.

If the basket client turns out to have the same token problem, raise it as its own ticket.

**Blocked by:** 01 (shared tools library), 02 (Ordering newest-first)

**Status:** ready-for-agent

- [x] Fast tests in `WebApp.UnitTests`: holder filled → the outbound request carries `Authorization: Bearer <token>`; holder empty → no header. Only the inner handler is faked
- [x] Fast tests at the `AIFunction` seam (fake outbound `HttpMessageHandler`): orders and `today` are returned as Ordering sent them; 401 → `sign_in_required`; 500 → `unavailable`; the contract's name, with no parameters
- [x] The Shopping Assistant offers `list_my_orders`, with the token holder attached to the library's Ordering client
- [ ] Manual: signed in as alice with two orders, "what's my last order?" names the newest one
- [ ] Manual: signed out, the assistant says to sign in
- [x] The finding about the current token behaviour is recorded in the ticket's comments

## Comments

**Current token behaviour (the "show the token problem first" step).** Not observed in a running chat: the implementing agent could not drive the interactive Shopping Assistant, so this is reasoned from the code and Microsoft's docs, not seen.
- The existing `AddAuthToken()` handler (`eShop.ServiceDefaults/HttpClientExtensions.cs`) reads `IHttpContextAccessor.HttpContext` and calls `GetTokenAsync("access_token")`. In an Interactive Server circuit, that `HttpContext` is the one captured when the SignalR connection started; it carries the auth cookie, so the token is most likely found today.
- Microsoft's current docs (Blazor "additional server-side security scenarios", .NET 10, updated 2026-10-08) say this is *supported* "if the tokens are obtained during static SSR or prerendering", with two caveats: tokens aren't updated if the user signs in after the circuit started, and `IHttpContextAccessor` uses `AsyncLocal`, so the execution context must not be lost before reading it. Sign-in here is a full OIDC redirect, which starts a new circuit, so the first caveat doesn't bite.
- So the likely finding is **no failure today**; the risk is that it rests on an `AsyncLocal` `HttpContext` that the docs elsewhere warn against relying on in interactive components. The basket gRPC client (`AddToCart`) uses the same handler and should behave the same; no separate basket ticket raised, because no failure was shown.
- The token holder was still built as the ticket and spec require. It reads the token in the statically rendered `ShowChatbotButton` (valid `HttpContext`) and passes it to the interactive `Chatbot` as a parameter (Blazor data-protects interactive Server component parameters in the page). This is the `TokenProvider`/initial-state pattern from the same docs page. The handler finds the circuit's holder through `CircuitServicesAccessor` (Microsoft's "different DI scope" pattern), because `IHttpClientFactory` builds handlers in its own scope.
- To actually observe it: during the manual check, temporarily switch the Ordering client to `.AddAuthToken()` instead of `.AddAccessTokenFromHolder()` and ask "what's my last order?" signed in. If it lists the orders, the old handler works in the circuit.

**For ticket 07 (`get_order`)** — names to build on:
- Typed client `eShop.Assistant.Tools.Ordering.OrderingClient` (`BaseAddress = "https+http://ordering-api"`, API v1), exposed as `AssistantToolsBuilder.OrderingClient` (a `required init` property set in `AddAssistantTools`). It throws `HttpRequestException` with `StatusCode` set (via `GetFromJsonAsync`); add a `GetOrderAsync(int, CancellationToken)` next to `GetMyOrdersAsync`.
- Order tools live in `AssistantTools.Ordering.cs` (`AssistantTools` is now `partial`). `OrderingError(Exception, CancellationToken)` maps 401 → `ToolError.SignInRequired(...)`, everything else → logged `ToolError.Unavailable("Error accessing orders.")`, and rethrows the caller's own cancellation. Add the 404 → `not_found` case there (`ToolError.NotFound` doesn't exist yet).
- `today` comes from the `Today` property (UTC date from the injected `TimeProvider`; `AddAssistantTools` does `TryAddSingleton(TimeProvider.System)`). Serialised as `"yyyy-MM-dd"` (`DateOnly`).
- WebApp: `assistantTools.OrderingClient.AddAccessTokenFromHolder()` in `Extensions.AddApplicationServices` (`eShop.WebApp.Services.AccessTokens`). Nothing more is needed in WebApp for `get_order` beyond adding the tool to `ChatState`'s list.

The system prompt and greeting were not changed by this ticket.
