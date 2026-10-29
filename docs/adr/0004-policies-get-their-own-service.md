# Policies get their own service and database

Policy search lives in a new `policy-api` with its own `policydb` on the shared Postgres server, not in Catalog.API, even though Catalog.API already has pgvector, an embedding generator and a seed-on-startup pattern. Policies are rules for Buyers, not catalog data, and the search has to be an HTTP endpoint that both the Shopping Assistant (WebApp) and eShop.Mcp can call, so it can't be a class inside WebApp either. The point of the work is also to learn retrieval: with 15 short Policies (about 8k tokens) the whole set would fit in a cached prompt, and we chose retrieval anyway.

## Considered Options

- **Put it in Catalog.API.** Rejected: Catalog would own returns and warranty rules, and its embedding code (`CatalogAI`) is built around `CatalogItem`.
- **Search inside WebApp.** Rejected: eShop.Mcp would have to call a front end as if it were a backend.
- **Whole Policy set in the prompt, no retrieval.** Rejected for now. It works at this size, but the goal is to learn retrieval and to give Claude Desktop a Policy Section it can cite.

## Consequences

- policy-api reuses the `EmbeddingProvider` setting (Ollama or Foundry), so the AppHost wires the embedding model into two resources. Under `None` it does not switch off, unlike the Shopping Assistant under `ChatProvider=None`: search falls back to Postgres full-text search over the same Policy Sections and reports `mode: "keyword"`, so Citations keep working.
- The search endpoint is anonymous and read-only, because Policies are public. The order tools keep using Ordering.API with the Buyer's token, and ownership is checked there.
- The Policy files ship as content in policy-api's output. Ingestion runs at startup and re-embeds a Policy when its file hash or the embedding model changes.
