# 01: WebApp's Claude chat pipeline becomes one builder

**What to build:** A prefactor with no behaviour change, so the eval can score exactly WebApp's chat setup.

- **Today:** WebApp builds the Claude pipeline inline when it registers its Chat Provider:
  - the model id default (`claude-haiku-4-5`) and the output limit default (1024), overridable under `Claude:ModelId` and `Claude:MaxOutputTokens`;
  - a `ConfigureOptions` that sets `ReasoningEffort.None` (sent as thinking disabled);
  - function invocation.
- **The change:** move that into one public builder that takes an inner `IChatClient` plus the configuration, and applies the same defaults, options and function invocation around it.
- **WebApp** passes the Anthropic client, so it behaves exactly as before. The missing-key startup failure stays where it is.
- **Why:** the eval (ticket 03) passes a caching client wrapped around the Anthropic client. That puts the cache below function invocation, so each model turn is cached, without copying WebApp's setup. The eval then scores what ships.

See the evals spec's "WebApp: the chat pipeline as one builder".

**Blocked by:** None (can start immediately)

**Status:** done

- [x] The existing chat-provider registration tests pass unchanged
- [x] The existing Shopping Assistant tests pass unchanged
- [x] The builder can be called from another assembly with any inner `IChatClient`, and returns a client with function invocation and the thinking-off option applied
- [ ] With `ChatProvider=Claude`, `aspire run` still answers a chat message as before (manual check)

## Comments

- 2026-11-09: Merged into `evals` (commit a1686b6). `dotnet build eShop.Web.slnf` passed and `tests/WebApp.UnitTests` passed (29/29), which covers the three automated checks. The manual check (`aspire run` with `ChatProvider=Claude` answering a chat message) is still open.
