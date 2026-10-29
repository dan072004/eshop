# 04: Shopping Assistant answers Policy questions with Citations

**What to build:** A Buyer asks the Shopping Assistant a Policy question and gets an answer whose rules each carry a Citation (Policy title › Policy Section). When nothing relevant comes back, the assistant says so instead of guessing.

- The `search_policies(question)` tool goes in the shared library. It calls policy-api's search with `top` 5 and passes `mode` and `results` through unchanged. Its description carries the citation rule from the spec's tool contract, so any model that calls it is told how to cite.
- The library gets a policy-api typed client. It sends no token. WebApp registers it, and the AppHost makes WebApp reference policy-api.
- The Shopping Assistant's system prompt now mentions Policies and orders in its scope and repeats the citation rule as a backstop. The greeting changes from "AdventureWorks Concierge" to the Shopping Assistant.

**Blocked by:** 01 (shared tools library), 03 (policy-api with keyword Policy search)

**Status:** ready-for-agent

- [ ] `search_policies` is in the library with the contract's name, parameter name and description
- [ ] Fast tests at the `AIFunction` seam (fake outbound `HttpMessageHandler`): `citation` and `mode` are passed through as returned; a failure from policy-api becomes the `unavailable` error result
- [ ] The Shopping Assistant offers `search_policies` alongside its existing tools
- [ ] The greeting no longer says "Concierge"
- [ ] AppHost unit test: WebApp references policy-api
- [ ] Manual, with `ChatProvider=Claude` and `EmbeddingProvider=None`: "can I return a GPS watch after 45 days?" gets an answer citing *Returns Exceptions › GPS watches, navigators and bike computers*
- [ ] Manual: signed out, a Policy question still gets a cited answer
