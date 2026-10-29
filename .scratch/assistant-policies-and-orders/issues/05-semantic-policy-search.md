# 05: Semantic Policy search through EmbeddingProvider

**What to build:** When an embedding provider is configured, policy-api searches Policy Sections by meaning, and says so in `mode`.

- **AppHost:** it wires the provider chosen by `EmbeddingProvider` (Ollama or Foundry) into policy-api the same way as Catalog.API, still creating the Ollama or Foundry resource once. policy-api detects its embedding source the way Catalog.API does.
- **Ingestion:** it embeds each Policy Section's indexed text, cut to 384 dimensions, and stores the generator's model id. When the stored model id differs from the current one (including a switch from no provider), every Policy is indexed again.
- **Search:** with a generator, it embeds the question and orders by cosine distance, with `mode` = `semantic`. Without a generator, it stays in keyword mode, as in ticket 03.
- **On-demand golden set:** a small list of questions with their expected Policy Section (e.g. "can I return my GPS watch?" → Returns Exceptions › GPS watches…, and "how much is shipping to Canada?" → Shipping Destinations › Canada). It runs against a real Ollama embedding model only when an environment variable opts in, and reports which questions missed.

**Blocked by:** 03 (policy-api with keyword Policy search)

**Status:** ready-for-agent

- [ ] `Policy.FunctionalTests` replaces `IEmbeddingGenerator` (a boundary we don't own) with a deterministic fake, such as hashed bag-of-words vectors:
  - [ ] Search returns the expected section first, with `mode` = `semantic`
  - [ ] An unchanged Policy isn't embedded again on a second ingestion. The fake counts calls
  - [ ] Changing the fake's model id indexes every Policy again
  - [ ] The keyword-mode tests from ticket 03 still pass with no generator
- [ ] AppHost unit tests: for each `EmbeddingProvider` value, policy-api receives the same embedding wiring as Catalog.API, and a shared provider resource is created once
- [ ] The golden set exists, is skipped unless the environment variable is set, and has been run once against Ollama with its results noted in the ticket's comments
- [ ] CI needs no embedding model
