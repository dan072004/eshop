# 03: policy-api with keyword Policy search

**What to build:** A new service, `policy-api`, with its own `policydb` (ADR 0004), that turns the Policy files into searchable Policy Sections and answers `GET /api/policies/search` in keyword mode. This is the first full path from a Policy file to a cited search result.

- **Hosting:** the project is added to the AppHost and `eShop.Web.slnf`, with service defaults and a health check. The Policy files ship as content in its output, and the folder it reads from is a setting.
- **Splitting:** each Policy is split into Policy Sections as the spec describes. The YAML front matter supplies title, category and `last_updated`, and a missing title fails startup naming the file. Each `##` starts a Policy Section, there is an intro section when text comes before the first `##`, and `###` headings and tables stay inside their section. The text that is indexed is `Title › Heading` plus the body.
- **Ingestion at startup:**
  - It runs in one transaction under a Postgres advisory lock.
  - It replaces a Policy's sections when the file hash differs, or when the Policy is new.
  - Policies whose file is gone are deleted, and unchanged Policies are left alone.
  - The embedding model id is stored as the "no provider" marker. Ticket 05 adds real embeddings.
- **Search:** Postgres full-text search, English configuration, ordered by rank. It is anonymous. `q` is required; `top` defaults to 5, below 1 is a 400, above 10 is capped. The response is `{ mode: "keyword", results: [ { citation, policySlug, policyTitle, section, anchor, text, score } ] }`.

See the spec's "policy-api" section for the full schema and rules.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [x] `policy-api` and `policydb` appear in the Aspire dashboard, healthy, with ingestion logged at startup
- [x] Plain fast tests (new policy-api unit test project, MSTest.Sdk) cover front matter, a missing title, an intro section, an empty intro, `###` and tables kept in place, a Policy with no `##`, and anchors
- [x] A fast test over the real `policies/` folder asserts values. Example: Returns Exceptions has the section "GPS watches, navigators and bike computers", whose body contains "30-day" and whose citation is "Returns Exceptions › GPS watches, navigators and bike computers"
- [x] New `Policy.FunctionalTests` (xUnit v3, Aspire + `WebApplicationFactory`; prior art: `Catalog.FunctionalTests`) against real Postgres, pointing the Policies folder at a temp directory:
  - [x] Search returns the expected section first for a matching question, with `mode` = `keyword`
  - [x] An edited file, after ingestion runs again, changes what search returns
  - [x] A deleted file's sections disappear from results
  - [x] An empty `q` and `top` 0 return 400; `top` 50 returns at most 10; no match returns 200 with empty `results`
- [x] AppHost unit tests: `policy-api` exists with `policydb`

## Comments

- Implemented on `ticket/03-policy-api`. Verified in the real AppHost (`aspire start --isolated`): `policy-api` and `policydb` healthy; first start logged "Indexed Policies ... 15 new or changed, 0 removed, 0 unchanged", a restart logged "0 new or changed, 0 removed, 15 unchanged". "can I return my GPS watch?" returns Returns Exceptions › GPS watches, navigators and bike computers first; "how much is shipping to Canada" returns Shipping Destinations › Canada.
- For ticket 05 (semantic search):
  - Ingestion: `eShop.Policy.API.Infrastructure.PolicyIngestion.IngestAsync(CancellationToken)` (scoped; run at startup through `AddMigration<PolicyContext>` in `src/Policy.API/Extensions/Extensions.cs`). The embedding model id comes from the private `CurrentEmbeddingModelId` (now the constant `PolicyIngestion.NoEmbeddingModel = "none"`), and `ToIndexedPolicy` sets `Embedding = null`; those are the two slots for a generator. A model-id change already re-indexes every Policy.
  - Search: `eShop.Policy.API.Services.PolicySearch.SearchAsync(question, top, ct)` returns `PolicySearchResponse(Mode, Results)`; it always calls `KeywordSearchAsync` today. Add a semantic branch beside it when an `IEmbeddingGenerator` is registered.
  - Schema: `Policy` (Slug PK, Title, Category, LastUpdated, ContentHash, EmbeddingModelId, IndexedAt) and `PolicySection` (Id, PolicySlug FK cascade, Position, Heading, Anchor, Body, IndexedText, `Embedding vector(384)` nullable, `SearchVector` tsvector generated from IndexedText, GIN index). Entities: `IndexedPolicy`, `IndexedPolicySection`; context `PolicyContext`. Migration `Initial`.
  - Writing real vectors may need Catalog's `NpgsqlConnection.ReloadTypes()` workaround, because the `vector` extension is created by the migration on the same connection. Null vectors work without it.
- Policies folder setting: `Policies:Folder` (env `Policies__Folder`), bound to `PolicyOptions.Folder`, default `<AppContext.BaseDirectory>/policies` (the `policies/*.md` copied into the build output as content).
- Keyword search joins the question's words with OR (`plainto_tsquery` with `&` replaced by `|`), because AND would need every word of a natural question to appear in the section; `ts_rank` orders them.
- AppHost: `AddPolicyApi(postgres)` in `src/eShop.AppHost/Extensions.cs`; WebApp already references `policy-api`. Not added to `AddChatAndEmbeddingProviders` (ticket 05).
- The dotnet-ef global tool here is 8.x and can't read this repo's artifacts layout; the migration was made with `dotnet tool exec dotnet-ef@10.0.12 --yes -- migrations add <name> --context PolicyContext -o Infrastructure/Migrations --msbuildprojectextensionspath ../../artifacts/obj/Policy.API` from `src/Policy.API`.
