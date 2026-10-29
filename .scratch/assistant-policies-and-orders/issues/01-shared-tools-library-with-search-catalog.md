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

- [x] The library and its unit test project exist, are in `eShop.Web.slnf`, and build with warnings as errors
- [x] `search_catalog(productDescription)` lives in the library with the same behaviour and result as the old catalog search tool
- [x] The Shopping Assistant's tool list uses the library's `search_catalog`. The user-info, add-to-basket and basket-contents tools stay in WebApp, unchanged
- [x] With WebApp's picture URL function supplied, results carry `pictureUrl` as today. With none supplied, `pictureUrl` is absent
- [x] Fast tests invoke the tool through its `AIFunction`, faking only the outbound `HttpMessageHandler` (prior art: `WebhookTests`). They assert the returned JSON values, the tool's name and parameter name, and the result when Catalog fails
- [x] Any test made redundant by the move is deleted in the same change
- [ ] Manual: with a Chat Provider on, catalog search in the Shopping Assistant still finds products

## Comments

**Names later tickets build on (04, 06, 07, eShop.Mcp):**
- Project `src/eShop.Assistant.Tools` (namespace `eShop.Assistant.Tools`), tests in `tests/eShop.Assistant.Tools.UnitTests` (MSTest.Sdk). No ASP.NET Core framework reference.
- DI: `services.AddAssistantTools()` returns `AssistantToolsBuilder` with `Services`, `CatalogClient` (an `IHttpClientBuilder`) and `WithProductPictureUrl(Func<IServiceProvider, ProductPictureUrl>)`. To add a client, register it in `AddAssistantTools` and expose its `IHttpClientBuilder` on the builder (e.g. `OrderingClient`, `PolicyClient`), so the host can call `.AddHttpMessageHandler<...>()` on the Ordering one.
- Typed clients live in per-backend folders (`Catalog/CatalogClient`, base address `https+http://catalog-api`, `api-version=2.0` added by `AddApiVersion`). The host must add service discovery (ServiceDefaults does).
- Tools: `AssistantTools` (transient), one `AIFunction` property per tool (`SearchCatalog`) plus `All` (every tool, in contract order). New tools add a property, an entry in `All` and a const in `AssistantToolNames`. Each is built with `AIFunctionFactory.Create(method, new AIFunctionFactoryOptions { Name, Description })`.
- Error results: `ToolError(Error, Message)` serialises to `{ error, message }`; `ToolError.Unavailable(...)` exists. Add `NotFound` / `SignInRequired` factories for the order tools.
- `ProductPictureUrl` is a delegate `string (int productId)`, optional in DI.

**Deliberate small differences from the old WebApp tool:** results are now camelCase JSON (`pageIndex`, `data[].pictureUrl`, ...), like every other tool in the contract, where the old tool returned PascalCase from `JsonSerializer.Serialize`. A Catalog failure returns `{ "error": "unavailable", "message": "Error accessing catalog." }` instead of the bare string, following the spec's error-result shape. The item fields are unchanged (id, name, description, price, pictureUrl, catalogBrandId, catalogBrand, catalogTypeId, catalogType); the catalog client no longer forwards the user's token, because the search endpoint is anonymous.

No existing test covered the old `SearchCatalog`, so none was deleted.
