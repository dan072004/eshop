# Spec: Policy answers and order lookup in the Shopping Assistant

Status: ready-for-agent

## Problem Statement

A Buyer who asks the Shopping Assistant "what's the return window for my last order?" gets nothing useful back.

- **The assistant doesn't know the Policies.** AdventureWorks has 15 written Policies (returns, exchanges, shipping, warranty, sizing, payments and more), but the assistant has never seen them. It can refuse, guess, or invent a rule, and a Buyer can't tell which of those happened.
- **The assistant can't see orders.** Its tools cover the catalog, the basket and the Buyer's profile. It can't list the Buyer's orders, so it can't tell when the last one was placed, whether it has shipped, or what was in it.
- **Answers carry no source.** Even a correct answer doesn't say which Policy it came from, so the Buyer has no way to check it.
- **A second host is coming.** This week, eShop.Mcp will expose the same abilities to Claude Code and GitHub Copilot in VS Code. If policy search and order lookup are written inside the WebApp chat, the MCP server will have to write them again, and the two copies will drift.

## Solution

The Shopping Assistant can search the Policies and look up the Buyer's own orders, and every Policy rule it states comes with a Citation.

- **A new policy service** holds the Policies, split into Policy Sections, in its own database. It indexes them when it starts and searches them by meaning when an embedding provider is configured, or by keyword when none is. Policies are public, so the search needs no sign-in.
- **Three new assistant tools:** search Policies, list my orders, and get one order. The order tools call Ordering.API with the Buyer's token, and Ordering.API decides which orders the Buyer may see.
- **One shared tools library** holds these tools and catalog search, written so that WebApp's Shopping Assistant uses them now and eShop.Mcp can use them unchanged. Each host supplies only its own way of attaching the user's token.
- **Citations travel with the tool.** Every search result carries a ready-made Citation, such as "Returns Exceptions › GPS watches, navigators and bike computers", and the tool's description tells any model to state rules only from results and to cite each one. Citations then work in any model that calls the tool, including Claude Code, which never sees WebApp's system prompt.

For "what's the return window for my last order?" on a shipped order containing a navigation item, the assistant answers: 30 days for that item and 60 for the rest, with the end dates, citing *Returns Exceptions › GPS watches, navigators and bike computers* and *Returns Policy › Return window*.

## User Stories

1. As a Buyer, I want to ask the Shopping Assistant about returns, shipping, warranty, sizing and payments, so that I get answers without hunting through Policies.
2. As a Buyer, I want each rule the assistant states to come with a Citation (Policy title › Policy Section), so that I can check where it came from.
3. As a Buyer, I want the assistant to say when no Policy covers my question, instead of guessing, so that I'm never given an invented rule.
4. As a Buyer, I want to ask "what's the return window for my last order?", so that I know how long I have without working it out myself.
5. As a Buyer, I want the Return Window worked out per item, so that a 30-day navigation item in my order isn't given the standard 60 days.
6. As a Buyer, I want the assistant to give the actual end date of a Return Window, counted from the Order Date, so that I don't have to count days.
7. As a Buyer, I want to be told when my order can't be returned yet because it hasn't shipped, so that I cancel it instead if that's what I need.
8. As a Buyer, I want to ask what status my last order is in, so that I know whether it has been paid or shipped.
9. As a Buyer, I want to ask about a specific order by its number, so that I can check an older order.
10. As a Buyer, I want the assistant to list my recent orders newest-first, so that "my last order" means the one I placed most recently.
11. As a Buyer, I want the assistant to tell me what was in an order, so that I can ask follow-up questions about specific items.
12. As a Buyer, I want warranty answers that use my item's brand, so that I'm told the right warranty length.
13. As a Buyer, I want an order number that isn't mine to be treated as unknown, so that nobody can read my orders through the assistant either.
14. As a Buyer who isn't signed in, I want the assistant to tell me to sign in when I ask about my orders, so that I understand why it can't answer.
15. As a Buyer who isn't signed in, I want Policy questions to still work, so that I can read the rules before I buy.
16. As a Buyer, I want sizing questions answered from the sizing guides, including their size tables, so that I can pick a size.
17. As a Buyer, I want the assistant to combine an order and a Policy in one answer, so that I don't have to ask twice.
18. As a Buyer, I want the assistant to introduce itself as the Shopping Assistant, so that its name matches the rest of the site.
19. As a developer, I want Policy search to work when EmbeddingProvider is None, using keyword search, so that a fresh clone with Claude chat can still answer Policy questions.
20. As a developer, I want Policy search to use the same EmbeddingProvider setting as catalog search, so that there's one switch for embeddings and no new API key.
21. As a developer, I want the search response to say whether it ran in semantic or keyword mode, so that I can tell which path answered.
22. As a developer, I want Policies indexed automatically when policy-api starts, so that there's no separate step to remember.
23. As a developer, I want a Policy that I edit to be indexed again on the next start, and unchanged Policies left alone, so that restarts are fast and edits always show up.
24. As a developer, I want a Policy whose file I delete to disappear from search, so that withdrawn rules are never cited.
25. As a developer, I want all Policies indexed again when I switch embedding provider, so that stored vectors always match the model searching them.
26. As a developer, I want only one replica to index at a time, so that two instances starting together don't corrupt the index.
27. As a developer, I want the Policy files to ship inside the service, so that a deployed container finds them without access to the repo.
28. As a developer, I want a Policy file with no title in its front matter to fail startup with a message naming the file, so that a broken Policy is never cited as "untitled".
29. As a developer, I want policy-api to appear in the Aspire dashboard with its own database, health check and logs, so that I can watch indexing and searches.
30. As an eShop.Mcp developer, I want the policy, order and catalog tools in a library with no knowledge of WebApp, so that I can expose the same tools over MCP.
31. As an eShop.Mcp developer, I want the tool names, parameters, result shapes and error results fixed in this spec, so that I can build the MCP server in parallel.
32. As an eShop.Mcp developer, I want to supply my own way of attaching the user's token, so that order lookup works with a token from MCP sign-in.
33. As an eShop.Mcp developer, I want the search endpoint to be anonymous, so that the policy client needs no token and behaves the same in both hosts, whatever sign-in rules each host applies.
34. As a maintainer, I want order ownership checked only in Ordering.API's endpoints, so that no tool can be talked into revealing someone else's order.
35. As a maintainer, I want the Buyer's token to reach Ordering.API from inside the chat's interactive session, so that the order tools don't silently call Ordering with no token.
36. As a maintainer, I want Ordering's order list sorted newest-first and its order detail to include each item's product id, so that tools don't have to work around missing data.
37. As a maintainer, I want Policy splitting tested against the real Policy files, so that a rewritten Policy can't break Citations unnoticed.
38. As a maintainer, I want ingestion and both search modes tested against real Postgres, so that the SQL that does the work is the thing under test.
39. As a maintainer, I want the tools tested with only outbound HTTP faked, so that their results are checked without starting any services.
40. As a maintainer, I want CI to need no embedding model and no API key, so that tests stay free and deterministic.
41. As a maintainer, I want a way to check retrieval quality against a real embedding model on demand, so that I can tell whether semantic search actually finds the right Policy Section.

## Implementation Decisions

**policy-api (new service; see ADR 0004)**

- New project `Policy.API`, Aspire resource `policy-api`, with its own `policydb` database on the existing Postgres server. It is added to `eShop.Web.slnf` and gets the standard service defaults and health check.
- **Schema:**
  - A **Policy** table: slug (from the file name; primary key), title, category, the `last_updated` value from front matter (stored for display only), content hash (SHA-256 of the file bytes), the embedding model id the Policy was indexed with (a fixed marker when no provider is configured), and when it was indexed.
  - A **Policy Section** table: the owning Policy, order within the Policy, heading, anchor (a slug of the heading), body text, the text that was embedded, a nullable `vector(384)` embedding, and a full-text search column generated from the embedded text.
  - Managed through EF Core migrations, as Catalog.API does.
- **Splitting a Policy into Policy Sections:**
  - The YAML front matter supplies title, category and `last_updated`. A missing title fails startup, naming the file.
  - Each `##` heading starts a Policy Section. `###` headings and tables stay inside the section they appear in.
  - Text between the `#` title and the first `##` becomes an intro section whose heading is the Policy title. If that text is empty, there is no intro section.
  - A Policy with no `##` headings becomes a single section.
  - The embedded and searched text is `"{Policy title} › {heading}"` followed by the body, so that sections with the same heading in different Policies (e.g. "Helmets") are told apart.
- **Ingestion runs at policy-api startup,** like Catalog's seeding:
  - The Policy files are copied from the repo's `policies/` folder into policy-api's build output as content. The folder it reads is a setting, defaulting to that copy, so tests can point it at a temporary directory.
  - Inside one transaction holding a Postgres transaction-level advisory lock: for each file, if its content hash or the current embedding model id differs from what is stored (or the Policy is new), replace all its Policy Sections. Policies whose file is gone are deleted. Unchanged Policies are not touched and not embedded again.
  - The embedding model id comes from the embedding generator's metadata. Switching provider therefore changes it, and every Policy is indexed again.
  - Vectors are cut to 384 dimensions, as Catalog does, which suits both all-minilm and text-embedding-3-small. With roughly 100 Policy Sections, search scans every row exactly; there is no vector index.
- **Search modes:**
  - **Semantic** when an `IEmbeddingGenerator` is registered: embed the question, then order by cosine distance.
  - **Keyword** when none is: Postgres full-text search with an English configuration, ordered by rank.
  - The provider is detected the way Catalog.API does it (Ollama-enabled setting or a text-embedding connection), so the AppHost wiring is the same.
- **Search endpoint:** `GET /api/policies/search?q={question}&top={n}`, anonymous and read-only.
  - `q` is required and must not be empty. `top` defaults to 5; values below 1 are a 400; values above 10 are capped at 10.
  - Response shape:

    ```
    {
      mode: "semantic" | "keyword",
      results: [ { citation, policySlug, policyTitle, section, anchor, text, score } ]
    }
    ```

    `citation` is `"{Policy title} › {heading}"`, or just the Policy title for an intro section. An empty `results` list is a normal 200.

**AppHost**

- Add `policydb` and `policy-api` (reference `policydb`, wait for it). WebApp references policy-api.
- The embedding provider chosen by `EmbeddingProvider` is wired into policy-api as well as Catalog.API. The Ollama or Foundry resource is still created once, and both services share the embedding model.
- policy-api is not added to mobile-bff. eShop.Mcp's grilling decides how it reaches policy-api.

**Ordering.API (additive change)**

- The order detail response gains each item's `productId`, taken from the order item already stored in the domain.
- The Buyer's order list is returned newest-first by Order Date, ties broken by order number descending.
- Endpoints, their ownership checks and their 404-for-not-yours behaviour are unchanged. The tools add no ownership logic: ownership stays in the endpoints, where the current user comes from the request's token. A tool can be asked for any order number, including one a model invented or was manipulated into, and only the endpoint has a user id it can trust.

**Shared tools library (working name `eShop.Assistant.Tools`)**

- Holds `search_policies`, `list_my_orders`, `get_order` and `search_catalog` (moved out of the Shopping Assistant's conversation class). The user-info, add-to-basket and basket-contents tools stay in WebApp, because they depend on WebApp's session and basket state.
- Tools are exposed as `AIFunction`s with explicit snake_case names and descriptions, so WebApp passes them straight into its chat options and eShop.Mcp can wrap them as MCP tools.
- The library contains typed HTTP clients for policy-api, Ordering.API (v1) and Catalog.API (v2), addressed by Aspire service name. It knows nothing about `ClaimsPrincipal`, `HttpContext` or Blazor. The host registers the clients and attaches its own `DelegatingHandler` to the Ordering client. The policy and catalog clients need no token.
- **Tool contract.** This is the hand-off to the eShop.Mcp grilling: change it only on purpose, and update both specs when you do.
  - `search_policies(question: string)`
    - Calls the search endpoint with `top` 5 and returns its `mode` and `results` as they come.
    - Description: AdventureWorks Policies on returns, exchanges, shipping, warranty, sizing, payments and orders. State Policy rules only from these results; cite each rule with its `citation` value; if no result answers the question, say so.
  - `list_my_orders()`
    - Returns `{ today, orders: [ { orderNumber, orderDate, status, total } ] }`, newest-first, as Ordering returns them.
  - `get_order(orderNumber: int)`
    - Returns `{ today, orderNumber, orderDate, status, items: [ { productId, productName, units, unitPrice, type, brand } ] }`.
    - `type` and `brand` come from Catalog.API by `productId`. If an item can't be found in the catalog, they are `null`; the tool doesn't fail.
    - If Catalog.API itself fails, the whole call returns `unavailable` rather than items with `null` types, so the model never gives a Return Window without the item types it depends on.
  - `search_catalog(productDescription: string)`
    - Same behaviour and result as the existing catalog search tool, except `pictureUrl`: the host may supply a function that turns a product id into an image URL. WebApp supplies its current one; with none supplied, `pictureUrl` is left out. (Contract change agreed in the eShop.Mcp grilling, because a WebApp-relative path means nothing to an MCP client.)
  - `today` is the current UTC date, so the model doesn't have to guess it. The tools contain no Policy rules: no "60 days" anywhere in code. Return Windows come from cited Policy Sections.
  - **Error results** are returned as tool output (not thrown), shaped `{ error, message }`:
    - `not_found`: Ordering returned 404. It covers both unknown orders and orders that aren't yours.
    - `sign_in_required`: no token was available, or Ordering returned 401.
    - `unavailable`: any other failure, logged by the library.

**WebApp**

- **Getting the token into the chat.** The Shopping Assistant runs as an interactive Server component, where `HttpContext` isn't reliably available, so the existing token-forwarding handler can't be relied on there.
  - While the page is still rendered on the server, WebApp reads the Buyer's access token into a scoped token holder that the interactive session can use (the pattern Microsoft documents for interactive Server rendering).
  - A `DelegatingHandler` attaches it as a Bearer token. With no token in the holder, it sends no header.
  - The handler is attached to the tools library's Ordering client.
- The Shopping Assistant's tool list becomes the library's four tools plus its three WebApp-only tools.
- **System prompt:**
  - Mention orders and Policies in its scope.
  - Repeat the citation rule as a backstop.
  - Change the greeting from "AdventureWorks Concierge" to the Shopping Assistant.

**Domain language**

Policy, Policy Section, Citation, Return Window, Order Date and Final Sale are defined in `GLOSSARY.md`. Use them in code names, tool descriptions and docs. Final Sale is described in a Policy but not modelled on any product.

## Testing Decisions

- **What makes a good test here:** drive the highest seam that holds the behaviour, and assert on values from an independent source: the Policy file text, the order that was created, the JSON the endpoint returns. No interaction assertions on our own types (ADR 0002). No test passes only because something non-empty came back (ADR 0003).
- **Splitting Policies into Policy Sections: plain fast tests** in a new policy-api unit test project (MSTest.Sdk).
  - Inline markdown cases: front matter parsing, a missing title, an intro before the first `##`, an empty intro, `###` and tables kept in place, a file with no `##`, anchors.
  - One test reads the real `policies/` folder and asserts specific values. For example, Returns Exceptions has a section headed "GPS watches, navigators and bike computers" whose body contains "30-day", and its citation is "Returns Exceptions › GPS watches, navigators and bike computers".
- **Ingestion and search: functional tests over HTTP against real Postgres** in a new `Policy.FunctionalTests` (xUnit v3, Aspire `DistributedApplication` inside a `WebApplicationFactory` fixture). Prior art: `Catalog.FunctionalTests`, including `CatalogSeedTests`. The fixture points the Policies folder at a temporary directory and replaces `IEmbeddingGenerator`, a boundary we don't own, with a deterministic fake (e.g. hashed bag-of-words vectors), so ranking is predictable. The fake needs no contract test, because ADR 0002's contract rule is for fakes of our own ports.
  - Search finds the expected section first for a question that shares words with it, in semantic mode with the fake and in keyword mode with no generator, and `mode` reports which one ran.
  - Editing a file and restarting ingestion changes what search returns. Deleting a file removes its sections from results. An unchanged file isn't re-embedded: the fake counts calls, which is a boundary we don't own.
  - Changing the embedding model id indexes every Policy again.
  - Validation: an empty `q` and `top` 0 return 400; `top` 50 returns at most 10.
- **Ordering: functional tests over HTTP** in `Ordering.FunctionalTests`. Prior art: the existing ordering API tests with `AutoAuthorizeMiddleware`.
  - Create two orders for the auto-authorised user and assert the list comes back newest-first.
  - Create an order with known products and assert the detail response carries their `productId` values.
- **Tools library: fast tests at the `AIFunction` seam** in a new library unit test project.
  - Invoke each tool as a model would, through its `AIFunction`. Fake only the outbound `HttpMessageHandler` (prior art: `WebhookTests`), and assert on the returned JSON.
  - Cases: `get_order` adds `type` and `brand` from Catalog, and returns `null` for a product Catalog doesn't know; 404 → `not_found`; 401 → `sign_in_required`; a 500 → `unavailable`; `search_policies` passes `citation` and `mode` through; `today` is present.
  - The tools are also checked to have the contract's names and parameter names, since eShop.Mcp depends on them.
- **WebApp token handler: fast tests** in `WebApp.UnitTests`. Holder filled → the outbound request carries `Authorization: Bearer <token>`; holder empty → no header. Fake the inner handler only.
- **AppHost wiring: fast tests** in `eShop.AppHost.UnitTests`. Prior art: `AppHostConfigurationTests`. Check that policy-api exists with `policydb`, that WebApp references it, and that for each `EmbeddingProvider` value policy-api receives the same embedding wiring as Catalog.API, with a shared provider resource created once.
- **Retrieval quality: on demand only, not in CI.** A small golden set of questions and their expected Policy Section, e.g. "can I return my GPS watch?" → Returns Exceptions › GPS watches…, and "how much is shipping to Canada?" → Shipping Destinations › Canada. It runs against a real Ollama embedding model when an environment variable opts in, and reports which questions missed.
- **Acceptance check: manual, not in CI.** It is also the gate before eShop.Mcp's Claude Code demo, which asks the same question and expects the same answer.
  1. `aspire run` with `ChatProvider=Claude` and `EmbeddingProvider=Ollama`.
  2. Sign in as `alice` and place an order containing a Navigation item and a non-navigation item.
  3. Ask "what's the return window for my last order?" while the order is not yet shipped. Expected: it can't be returned until it ships, with a Citation from the Returns Policy.
  4. Ship the order through Ordering.API's ship endpoint and ask again. Expected: 30 days for the Navigation item and 60 days for the rest, with end dates counted from the Order Date, citing *Returns Exceptions › GPS watches, navigators and bike computers* and *Returns Policy › Return window*.
  5. Repeat with `EmbeddingProvider=None` (keyword mode) and expect the same Citations.
  6. Sign out and ask again. Expected: a prompt to sign in for orders, while a Policy question still works.
- **CI** runs with no Chat Provider and no embedding provider. No test calls an LLM or a real embedding model.

## Out of Scope

- **eShop.Mcp itself.** Its transport, MCP sign-in through Identity.API, client registration for Claude Code and VS Code (see ADR 0005), and whether it reaches policy-api directly or through mobile-bff all belong to the parallel eShop.Mcp grilling. This spec only fixes the tool contract it consumes.
- Voyage or any other new embedding provider.
- Whole-Policy-set-in-prompt as a mode.
- A WebApp page that shows Policies, and clickable Citations.
- A Final Sale flag on products, and any rule that depends on it.
- Computing Return Windows or warranty periods in code.
- Changing the user-info, add-to-basket or basket-contents tools, beyond the shared token-handler fix if it turns out they need it.
- Automated end-to-end tests that call a real LLM. This includes a Playwright journey through the chat.
- Streaming chat responses, and prompt caching.
- Updating Policy content.
- The MAUI ClientApp and HybridApp.

## Further Notes

- **Learning goal.** The reason for retrieval is learning RAG, and it is recorded in ADR 0004. At about 8k tokens, the whole Policy set would fit in a cached prompt. Reviewers raising that are right; it was a deliberate choice.
- **Unverified: whether tokens reach any backend from the chat today.** Nobody has checked whether the existing basket tools actually send a token from inside the interactive session; `AddToCart` already handles an Unauthenticated result. The token-holder work should first show the failure (or show there isn't one) with the Ordering client. If the basket client has the same problem, raise it as its own ticket rather than widening this one.
- **For the eShop.Mcp grilling, unverified:** that the MCP C# SDK can wrap an `AIFunction` as a server tool (`McpServerTool.Create(AIFunction)`). Check it against the SDK version that grilling chooses. If it can't, the library's tools still define the contract, and eShop.Mcp adapts them.
- **Order statuses** come back as Ordering's enum names (e.g. `AwaitingValidation`), while the Policies write "Awaiting validation". The model can map them. If acceptance shows it doesn't, map them in the order tools.
- `today` is a UTC date. A Buyer near midnight in a US time zone may see a Return Window end date that is off by one day. That's acceptable for now; record it if acceptance shows it.
- `TreatWarningsAsErrors` is on; the new projects follow it.
- Build order (for `/to-tickets`):
  1. The Ordering change and policy-api with its search endpoint, which are independent of each other.
  2. The shared tools library.
  3. WebApp's token holder and the tools wiring.
  4. eShop.Mcp, from its own spec, which can start once step 2 exists.
