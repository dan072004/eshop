# Spec: Tokens and estimated cost of the Shopping Assistant in the dashboard

Status: ready-for-agent

## Problem Statement

When I run the store and talk to the Shopping Assistant, I can't see what a Turn cost. The service defaults already collect the AI meter and trace source. The Ollama and Foundry integrations already wrap their clients in Microsoft.Extensions.AI's OpenTelemetry step, so those calls show up with tokens, but the Claude client emits nothing that is collected, so for Claude the Aspire dashboard shows no model calls and no token counts, and no Chat Provider shows a cost. When a reply is wrong, I can't see the system prompt, the tool calls or the tool results that led to it either. I only see the final reply in the chat panel. The prompt-caching feature being built in parallel will need the same view to show that its cache is hit.

## Solution

Every Chat Provider's calls show up in the dashboard as standard AI spans: one span per model call with its tokens, the tool executions under it, and one span for the whole Turn with its totals. For Claude models, each model call span and the Turn span also carry an estimated cost in US dollars. A counter metric adds up the estimated cost since WebApp started, so the Metrics tab shows what the session has cost so far. Cache-read and cache-write token counts appear on each model call span. When running in Development, the spans also include the messages, tool calls and tool results, for debugging prompts.

## User Stories

1. As a developer, I want each call to the Chat Provider to appear as a span in the dashboard, so that I can see how many model calls a Turn took.
2. As a developer, I want each model call span to show its input and output tokens, so that I can see which calls are expensive.
3. As a developer, I want one span per Turn that groups its model calls and tool executions, so that I can read a Turn as one trace.
4. As a developer, I want the Turn span to show the Turn's total tokens, so that I don't have to add up its model calls myself.
5. As a developer, I want each model call span for a Claude model to show its estimated cost in US dollars, so that I can see what a single call cost.
6. As a developer, I want the Turn span to show the Turn's total estimated cost, so that I can see what answering one Buyer message cost.
7. As a developer, I want a metric that adds up the estimated cost since WebApp started, so that I can see what a session has cost so far.
8. As a developer, I want the cost metric tagged by model, so that I can compare what Haiku and a larger model would cost.
9. As a developer, I want the cost labelled as an estimate, so that I don't mistake it for Anthropic's bill.
10. As a developer working on prompt caching, I want each model call span to show its cache-read and cache-write token counts, so that I can see whether the cache is hit.
11. As a developer working on prompt caching, I want cache reads and writes priced at their own rates, so that the estimate shows what caching saves.
12. As a developer, I want no cost shown for a model without a listed price (Ollama, Foundry, an unlisted Claude model), so that a missing price is never shown as $0.
13. As a developer running Ollama or Foundry, I want token spans and token metrics anyway, so that every Chat Provider looks the same in the dashboard apart from cost.
14. As a developer debugging a prompt locally, I want the spans to include the system prompt, the messages, the tool calls with their arguments and the tool results, so that I can see why the model answered as it did.
15. As a Buyer, I want my messages, orders and addresses never written into traces outside Development, so that my data doesn't end up in a deployed trace backend.
16. As a developer running the evals, I want the evals to keep estimating cost from the same price table as WebApp, so that the two estimates never disagree.
17. As a maintainer, I want one place that lists model prices, so that a price change is one edit.
18. As a maintainer, I want the price table to state when its prices were taken and what it assumes about cache lifetime, so that I know when it's stale.

## Implementation Decisions

- **Telemetry on every Chat Provider.** Ollama, Foundry and Claude each get the Microsoft.Extensions.AI OpenTelemetry step in their chat client pipeline. It goes inside function invocation, so each model call is its own span, and tool executions and model calls group under the Turn. The Ollama and Foundry Aspire integrations add that step themselves unless their `DisableTracing` setting is on (checked in ticket 02); WebApp turns it on, so each model call is exactly one span, from the shared step. The service defaults already collect the Microsoft.Extensions.AI source and meter, and that doesn't change.
- **The Turn span.** Microsoft.Extensions.AI's function invocation already emits `orchestrate_tools`, a span covering the whole Turn with its total input and output tokens, when the OpenTelemetry step is inside it (checked in ticket 02). That is the Turn span for every Chat Provider; the Claude chat pipeline starts none of its own.
- **Message content only in Development.** The OpenTelemetry step captures message content (`EnableSensitiveData`) when, and only when, the host environment is Development. In every other environment, spans carry tokens and metadata only. Microsoft.Extensions.AI also reads the standard `OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT` variable, but the Development check is the chosen switch.
- **One price table, moved into WebApp.** The evals' table of Anthropic list prices moves into WebApp, next to the Claude chat pipeline, and the evals' run summary uses it from there. It's keyed by model id prefix, with prices per million tokens for input, output, cache reads and cache writes. It currently lists `claude-haiku-4-5` (the Shopping Assistant's default) and the evals' judge model.
- **What the estimate counts.** The estimate is uncached input tokens × input price + output tokens × output price + cache-read tokens × 0.1 × input price + cache-write tokens × 1.25 × input price. The 1.25 is the price for the 5-minute cache lifetime, and the table's comment records that assumption and the date the prices were taken. Microsoft.Extensions.AI reports cache reads as the cached-input count, and the Anthropic adapter reports cache writes in its additional counts. The adapter's input count already includes cache reads and cache writes (checked in ticket 02), so the uncached input tokens are the input count minus both, and they're never counted twice.
- **A cost only when the price is known.** A model with no listed price gets no cost: no cost attribute on its spans and no increase of the counter. That covers Ollama, Foundry and any Claude model missing from the table.
- **Where the cost appears.** A custom attribute, `eshop.assistant.estimated_cost_usd`, goes on each model call span and on the Turn span, which gets the Turn's total. The OpenTelemetry AI conventions have no cost attribute, so the name uses the project's own prefix.
- **The cost metric.** A counter `eshop.assistant.estimated_cost`, unit `USD`, tagged with the model id, goes up by each model call's estimated cost. It's per model call, not per Turn, so nothing is counted twice. It has its own meter, which WebApp adds to the collected meters.
- **Cache token attributes.** Each model call span carries `gen_ai.usage.cache_read.input_tokens`, which Microsoft.Extensions.AI writes, and `gen_ai.usage.cache_creation.input_tokens`, which this feature adds because Microsoft.Extensions.AI doesn't. The Anthropic SDK 13.3.0 emits its own spans by default (source `com.anthropic.sdk.csharp`, `anthropic.messages.create`, with `gen_ai.usage.cache_write.input_tokens`); WebApp switches them off, so the Microsoft.Extensions.AI `chat` spans are the one source and no Claude call shows up twice (ticket 02's Comments say why). The prompt-caching feature uses these attributes to check its cache hits. Whichever of the two ships second needs a look at the other's pipeline change.
- **The evals keep working as they do.** The eval builds the same Claude chat pipeline, so its calls also produce spans and counter increases. Nothing listens in the test process, so this has no effect. Its run summary keeps estimating cost itself from the shared table.
- **New domain term.** **Turn** is in the glossary: one Buyer message and the Shopping Assistant's reply, however many model calls and tool uses that takes. No ADR, because every decision here is easy to reverse.

## Testing Decisions

- A good test drives the Claude chat pipeline through its public entry point (one request, which is one Turn) and asserts on what comes out of it: the spans and the metric, read with an activity listener and a meter listener. It asserts nothing about how the pipeline's steps are arranged.
- **One seam.** The Claude chat pipeline's build method over the real Anthropic adapter, with a fake HTTP message handler under it, the boundary we don't own. The fake returns Messages API JSON with Anthropic's documented usage fields, so the test also pins how the adapter maps cached tokens. A fake chat client in place of the adapter would have to guess that mapping.
- **Tests, in the existing Claude chat pipeline tests:**
  1. A Turn with two model calls (the first asks for a tool, the second answers), each with known input, output, cache-read and cache-write tokens on a priced model. Each model call span carries its tokens, cache tokens and estimated cost. The Turn span carries the totals. The counter went up twice, tagged with the model, and the increases add up to the Turn's cost. Expected values are written as literals.
  2. The same Turn on a model with no listed price. The spans carry tokens but no cost attribute, and the counter never goes up.
- **The price table.** It has no tests of its own today; the eval run summary test covers it and stays. Add a plain fast test for input, output, cache-read and cache-write pricing and for an unlisted model.
- **Not tested.** The OpenTelemetry step on Ollama and Foundry is one line of composition. A test asserting it would be shape-only, which ADR 0003 rules out, so it's checked in review. The Development-only content capture is a single condition on the host environment, also checked in review.
- **Manual check.** One run with `ChatProvider=Claude`, checking that the Turn, its model calls, tokens and cost appear in the dashboard's AI view and that the cost counter appears under Metrics.
- **Prior art.** `WebhookTests` (fakes only the outbound `HttpMessageHandler`), the existing Claude chat pipeline tests, and the eval run summary tests. The prompt-caching spec uses the same seam, so the two features share the fake handler.

## Out of Scope

- Cost for Ollama or Foundry, and showing $0 for local models.
- Anthropic's billed cost, which the API doesn't return per request.
- Storing spend beyond the dashboard's in-memory session, or any metrics backend.
- Cost per Buyer or per conversation.
- Pricing thinking tokens, because thinking is off.
- Pricing the 1-hour cache lifetime, unless the prompt-caching feature chooses it.
- Turning on prompt caching. That's the parallel feature.
- Making the price table configurable.

## Further Notes

- The counter is a running total since WebApp started, and the Aspire dashboard keeps telemetry only in memory, so it reads as "what this session cost".
- List prices change. The table's date comment is the reminder to check them.
