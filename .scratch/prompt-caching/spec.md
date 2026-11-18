# Spec: Prompt caching for the Shopping Assistant on Claude, and what it saves

Status: ready-for-agent

Blocked by: `.scratch/assistant-telemetry/spec.md` (ships first; this feature reads its spans, cache token attributes and price table)

## Problem Statement

Every model call of a Turn sends the whole request again: the tool definitions, the system prompt and the conversation so far, including any Policy search results. A Turn that searches the Policies makes at least two model calls, and each one pays full input price for the same prefix. The next Turn pays for it again. I'd like to learn how prompt caching works on this app and know, in dollars, whether it saves anything on Claude Haiku 4.5, the Shopping Assistant's default model.

## Solution

When the Chat Provider is Claude, each request asks Anthropic to cache its prefix. Later model calls in the same Turn, and the Buyer's next Turn, read the shared prefix from the Prompt Cache at a tenth of the input price. The saving shows in two places: the dashboard's model call spans (cache-read and cache-write tokens, and estimated cost, from the assistant-telemetry feature), and the eval run summary, which gains cache-read and cache-write columns and prices them at their own rates.

Haiku 4.5 caches only prefixes of 4,096 tokens or more, so the cache is expected to start working once Policy search results are in the conversation. If the measurement shows no saving on Haiku, the marker is removed and the finding is recorded instead.

## User Stories

1. As a developer, I want the Shopping Assistant's Claude requests to ask for prompt caching, so that repeated prefixes are billed at the cache-read price.
2. As a developer, I want the cache to cover the conversation as well as the tools and system prompt, so that Policy search results are cached too.
3. As a developer, I want the second model call of a Turn that searched the Policies to read the first call's prefix from the cache, so that the tool loop doesn't pay twice for the same tokens.
4. As a developer, I want a Buyer's next Turn to read the previous Turn's prefix from the cache when it comes within five minutes, so that a conversation gets cheaper as it goes.
5. As a developer, I want caching to start working without code changes when the model changes to one with a lower minimum, so that a model upgrade gets the saving for free.
6. As a developer, I want the request sent to Anthropic checked in a test, so that I know the adapter actually sends the caching marker and doesn't drop it silently.
7. As a developer running the evals, I want the run summary to show cache-read and cache-write tokens per model, so that I can see whether the cache was hit.
8. As a developer running the evals, I want cache reads and writes priced at their own rates in the run summary, so that the estimated cost reflects what caching saves or costs.
9. As a developer, I want the dashboard to show cache-read tokens on the model calls after a Policy search, so that I can see the cache working in a real conversation.
10. As a maintainer, I want the PR to show the estimated chat cost before and after caching, so that the saving is a measured number and not a claim.
11. As a maintainer, I want the marker removed if Haiku shows no saving, so that we don't ship something that costs more.
12. As a maintainer, I want the measurement to use runs we pay for anyway, so that proving the saving costs no extra money.
13. As a developer running Ollama or Foundry, I want nothing to change, so that local and Azure models behave as before.
14. As a Buyer, I want the Shopping Assistant's answers to stay the same, so that caching only changes the bill and not what I'm told.
15. As a maintainer, I want the evals' Cached Answers and the Prompt Cache named differently in docs and code, so that nobody confuses replayed eval answers with Anthropic's cache.

## Implementation Decisions

- **Claude only.** The marker goes in the Claude chat pipeline's request defaults, next to the model id, output limit and thinking settings. Ollama and Foundry pipelines don't change.
- **Automatic caching, one marker.** Each request carries Anthropic's top-level automatic `cache_control` of type `ephemeral`, which caches up to the last cacheable block. The cached prefix therefore grows through the tool loop and across Turns: tools, then system prompt, then the conversation. No fixed breakpoint after the system prompt: on Haiku 4.5, the tools and system prompt alone (an estimated 2k tokens, confirm with token counting) are below the 4,096-token minimum and would never cache.
- **How the marker reaches the API.** Through the chat options' raw representation factory, which returns the SDK's message request with its top-level cache control set. A factory a caller already set takes precedence, as with the other defaults. If the Anthropic adapter turns out not to carry a top-level cache control from the raw representation into the request, put the marker on the last message's content instead; the pipeline test (below) decides which.
- **Five-minute lifetime.** The default `ephemeral` lifetime. Writes cost 1.25× the input price and reads 0.1×, which the assistant-telemetry price table already assumes. A Buyer's chat is short and bursty, so the 1-hour lifetime (2× writes) wouldn't pay off.
- **Policies stay out of the system prompt.** Policies keep coming from the Policy search tool (ADR 0004). Putting the whole Policy set in a cached system prompt was considered and rejected, because it reverses that ADR.
- **Builds on assistant-telemetry.** That feature adds the OpenTelemetry step, the cache-read and cache-write token attributes on each model call span, the shared price table with cache rates, and the check that the adapter's input count doesn't already include cached tokens. This feature rebases onto its Claude pipeline change and reuses all of it.
- **Eval usage gains cache tokens.** The evals' token usage record gains cache-read and cache-write token counts, filled from each chat call's usage the same way input and output are. The run summary's token and cost table gets two more columns, and its estimate uses the shared price table's cache rates. The judge's calls report zero cache tokens because the judge isn't cached.
- **Changing the pipeline resamples every eval case.** The marker changes every request, so the eval's Cached Answers no longer match and the PR's CI run samples all 40 cases fresh. That run costs about $1, which is spent regardless, and serves as the "after" measurement.
- **Measuring on Haiku, and the proof.**
  - First, a few scripted local conversations with `ChatProvider=Claude` (a Policy question, a follow-up, an order question), reading cache tokens and estimated cost on the model call spans in the dashboard.
  - Then the eval numbers for the PR: "before" from the last fresh eval run summary on main, "after" from the PR's run. The comparison only holds if the prompt and the cases didn't change between the two runs.
  - Caching is proven when cache reads are above zero on the model calls after a Policy search, **and** the "after" run's estimated chat cost is lower than "before".
  - If either part fails, the marker is removed, the finding goes in the PR, and only then is a Sonnet comparison run considered.
- **No new domain terms, no ADR.** The Prompt Cache is not a domain term, so it stays out of the glossary. Every decision here is easy to reverse.

## Testing Decisions

- A good test drives a public entry point and asserts on what comes out: the request that leaves for Anthropic, or the summary a maintainer reads. It asserts nothing about how the pipeline's steps are arranged.
- **Seam 1: the Claude chat pipeline over the real Anthropic adapter.** Build the pipeline around the real Anthropic client with a fake HTTP message handler under it, the boundary we don't own. Send one Turn and assert that the JSON body sent to Anthropic carries top-level `"cache_control": {"type": "ephemeral"}`, compared as a JSON literal. This also proves that the adapter honours the marker, which is the open risk. The fake answers with a fixed Messages API response.
- **Seam 2: the eval run summary.** A plain fast test, next to the existing gate tests. Given case results whose usage includes known cache-read and cache-write tokens on a priced model, the summary's token and cost table shows those counts and the cache-aware estimated cost, written as literals.
- **Not tested.** That Anthropic actually serves cache hits is the external service's behaviour, so the dashboard check and the eval run cover it. The five-minute lifetime is the default value of the marker, covered by the seam 1 literal.
- **Prior art.** `WebhookTests` (fakes only the outbound `HttpMessageHandler`), the existing Claude chat pipeline tests, and `RunGateTests`.

## Out of Scope

- Caching the eval judge's calls. Its rubric prefix repeats across cases and could be its own item.
- The 1-hour cache lifetime and keep-alive requests.
- A Sonnet comparison run, unless Haiku fails the proof.
- Migrating to Claude Sonnet 5.5, which rejects disabled thinking and needs `between_tools`.
- Putting the Policy set in the system prompt.
- Cache diagnostics (the beta that explains cache misses).
- Caching for Ollama or Foundry.

## Further Notes

- Caching isn't free on Haiku. Once a prefix passes 4,096 tokens, every call pays the 1.25× write. It pays back only when a later call within five minutes reads that prefix. A one-call Turn that ends a conversation costs more with caching than without, which is why the proof compares the whole eval run's cost and not just whether the cache is hit.
- Toggling thinking or changing effort between requests invalidates the cached messages. The pipeline fixes both, so this doesn't happen today. Keep it in mind if either becomes configurable per Turn.
