# 01: Claude requests ask Anthropic to cache their prefix

**What to build:** When the Chat Provider is Claude, every request the Shopping Assistant sends carries Anthropic's top-level automatic `cache_control` of type `ephemeral`. The cached prefix (tools, then system prompt, then the conversation) can then grow through the tool loop and across Turns, so the model call after a Policy search reads the earlier prefix from the Prompt Cache. Ollama and Foundry don't change, and the Buyer's answers stay the same. See `.scratch/prompt-caching/spec.md` (stories 1–6, 13, 14).

The marker goes into the Claude chat pipeline's request defaults, next to the model id, output limit and thinking settings, through the chat options' raw representation factory. A factory the caller already set takes precedence, as with the other defaults. If the Anthropic adapter doesn't carry a top-level cache control from the raw representation into the request, put the marker on the last message's content instead. The pipeline test decides which.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] A Claude chat pipeline test, using the real Anthropic adapter over a fake HTTP message handler, sends one Turn and asserts that the JSON body sent to Anthropic carries `"cache_control": {"type": "ephemeral"}`, compared as a JSON literal
- [ ] A caller-supplied raw representation factory still takes precedence over the default
- [ ] Ollama and Foundry pipelines are unchanged
- [ ] Locally with `ChatProvider=Claude`, after a Policy question, the dashboard's model call span after the Policy search shows cache-read tokens above zero (or, if it doesn't on Haiku 4.5, note what the span shows for ticket 03)
