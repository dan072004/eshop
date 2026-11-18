# 02: Every Chat Provider's Turns in the dashboard, with tokens

**What to build:** When I talk to the Shopping Assistant, the Aspire dashboard shows each model call as a `chat` span with its input and output tokens, whichever Chat Provider is running. For Claude, one span covers the whole Turn with its totals, and each `chat` span also shows its cache-read and cache-write tokens. In Development, the spans also carry the system prompt, the messages, the tool calls and the tool results; in any other environment they carry no message content.

**Blocked by:** None (can start immediately)

**Status:** done

**First, before any code:** the Anthropic SDK 13.3.0 has its own OpenTelemetry support. Find out whether it emits spans, under which source name, whether it's on by default, and which usage attributes it writes (cache reads as well as writes?). If it emits `chat` spans, keep one source of them so a Claude call never shows up twice, and take the cache token attributes from the SDK instead of adding them. Record the findings in this ticket's Comments, and update the spec if they change a decision. Also find out whether Microsoft.Extensions.AI 10.11.0 already emits a span covering the whole Turn.

- [x] Ollama, Foundry and Claude model calls appear as `chat` spans with input and output tokens, with tool executions under the Turn
- [x] A Claude Turn has one span covering all its model calls and tool executions, with the Turn's total tokens (an existing one, or one the Claude pipeline starts outermost)
- [x] Each Claude `chat` span carries cache-read and cache-write token counts
- [x] Message content is captured only when the host environment is Development
- [x] It is settled whether the adapter's input token count includes cached tokens; the answer is in this ticket's Comments for ticket 03
- [x] A test builds the Claude chat pipeline over the real Anthropic adapter with a fake HTTP message handler that returns two Messages API responses (a tool call, then the answer) with known usage, including cache tokens. It captures spans with an activity listener and asserts, as literals, each `chat` span's tokens and cache tokens and the Turn span's totals
- [ ] Manual check: a run with `ChatProvider=Claude` shows the Turn, its model calls and their tokens in the dashboard's AI view

## Comments

Findings (2026-11-18), from a probe over the real adapter with a fake `HttpMessageHandler`; longer notes in the session scratchpad `notes/telemetry-findings.md`.

- **Anthropic SDK 13.3.0 traces are on by default.** `ActivitySource` `com.anthropic.sdk.csharp`, one span per HTTP call named `anthropic.messages.create` with `gen_ai.operation.name = chat`, metadata only by default (`ClientOptions.OpenTelemetry.Traces { Enabled, ContentMode, MaxContentBytes }`, or the `ANTHROPIC_OPEN_TELEMETRY*` variables). It writes `gen_ai.usage.input_tokens` (cache included), `output_tokens`, `cache_read.input_tokens` and `cache_write.input_tokens`. ServiceDefaults never collected that source.
- **Decision: the Microsoft.Extensions.AI spans are the one source; the SDK's are switched off** (`Traces.Enabled = false` in `AddClaudeChatClient`). Keeping the SDK's spans instead would lose the Turn and `execute_tool` spans (function invocation only finds the OpenTelemetry step's source), make Claude look different from Ollama and Foundry, and give ticket 03 no span it can tag inside the pipeline. So the cache-write count is added by `CacheWriteTelemetryChatClient` as `gen_ai.usage.cache_creation.input_tokens`; the cache-read count is already written by Microsoft.Extensions.AI as `gen_ai.usage.cache_read.input_tokens`.
- **Microsoft.Extensions.AI has a Turn span:** with the OpenTelemetry step inside function invocation, `FunctionInvokingChatClient` emits `orchestrate_tools`, the parent of every `chat` and `execute_tool` span, with the Turn's `gen_ai.usage.input_tokens` and `output_tokens` (no cache totals). No `invoke_agent` span; that belongs to the Agent Framework. So the Claude pipeline starts no span of its own. Same in 10.10.0, which is what WebApp actually resolves (transitively, through the Aspire and Ollama integrations); `Microsoft.Extensions.AI.Abstractions` is 10.11.0.
- **For ticket 03: the adapter's input token count includes cached tokens, reads and writes.** Messages API `input_tokens: 100, cache_read_input_tokens: 1000, cache_creation_input_tokens: 200` becomes `InputTokenCount = 1300`, `CachedInputTokenCount = 1000`, `AdditionalCounts["CacheCreationInputTokens"] = 200`. Uncached input is therefore `InputTokenCount - CachedInputTokenCount - CacheCreationInputTokens`; pricing `InputTokenCount` at the input rate would count the cache tokens twice.
- Manual dashboard check not done (needs a run with `ChatProvider=Claude` and a key).
