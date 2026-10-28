# 01: Explicit Chat Provider and embedding provider selectors

**What to build:** A developer chooses the Chat Provider and the embedding provider with two AppHost settings, instead of a flag for Foundry and a code edit for Ollama:

- `ChatProvider`: `None` (default) | `Ollama` | `Foundry`.
- `EmbeddingProvider`: `None` (default) | `Ollama` | `Foundry`.

WebApp is told the chosen Chat Provider explicitly and stops guessing it from whichever connection strings happen to exist. The Shopping Assistant behaves exactly as it does today on Ollama and Foundry. With no Chat Provider, it shows its existing "isn't switched on" notice. This is the prefactor that makes adding Claude a single new case. See `.scratch/claude-chat-provider/spec.md`.

**Blocked by:** None (can start immediately)

**Status:** done

- [x] `UseFoundry` and the hardcoded Ollama switch are removed (no alias). The two selectors replace them, are parsed case-insensitively, and default to `None`.
- [x] An unknown selector value fails AppHost startup with a message naming the setting and the allowed values.
- [x] The Foundry resource and the Ollama resource are each created at most once, with only the chat and/or embedding deployments or models the selectors ask for (e.g. Foundry chat with no embeddings; Ollama embeddings with Foundry chat).
- [x] The AppHost passes the chosen `ChatProvider` value to WebApp, along with the reference that provider already needs.
- [x] Catalog.API's code is unchanged. It receives its embedding source from the AppHost according to `EmbeddingProvider`.
- [x] WebApp's Chat Provider registration is a public extension on `IHostApplicationBuilder`. It replaces the private AI registration and is called from the application-services setup. It switches on `ChatProvider`:
  - missing or `None` gives no `IChatClient`;
  - `Ollama` and `Foundry` register as today, with function invocation;
  - an unknown value throws at startup, naming the setting.
- [x] The Shopping Assistant's conversation state, system prompt, four tools and chatbot component are unchanged.
- [x] AppHost tests at the application-model seam cover:
  - each value of each selector;
  - mixed combinations;
  - a shared resource being created once;
  - unknown values.

  The existing `UseFoundry` tests are rewritten for the selectors. Prior art: the existing Foundry and Ollama extension tests.
- [x] A new `WebApp.UnitTests` project (MSTest.Sdk) tests the registration through DI with in-memory config:
  - no client for missing or `None`;
  - a resolved client for `Ollama` and for `Foundry` when given their connection config;
  - an exception naming the setting for an unknown value.

  No mocks.
- [x] README and CLAUDE.md describe `ChatProvider` and `EmbeddingProvider` in place of `UseFoundry`.
- [x] `dotnet build eShop.Web.slnf` and the server tests pass, with no new warnings.
