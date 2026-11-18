# 02: Every Chat Provider's Turns in the dashboard, with tokens

**What to build:** When I talk to the Shopping Assistant, the Aspire dashboard shows each model call as a `chat` span with its input and output tokens, whichever Chat Provider is running. For Claude, one span covers the whole Turn with its totals, and each `chat` span also shows its cache-read and cache-write tokens. In Development, the spans also carry the system prompt, the messages, the tool calls and the tool results; in any other environment they carry no message content.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

**First, before any code:** the Anthropic SDK 13.3.0 has its own OpenTelemetry support. Find out whether it emits spans, under which source name, whether it's on by default, and which usage attributes it writes (cache reads as well as writes?). If it emits `chat` spans, keep one source of them so a Claude call never shows up twice, and take the cache token attributes from the SDK instead of adding them. Record the findings in this ticket's Comments, and update the spec if they change a decision. Also find out whether Microsoft.Extensions.AI 10.11.0 already emits a span covering the whole Turn.

- [ ] Ollama, Foundry and Claude model calls appear as `chat` spans with input and output tokens, with tool executions under the Turn
- [ ] A Claude Turn has one span covering all its model calls and tool executions, with the Turn's total tokens (an existing one, or one the Claude pipeline starts outermost)
- [ ] Each Claude `chat` span carries cache-read and cache-write token counts
- [ ] Message content is captured only when the host environment is Development
- [ ] It is settled whether the adapter's input token count includes cached tokens; the answer is in this ticket's Comments for ticket 03
- [ ] A test builds the Claude chat pipeline over the real Anthropic adapter with a fake HTTP message handler that returns two Messages API responses (a tool call, then the answer) with known usage, including cache tokens. It captures spans with an activity listener and asserts, as literals, each `chat` span's tokens and cache tokens and the Turn span's totals
- [ ] Manual check: a run with `ChatProvider=Claude` shows the Turn, its model calls and their tokens in the dashboard's AI view
