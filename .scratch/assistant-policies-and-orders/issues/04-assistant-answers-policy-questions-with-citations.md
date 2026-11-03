# 04: Shopping Assistant answers Policy questions with Citations

**What to build:** A Buyer asks the Shopping Assistant a Policy question and gets an answer whose rules each carry a Citation (Policy title › Policy Section). When nothing relevant comes back, the assistant says so instead of guessing.

- The `search_policies(question)` tool goes in the shared library. It calls policy-api's search with `top` 5 and passes `mode` and `results` through unchanged. Its description carries the citation rule from the spec's tool contract, so any model that calls it is told how to cite.
- The library gets a policy-api typed client. It sends no token. WebApp registers it, and the AppHost makes WebApp reference policy-api.
- The Shopping Assistant's system prompt now mentions Policies and orders in its scope and repeats the citation rule as a backstop. The greeting changes from "AdventureWorks Concierge" to the Shopping Assistant.

**Blocked by:** 01 (shared tools library), 03 (policy-api with keyword Policy search)

**Status:** done

- [x] `search_policies` is in the library with the contract's name, parameter name and description
- [x] Fast tests at the `AIFunction` seam (fake outbound `HttpMessageHandler`): `citation` and `mode` are passed through as returned; a failure from policy-api becomes the `unavailable` error result
- [x] The Shopping Assistant offers `search_policies` alongside its existing tools
- [x] The greeting no longer says "Concierge"
- [x] AppHost unit test: WebApp references policy-api
- [ ] Manual, with `ChatProvider=Claude` and `EmbeddingProvider=None`: "can I return a GPS watch after 45 days?" gets an answer citing *Returns Exceptions › GPS watches, navigators and bike computers*
- [ ] Manual: signed out, a Policy question still gets a cited answer

## Comments

- Implemented on `ticket/04-policy-tool`. Names later tickets and eShop.Mcp build on:
  - `eShop.Assistant.Tools.Policy.PolicyClient` (base address `https+http://policy-api`, no token, no api-version) with `SearchAsync(question, top, ct)` returning `PolicySearchResult(Mode, Results)` of `PolicySectionMatch(Citation, PolicySlug, PolicyTitle, Section, Anchor, Text, Score)`, mirroring policy-api's response.
  - `AssistantToolsBuilder.PolicyClient` (IHttpClientBuilder); `AssistantTools.SearchPolicies`; `AssistantToolNames.SearchPolicies`; the contract description is `AssistantTools.SearchPoliciesDescription`. `All` is in contract order: `[SearchPolicies, SearchCatalog]`; tickets 06/07 insert `ListMyOrders`, `GetOrder` between them.
  - A policy-api failure returns `{ "error": "unavailable", "message": "Error searching the Policies." }`.
- AppHost: the WebApp → policy-api reference moved from top-level `AppHost.cs` into `WithPolicySearch(policyApi)` in `src/eShop.AppHost/Extensions.cs`, because the unit tests can't reach top-level statements (no Aspire.Hosting.Testing). eShop.Mcp can use the same extension. Test: `WebAppReferencesPolicyApiByItsServiceName`.
- WebApp: `ShoppingAssistantTests` (WebApp.UnitTests) fakes only `IChatClient` and checks the offered tool names, the greeting and the citation backstop in the system prompt. Ticket 06 can add `list_my_orders` to its expected tool list.
- Manual acceptance items not run here (need `ChatProvider=Claude` and an API key).
