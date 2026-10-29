# 01: Shared tools library, starting with `search_catalog`

**What to build:** A prefactor that changes no behaviour. Create the shared tools library (working name `eShop.Assistant.Tools`) and its fast test project, and move catalog search out of the Shopping Assistant's conversation class into it as the `search_catalog` tool. The Shopping Assistant then uses the library's tool. Buyers see catalog search in the chat work exactly as before.

The library is shaped the way the spec's tool contract describes, so the tickets after this one only add tools:
- Tools are `AIFunction`s with explicit snake_case names and descriptions.
- The library has typed HTTP clients addressed by Aspire service name, and the host registers them.
- The library knows nothing about `ClaimsPrincipal`, `HttpContext` or Blazor.
- The host may supply a product-id → picture URL function. WebApp supplies its current one; with none supplied, `pictureUrl` is left out of results (agreed in the eShop.Mcp grilling).

See the spec's "Shared tools library" section.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] The library and its unit test project exist, are in `eShop.Web.slnf`, and build with warnings as errors
- [ ] `search_catalog(productDescription)` lives in the library with the same behaviour and result as the old catalog search tool
- [ ] The Shopping Assistant's tool list uses the library's `search_catalog`. The user-info, add-to-basket and basket-contents tools stay in WebApp, unchanged
- [ ] With WebApp's picture URL function supplied, results carry `pictureUrl` as today. With none supplied, `pictureUrl` is absent
- [ ] Fast tests invoke the tool through its `AIFunction`, faking only the outbound `HttpMessageHandler` (prior art: `WebhookTests`). They assert the returned JSON values, the tool's name and parameter name, and the result when Catalog fails
- [ ] Any test made redundant by the move is deleted in the same change
- [ ] Manual: with a Chat Provider on, catalog search in the Shopping Assistant still finds products
