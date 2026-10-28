# 02: Claude as a Chat Provider

**What to build:** A developer sets `ChatProvider=Claude`, supplies an Anthropic API key when the AppHost asks for it, and the Shopping Assistant runs on Claude. It keeps the same system prompt, the same four tools (user info, catalog search, add to basket, basket contents) and the same UI. A missing key fails at startup with a clear message. See `.scratch/claude-chat-provider/spec.md`.

**Blocked by:** 01 (Explicit Chat Provider and embedding provider selectors)

**Status:** done

- [x] `Claude` is a valid `ChatProvider` value. It is not a valid `EmbeddingProvider` value.
- [x] When `ChatProvider=Claude`, the AppHost declares an `anthropic-api-key` secret parameter and passes it to WebApp as `ANTHROPIC_API_KEY`. For every other value, no such parameter exists.
- [x] WebApp's Chat Provider registration handles `Claude`:
  - it builds a client from the official `Anthropic` SDK, adapts it with `AsIChatClient` and registers it with function invocation;
  - the defaults are model `claude-haiku-4-5`, maximum output tokens 1024 and thinking explicitly disabled;
  - the model id and maximum output tokens can be overridden in one Claude config section.
- [x] `ChatProvider=Claude` with no `ANTHROPIC_API_KEY` throws at startup, naming that setting.
- [x] The `Anthropic` package version (13.3.0 or later) is in `Directory.Packages.props`, with a versionless reference in WebApp.
- [x] AppHost tests cover:
  - Claude chat on its own;
  - Claude chat combined with each embedding provider;
  - the key parameter existing only for Claude;
  - `Claude` rejected as an embedding provider.
- [x] WebApp unit tests check that, for `Claude`:
  - the resolved client's `ChatClientMetadata` reports the Anthropic provider and the default model id;
  - an overridden model id is honoured;
  - a missing key throws.

  No network call is made, and no mocks are used.
- [x] README and CLAUDE.md describe how to run with Claude, including how to set the key secret.
- [x] The scope note of the deferred chat-model security finding (`docs/deferred-issues/`) mentions Claude.
- [x] Manual smoke test, not in CI: with a real key, ask the Shopping Assistant to find a product and then add it to the basket, and confirm the basket updates. If tool calling doesn't work end to end, report it before working around it.
- [x] `dotnet build eShop.Web.slnf` and the server tests pass, with no new warnings.

## Comments

- Thinking is disabled through `ChatOptions.Reasoning`: the registration adds `ConfigureOptions(o => o.Reasoning ??= new ReasoningOptions { Effort = ReasoningEffort.None })`, which the adapter sends as `thinking.type=disabled` in either `AnthropicThinkingMode`. The adapter has no "off" mode of its own. This default can't be seen at the DI seam without a request, so no automated test covers it; the smoke test showed Haiku 4.5 accepts the request.
- Smoke test (2026-10-28, `ChatProvider=Claude`, real key, signed in as `alice`): asked to search for "AeroLite", then add the AeroLite Cycling Helmet, then list the basket. The assistant found item 95, added it, and reported it; `/cart` showed the helmet. Tool calling works end to end with `UseFunctionInvocation()`.
- Side observation, not caused by this change: with `EmbeddingProvider=None`, catalog search is name-prefix only, so "cycling helmet" or "backpack for hiking" find nothing and the assistant says the store doesn't stock them.
