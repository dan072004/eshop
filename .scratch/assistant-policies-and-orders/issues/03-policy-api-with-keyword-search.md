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

- [ ] `policy-api` and `policydb` appear in the Aspire dashboard, healthy, with ingestion logged at startup
- [ ] Plain fast tests (new policy-api unit test project, MSTest.Sdk) cover front matter, a missing title, an intro section, an empty intro, `###` and tables kept in place, a Policy with no `##`, and anchors
- [ ] A fast test over the real `policies/` folder asserts values. Example: Returns Exceptions has the section "GPS watches, navigators and bike computers", whose body contains "30-day" and whose citation is "Returns Exceptions › GPS watches, navigators and bike computers"
- [ ] New `Policy.FunctionalTests` (xUnit v3, Aspire + `WebApplicationFactory`; prior art: `Catalog.FunctionalTests`) against real Postgres, pointing the Policies folder at a temp directory:
  - [ ] Search returns the expected section first for a matching question, with `mode` = `keyword`
  - [ ] An edited file, after ingestion runs again, changes what search returns
  - [ ] A deleted file's sections disappear from results
  - [ ] An empty `q` and `top` 0 return 400; `top` 50 returns at most 10; no match returns 200 with empty `results`
- [ ] AppHost unit tests: `policy-api` exists with `policydb`
