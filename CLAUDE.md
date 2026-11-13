# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

eShop ("AdventureWorks") is a .NET 10 reference e-commerce app built as microservices and orchestrated with Aspire 13.6. `src/eShop.AppHost/AppHost.cs` is the single source of truth for which services exist, what infrastructure they use (Postgres with pgvector, Redis, RabbitMQ) and how they reference each other.

## Commands

Prerequisites: the .NET SDK pinned in `global.json`, the Aspire CLI, and a running container runtime (Docker or Podman). Both the app and the functional tests start containers.

```bash
aspire run                                   # run everything; prints the dashboard URL (root aspire.config.json picks the AppHost)
aspire start / aspire ps / aspire stop       # same, but in the background

dotnet build eShop.Web.slnf                  # what CI builds (excludes the MAUI ClientApp/HybridApp)
dotnet test --solution eShop.Web.slnf        # all server tests
dotnet test --project tests/Ordering.UnitTests                                     # one project
dotnet test --project tests/Ordering.UnitTests --filter "FullyQualifiedName~Cancel"  # a subset

npm ci && npx playwright install chromium
npm run test:e2e                             # Playwright journeys in e2e/; starts the AppHost itself

# Shopping Assistant evals: opt-in, call a real Claude model, about $1 per fresh run (ADR 0006)
dotnet user-secrets set ANTHROPIC_API_KEY "<key>" --project tests/ShoppingAssistant.Evals   # or the ANTHROPIC_API_KEY env var
ESHOP_EVALS=1 dotnet test --project tests/ShoppingAssistant.Evals                          # all 40 cases and the gate
ESHOP_EVALS=1 ESHOP_EVALS_CASE=returns-gps-watch-last-order dotnet test --project tests/ShoppingAssistant.Evals  # some cases, no gate
dotnet aieval report -p artifacts/evals-store --open                                       # the HTML report
```

- Tests run on **Microsoft.Testing.Platform** (`global.json` `"test.runner"`), so use `--project` / `--solution`. VSTest-only flags such as `-nologo` fail.
- Unit tests (`*.UnitTests`) use MSTest.Sdk. Functional tests (`*.FunctionalTests`) use xUnit v3 and start real Postgres/Redis/Identity containers through an Aspire `DistributedApplication` inside a `WebApplicationFactory` fixture. Ordering functional tests skip auth with `AutoAuthorizeMiddleware`. Which kind to write: see **Testing** below.
- `TreatWarningsAsErrors` is on for the whole repo, so warnings break the build.
- Build output goes to `artifacts/` (`UseArtifactsOutput`). While the app runs, its DLLs are locked; to build or test without stopping it, add `--artifacts-path <some other dir>`.
- Playwright CI sets `ESHOP_USE_HTTP_ENDPOINTS=1`, which makes the AppHost use plain-HTTP launch profiles, and logs in with `USERNAME1`/`PASSWORD` (seeded users such as `alice` / `bob`, password `Pass123$`).
- Package versions are centralised in `Directory.Packages.props`; don't put versions in individual csproj files.
- Without `ESHOP_EVALS=1`, the evals' model-calling tests are skipped and only the eval project's fast tests run, so a normal `dotnet test` needs no key. Cached answers are replayed for 14 days; `ESHOP_EVALS_FRESH=1` samples every case again. CI runs the evals in `.github/workflows/evals.yml` on PRs that touch the assistant, with the key from the `evals` environment.

## Architecture

**Service communication**
- Services find each other by Aspire resource name, e.g. `https+http://ordering-api` (see `src/WebApp/Extensions/Extensions.cs`). WebApp calls Catalog and Ordering over HTTP and Basket over gRPC (`src/Basket.API/Proto/basket.proto`). `.AddAuthToken()` forwards the user's token on those calls.
- Identity.API (Duende IdentityServer) handles login for WebApp and the APIs. The AppHost injects every client's callback URL into Identity, which creates a deliberate reference cycle.
- `mobile-bff` is a YARP reverse proxy for the MAUI `ClientApp`.
- `eShop.ServiceDefaults` is shared by every service (`AddServiceDefaults`): OpenTelemetry, health checks, resilience, JWT auth, and `ClaimsPrincipalExtensions` (`GetUserId`).

**Async workflow (order lifecycle)**
Services exchange integration events over RabbitMQ using `src/EventBus` (abstractions) and `src/EventBusRabbitMQ`. Each service registers its handlers in its `Extensions.cs` with `AddRabbitMqEventBus("eventbus").AddSubscription<TEvent, THandler>()`. An order moves through these steps:
1. Checkout publishes `OrderStarted`, and Basket clears the basket.
2. OrderProcessor confirms the grace period.
3. The order becomes AwaitingValidation, and Catalog checks stock.
4. Once stock is confirmed, PaymentProcessor handles payment.
5. The order becomes Paid or Cancelled.

WebApp also subscribes to the status-change events so it can push live updates (`OrdersRefreshOnStatusChange`). Catalog and Ordering publish through `IntegrationEventLogEF` (transactional outbox): events are saved in the same database transaction as the business change, then published.

**Ordering (DDD/CQRS, the most layered service)**
- `Ordering.Domain`: aggregates (`Order`, `Buyer`) that enforce the business rules (e.g. `SetCancelledStatus` refuses Paid or Shipped orders) and raise domain events.
- `Ordering.Infrastructure`: EF Core `OrderingContext`, repositories, and dispatching of domain events on save. Ordering.API runs the migrations, which is why OrderProcessor `WaitFor`s it.
- `Ordering.API`: minimal API endpoints in `Apis/OrdersApi.cs`, which send MediatR commands. Writes are wrapped in `IdentifiedCommand<T>` and de-duplicated by the `x-requestid` header. Reads go through `Application/Queries` (`IOrderQueries`) instead of the domain model. Pipeline behaviours handle logging, validation (FluentValidation) and transactions.
- Commands are also sent from integration event handlers, which have no current user. Per-user checks (e.g. order ownership) therefore belong in the endpoint, not in the command handler.

**Front ends**
- `WebApp`: Blazor Web App using static SSR with enhanced navigation and forms (`@formname`, `[SupplyParameterFromForm]`, `data-enhance`). Shared UI lives in `WebAppComponents`. Styling uses design tokens (CSS custom properties such as `--color-danger`, `--text-sm`, `--radius-md`) and per-component `.razor.css` files.
- AI is opt-in through two AppHost settings, each `None` (default), `Ollama` or `Foundry`, and for chat also `Claude`: `ChatProvider` picks the Chat Provider behind WebApp's Shopping Assistant (the AppHost passes the value to WebApp, whose `AddChatProvider` switches on it), and `EmbeddingProvider` picks the embeddings for Catalog.API's semantic search and policy-api's Policy search (which falls back to keyword search under `None`). A provider chosen for both is provisioned once (`AddChatAndEmbeddingProviders` in `src/eShop.AppHost/Extensions.cs`).
- `ChatProvider=Claude` uses the official `Anthropic` SDK. The AppHost declares the secret parameter `anthropic-api-key` (set it with `aspire secret set "Parameters:anthropic-api-key" "<key>"`, or enter it when the dashboard prompts) and passes it to WebApp as `ANTHROPIC_API_KEY`; WebApp fails at startup without it. Defaults are `claude-haiku-4-5`, 1024 output tokens and thinking disabled; override the first two with `Claude:ModelId` and `Claude:MaxOutputTokens` in WebApp's configuration.
- Catalog.API supports API versioning (v1/v2). The generated OpenAPI documents (`Catalog.API*.json`) are checked in.
- `ClientApp` (MAUI) and `HybridApp` are outside `eShop.Web.slnf` and have their own CI workflow.

## Testing

Test behaviour through a public **seam** (an HTTP endpoint, a repository interface, an aggregate's methods) and assert on what comes out: the response, the stored value read back, the published event. Pick the seam by where the behaviour lives:

| Where the behaviour lives | How to test it |
|---|---|
| Inside the infrastructure call (a SQL/EF query, a Redis script, serialisation) | Against the real thing in a functional test, at the HTTP or repository seam |
| Above infrastructure that only stores or transports data | Fast test with an in-memory **fake** at that seam |
| In a system we don't own (RabbitMQ `IEventBus`, outbound HTTP, clock, framework types) | Fake or NSubstitute at that boundary only |
| Nowhere near infrastructure (domain aggregates) | Plain fast test |
| In the model's reaction to our system prompt, tool descriptions and tool results (the Shopping Assistant's answers) | An eval case in `tests/ShoppingAssistant.Evals`, run with a real model (opt-in, ADR 0006). The eval's own checks and gate get plain fast tests |

- NSubstitute is for boundaries we don't own. Our own types (`IMediator`, handlers, query and repository interfaces) are used for real, or replaced by a fake. Assert on outcomes read back through the seam; `Received(...)` belongs only on a boundary such as `IEventBus`.
- A fake is a small working implementation (e.g. a `Dictionary`-backed repository) that is allowed only when the real adapter just persists. Every fake runs the same contract tests as its real adapter, so it can't drift. A fake may only give answers the real adapter can give. A canned-answer stub for a query port needs no contract test; a fake that implements logic does.
- Behaviour inside an adapter, like the ownership filter in Ordering's `OrderQueries` or the grace-period SQL in OrderProcessor, is tested against the real database.
- Endpoints are tested only through HTTP in the functional tests, where the current user comes from the test auth middleware. The current user (`IIdentityService`) is not a boundary, and endpoint methods such as `OrdersApi.CancelOrderAsync` are not called directly from unit tests.
- When a test replaces a mock-based one, delete the old test in the same change.

Reference examples: `WebhooksApiTests.SubscriptionLifecyclePersistsAndIsScopedToUser` (HTTP), `RedisBasketRepositoryTests` (real adapter), `OrderAggregateTest.when_add_two_times_on_the_same_item_then_the_total_of_order_should_be_the_sum_of_the_two_items` (domain), `WebhookTests` (fakes only the outbound `HttpMessageHandler`).

### Readable tests

A test reads like a **specification**: its name states the behaviour, and its body shows only the inputs and expected outputs that behaviour depends on.

- Name tests after the behaviour and its outcome (`CancellingAnUnknownOrderReturnsNotFound`).
- Assert on expected data written as literals:
  - The fields of one object that matter: `Assert.Equivalent(new { Status = "Submitted", Total = 10m }, order)`. It compares only the listed members.
  - A collection, exactly and in order: project to tuples and `Assert.Equal([(42, "Trail boots")], items.Select(i => (i.ProductId, i.ProductName)))`. For ordering, filter to the seeded items and compare the sequence.
  - A whole contract (every field, extras fail): `JsonElement.DeepEquals` against expected JSON written as a raw string literal, as in the assistant tool tests. A literal can't share the serializer it checks: the tools' output relies on per-property settings (e.g. `JsonIgnoreCondition.Never` keeps `"type": null`), and an expected value serialized from a C# object would drop or keep fields the same way the code under test does.
- Write a faked HTTP response body as a PascalCase anonymous type and send it with `FakeHandler.Json(status, object)`, which serializes with web defaults (camelCase) like the real API. Keep a body as a string only when the test needs it to be invalid JSON.
- Every assertion must be able to fail. `Assert.All` takes an `Action`, so a lambda that returns `bool` asserts nothing; assert inside it.
- A failure-path test sets up a valid request with exactly one thing wrong, so the status code has one cause.
- A test that only checks for 200 earns its place only if the endpoint can't return 200 on failure. Otherwise, read the result back.
- Each behaviour is tested once. When a stronger test covers a weaker one, delete the weaker one.

Reference: `OrderingApiTests`.

## Repo skills

`.github/skills/` contains agent skills for the Aspire CLI (`aspire`, `aspire-orchestration` for file locks and orphaned processes, `aspire-monitoring` for logs, traces and the dashboard, `aspire-deployment`), `dotnet-inspect` (querying NuGet and .NET APIs), and `playwright-cli`. Read the relevant `SKILL.md` before working in those areas.

Before reporting work as done, run the `verify` skill (`.claude/skills/verify`). It builds, runs the tests that match the diff, and reports PASS/FAIL/SKIPPED with evidence. A PreToolUse hook (`.claude/hooks/verify-gate.ps1`) denies `git commit` while uncommitted code changes haven't passed verification.

## Agent skills

### Issue tracker

Issues and specs are local Markdown files under `.scratch/<feature>/`. See `docs/agents/issue-tracker.md`.

### Triage labels

The five default roles (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`), recorded as a `Status:` line in each ticket file. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one `GLOSSARY.md` and `docs/adr/` at the repo root. See `docs/agents/domain.md`.
