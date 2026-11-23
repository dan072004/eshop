# 03: Estimated cost on spans and the cost counter

**What to build:** For Claude models with a listed price, the dashboard shows what each model call and each Turn cost, as an estimate in US dollars on their spans. The Metrics tab shows a running total of the estimated cost since WebApp started, by model. A model with no listed price (Ollama, Foundry, an unlisted Claude model) shows tokens but no cost anywhere.

**Blocked by:** 01 (One price table for WebApp and the evals, with cache rates), 02 (Every Chat Provider's Turns in the dashboard, with tokens)

**Status:** ready-for-human

- [x] Each Claude `chat` span carries `eshop.assistant.estimated_cost_usd`, priced from the shared table, including cache reads and writes, and never counting cached tokens twice (see ticket 02's finding)
- [x] The Turn span carries the Turn's total estimated cost
- [x] A counter `eshop.assistant.estimated_cost` (unit `USD`, tagged with the model id) goes up by each model call's estimated cost, once per model call. WebApp collects its meter
- [x] An unlisted model gets no cost attribute and no counter increase
- [x] Tests at ticket 02's seam (the real adapter over a fake HTTP handler): a two-call Turn on a priced model asserts each span's cost, the Turn's total, and two counter increases tagged with the model that add up to the Turn's cost; the same Turn on an unlisted model asserts no cost attribute and no increase. Expected values are literals
- [ ] Manual check: a Claude run shows cost on the spans and the counter in the Metrics tab

## Comments

Findings (2026-11-23):

- **One client prices and tags.** `CacheWriteTelemetryChatClient` became `ClaudeUsageTelemetryChatClient`, since the cost needs the same cache-write count it already read. Directly inside the OpenTelemetry step, it prices the call from `ClaudePrices` with uncached input = `InputTokenCount - CachedInputTokenCount - CacheCreationInputTokens`, tags the `chat` span, adds the cost to its parent `orchestrate_tools` span, and adds it to the counter. The Turn's total is summed in decimal, so it equals the sum of its calls exactly.
- **The counter goes up even with no tracing listener;** only the span tags need the spans. Its meter is `eShop.WebApp.ShoppingAssistant`, added in `AddClaudeChatClient` with `AddOpenTelemetry().WithMetrics(...)`, the way EventBusRabbitMQ adds its trace source. It's only collected under `ChatProvider=Claude`, the only provider that records it.
- **The cost is priced and tagged by the request's model id** (`gen_ai.request.model`, the one in the span name), not the response's dated id. Both match the table by prefix.
- **Tests:** the existing token test in `ClaudeChatPipelineTests` now also asserts each span's cost, the Turn's total and the two counter increases (a stronger test replacing the weaker one), and a new test runs the same Turn on `claude-unlisted-1`. The test's meter listener keeps only increases recorded while its own trace is current, because tests run in parallel.
- Manual dashboard check not done (needs a run with `ChatProvider=Claude` and a key).
