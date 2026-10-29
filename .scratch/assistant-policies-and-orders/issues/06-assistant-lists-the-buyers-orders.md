# 06: Shopping Assistant lists the Buyer's orders

**What to build:** A signed-in Buyer asks "what's my last order?" and the Shopping Assistant answers from their own orders. A signed-out Buyer is told to sign in.

- **First, show the token problem (or show there is none).** The Shopping Assistant runs as an interactive Server component, where `HttpContext` isn't reliably available, so the existing token-forwarding handler may send no token. Record what actually happens in the ticket's comments.
- **Token holder.** While the page is still rendered on the server, WebApp reads the Buyer's access token into a scoped holder the interactive session can use (Microsoft's documented pattern). A `DelegatingHandler` adds it as a Bearer token, and sends no header when the holder is empty.
- **Library.** The library gets an Ordering.API (v1) typed client. The host attaches its own handler to it; the library has no auth code. It also gets `list_my_orders()`, returning `{ today, orders: [ { orderNumber, orderDate, status, total } ] }` newest-first, as Ordering returns them. Errors are tool output: 401 or no token → `sign_in_required`, anything else → `unavailable`.
- **No ownership logic in the tool.** Ordering.API's endpoint decides whose orders come back.

If the basket client turns out to have the same token problem, raise it as its own ticket.

**Blocked by:** 01 (shared tools library), 02 (Ordering newest-first)

**Status:** ready-for-agent

- [ ] Fast tests in `WebApp.UnitTests`: holder filled → the outbound request carries `Authorization: Bearer <token>`; holder empty → no header. Only the inner handler is faked
- [ ] Fast tests at the `AIFunction` seam (fake outbound `HttpMessageHandler`): orders and `today` are returned as Ordering sent them; 401 → `sign_in_required`; 500 → `unavailable`; the contract's name, with no parameters
- [ ] The Shopping Assistant offers `list_my_orders`, with the token holder attached to the library's Ordering client
- [ ] Manual: signed in as alice with two orders, "what's my last order?" names the newest one
- [ ] Manual: signed out, the assistant says to sign in
- [ ] The finding about the current token behaviour is recorded in the ticket's comments
