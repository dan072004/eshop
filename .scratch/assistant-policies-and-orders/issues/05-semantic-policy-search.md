# 05: Semantic Policy search through EmbeddingProvider

**What to build:** When an embedding provider is configured, policy-api searches Policy Sections by meaning, and says so in `mode`.

- **AppHost:** it wires the provider chosen by `EmbeddingProvider` (Ollama or Foundry) into policy-api the same way as Catalog.API, still creating the Ollama or Foundry resource once. policy-api detects its embedding source the way Catalog.API does.
- **Ingestion:** it embeds each Policy Section's indexed text, cut to 384 dimensions, and stores the generator's model id. When the stored model id differs from the current one (including a switch from no provider), every Policy is indexed again.
- **Search:** with a generator, it embeds the question and orders by cosine distance, with `mode` = `semantic`. Without a generator, it stays in keyword mode, as in ticket 03.
- **On-demand golden set:** a small list of questions with their expected Policy Section (e.g. "can I return my GPS watch?" → Returns Exceptions › GPS watches…, and "how much is shipping to Canada?" → Shipping Destinations › Canada). It runs against a real Ollama embedding model only when an environment variable opts in, and reports which questions missed.

**Blocked by:** 03 (policy-api with keyword Policy search)

**Status:** ready-for-agent

- [x] `Policy.FunctionalTests` replaces `IEmbeddingGenerator` (a boundary we don't own) with a deterministic fake, such as hashed bag-of-words vectors:
  - [x] Search returns the expected section first, with `mode` = `semantic`
  - [x] An unchanged Policy isn't embedded again on a second ingestion. The fake counts calls
  - [x] Changing the fake's model id indexes every Policy again
  - [x] The keyword-mode tests from ticket 03 still pass with no generator
- [x] AppHost unit tests: for each `EmbeddingProvider` value, policy-api receives the same embedding wiring as Catalog.API, and a shared provider resource is created once
- [x] The golden set exists, is skipped unless the environment variable is set, and has been run once against Ollama with its results noted in the ticket's comments
- [x] CI needs no embedding model

## Comments

- Implemented on `ticket/05-semantic`.
- **AppHost:** `AddChatAndEmbeddingProviders(catalogApi, webApp, policyApi)` now takes policy-api and gives it exactly what Catalog.API gets: Ollama → reference to `embedding` (all-minilm) + `OllamaEnabled=true` + `WaitFor`; Foundry → reference to `textEmbeddingModel` + `WaitFor`. The Ollama/Foundry resource and the embedding model are still created once and shared.
- **policy-api:** detects its generator like Catalog.API (`OllamaEnabled` + `embedding` connection, else a `textEmbeddingModel` connection), in `src/Policy.API/Extensions/Extensions.cs`. New `eShop.Policy.API.Services.PolicyEmbeddings` (scoped) wraps the optional `IEmbeddingGenerator<string, Embedding<float>>`: `IsEnabled`, `ModelId` (the generator metadata's `DefaultModelId`, e.g. `all-minilm`, or `PolicyEmbeddings.NoEmbeddingModel = "none"`; this replaces `PolicyIngestion.NoEmbeddingModel`), and `EmbedAsync`, which cuts vectors to 384 and sends batches of 32.
- **Ingestion** embeds only the sections of new or changed Policies, inside the advisory-locked transaction, and runs Catalog's `ReloadTypes` workaround first. **Search** returns `mode: "semantic"` when a generator exists and orders by `<=>` (cosine distance) with `score = 1 - distance`; otherwise it stays in keyword mode.
- **Tests:** `Policy.FunctionalTests` has `SemanticPolicySearchTests` with `SemanticPolicyApiFixture` (a `PolicyApiFixture` subclass; the base gained a `ConfigureTestServices` hook) and `HashedBagOfWordsEmbeddingGenerator` (returns 768-long vectors, so the cut to 384 is exercised; records embedded texts; settable `ModelId`). I checked by mutation that the "not embedded again" and "model change" tests fail when the logic breaks. Test classes in this assembly now run one at a time (`DisableTestParallelization`), because two fixtures starting Aspire's orchestrator together timed out.
- **Golden set:** `GoldenSetTests` + `GoldenSetFixture`, skipped unless `ESHOP_POLICY_GOLDEN_SET=1`. Run it with `$env:ESHOP_POLICY_GOLDEN_SET=1; dotnet test --project tests/Policy.FunctionalTests -- --filter-class "*GoldenSetTests"`. It starts its own Postgres and an Ollama container through Aspire (Ollama isn't installed locally; the container is what `EmbeddingProvider=Ollama` uses too), pulls all-minilm into a persistent data volume, indexes the real `policies/` folder, and fails listing every question whose expected section wasn't first.
- **Golden set result, 2026-10-29, Ollama 0.13.0 container, all-minilm: 14/18 first.** I wrote the 18 questions before the run and didn't tune them afterwards. Misses:
  - "how many days do I have to send something back?" → Returns Policy › Return window came 2nd (first: Order Status and Tracking › Late, lost or damaged parcels)
  - "is it too late to cancel my order?" → Order Cancellation and Changes › When you can cancel came 2nd (first: … › Orders we cancel)
  - "how long is the warranty for my brand?" → Warranty Overview › Warranty by brand came 3rd (first: … › After 60 days: claim from the brand)
  - "can I swap my boots for a bigger size?" → Exchanges › What you can exchange was not in the top 5 (first: Sizing Guide: Jackets and Footwear › Choosing the right footwear size)
  - 17/18 were in the top 5 that `search_policies` passes to the model. The one real gap is exchange wording ("swap"), which is worth trying again in acceptance (ticket 08).
- Security review (no Critical, High or Medium findings): the SQL is parameterised. **Low:** in semantic mode every anonymous search costs one embedding call, which is paid under Foundry, and `q` has no length cap or rate limit. policy-api has no external endpoint today. If eShop.Mcp or mobile-bff exposes it, add a `q` length cap (e.g. 500 characters) and a rate limiter, and update the spec's endpoint contract when you do.
- **Review fix, model id (2026-10-29):** `PolicyEmbeddings.ModelId` no longer falls back to the provider name or generator type name when the generator's metadata has no `DefaultModelId`; it throws `InvalidOperationException` with a message naming the connection-string setting, and since ingestion reads it at startup, policy-api fails to start. Why fail rather than derive: both wired generators already report the configured model (checked with a throwaway host on the pinned packages: CommunityToolkit.Aspire.OllamaSharp 13.1.1 → `provider=ollama model=all-minilm` from `Model=` in the `embedding` connection; Aspire.Azure.AI.OpenAI 13.6.0-preview → `provider=openai model=text-embedding-3-small` from `Deployment=`). So the fallback was never used by real wiring, and the only case it covered (a generator that doesn't name its model) is exactly the one where a provider-wide id would hide a model switch and leave stale vectors. Deriving the id from configuration would duplicate what the integrations already read. Tested in `Policy.UnitTests/PolicyEmbeddingsTests`.
