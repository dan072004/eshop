# Spec: Claude as a Chat Provider

Status: ready-for-agent

## Problem Statement

The Shopping Assistant can only be powered by Ollama or Microsoft Foundry, and neither is chosen in a straightforward way.

- **Foundry comes with embeddings.** It is switched on by a flag that also provisions embeddings for catalog search, so you can't get one without the other.
- **Ollama needs a code edit.** It can only be enabled by changing a hardcoded value in the AppHost.
- **WebApp guesses.** It works out which Chat Provider it was given by checking what configuration is present. If two providers were ever configured, the order of those checks would decide silently.
- **No Claude.** A developer who wants to run the Shopping Assistant on Claude has no way to do it.

## Solution

A developer chooses the Chat Provider with a single AppHost setting: `None`, `Ollama`, `Foundry` or `Claude`. Embeddings get a separate setting of their own.

When the developer picks Claude:

- The AppHost asks for an Anthropic API key, stored as a secret, and passes it to WebApp.
- WebApp registers a Claude-backed chat client behind the same `IChatClient` abstraction the Shopping Assistant already uses.
- The Shopping Assistant behaves as it does on any other Chat Provider: same system prompt, same four tools (user info, catalog search, add to basket, basket contents), same UI.

When nothing is chosen, the Shopping Assistant shows its existing "isn't switched on" notice. A misconfigured choice fails at startup with a message that names the setting.

## User Stories

1. As a developer, I want to set one AppHost setting to choose the Chat Provider, so that I know exactly which AI service the Shopping Assistant uses.
2. As a developer, I want `Claude` to be one of the Chat Provider choices, so that I can run the Shopping Assistant on Claude.
3. As a developer, I want `Ollama` to be selectable from config, so that I don't have to edit AppHost code to use a local model.
4. As a developer, I want `Foundry` to stay selectable for chat, so that the existing Azure path keeps working.
5. As a developer, I want the Chat Provider to default to `None`, so that a fresh clone runs without any AI credentials or downloads.
6. As a developer, I want to choose the embedding provider separately from the Chat Provider, so that I can combine Claude chat with Foundry embeddings, or use Claude chat with no semantic search.
7. As a developer, I want the embedding provider to default to `None`, so that catalog search falls back to its non-semantic behaviour unless I opt in.
8. As a developer, I want a provider resource to be provisioned once when both chat and embeddings point at it, so that I don't pay for or download duplicates.
9. As a developer, I want the AppHost to ask for an Anthropic API key only when Claude is the Chat Provider, so that people using other providers are never asked for it.
10. As a developer, I want the Anthropic API key stored as an Aspire secret parameter, so that it never appears in source control or plain config.
11. As a developer, I want the dashboard to prompt me for a missing Anthropic API key, so that I can supply it without restarting with different flags.
12. As a developer, I want WebApp to be told the chosen Chat Provider explicitly, so that it never has to infer it from which connection strings happen to exist.
13. As a developer, I want WebApp to fail at startup when the Chat Provider value is unknown, so that a typo doesn't quietly switch the Shopping Assistant off.
14. As a developer, I want WebApp to fail at startup when Claude is chosen but no API key is available, so that I find out immediately instead of at the first Buyer message.
15. As a developer, I want the startup error to name the setting that is wrong, so that I can fix it without reading code.
16. As a developer, I want the Claude model to default to Haiku 4.5, so that the Shopping Assistant is fast and cheap out of the box.
17. As a developer, I want to override the Claude model id in config, so that I can try a larger model without a code change.
18. As a developer, I want the maximum reply length for Claude to default to 1024 tokens and be overridable in config, so that replies stay short but can be tuned.
19. As a developer, I want Claude's extended or adaptive thinking switched off, so that replies are quick and the request is accepted by the default model.
20. As a Buyer, I want the Shopping Assistant to answer questions about the AdventureWorks catalog when it runs on Claude, so that I get help no matter which Chat Provider is configured.
21. As a Buyer, I want the Shopping Assistant on Claude to search the catalog for me, so that I can find products by describing them.
22. As a Buyer, I want the Shopping Assistant on Claude to add an item to my basket, so that I can shop from the conversation.
23. As a Buyer, I want the Shopping Assistant on Claude to tell me what's in my basket, so that I can check it without leaving the conversation.
24. As a Buyer, I want the Shopping Assistant on Claude to know who I am when I'm signed in, so that it can answer account questions.
25. As a Buyer, I want the Shopping Assistant on Claude to refuse off-topic questions, as it does today, so that it stays an AdventureWorks helper.
26. As a Buyer, I want a polite apology when the Chat Provider fails mid-conversation, as happens today, so that I'm not shown a raw error.
27. As a Buyer, I want to see the "Assistant isn't switched on" notice when no Chat Provider is configured, so that I understand why it can't answer.
28. As a maintainer, I want the conversation logic and its tools untouched by this change, so that adding a Chat Provider carries no risk of changing Shopping Assistant behaviour.
29. As a maintainer, I want the retired `UseFoundry` flag removed rather than aliased, so that there is exactly one way to choose providers.
30. As a maintainer, I want the README and CLAUDE.md to describe the new settings, so that newcomers don't follow instructions for a flag that no longer exists.
31. As a maintainer, I want the deferred security finding about chat models to mention Claude, so that its scope stays accurate.
32. As a maintainer, I want the Anthropic package version pinned centrally, so that it follows the repo's package-management rule.
33. As a maintainer, I want fast tests for every Chat Provider and embedding provider combination in the AppHost, so that wiring regressions are caught without starting containers.
34. As a maintainer, I want fast tests for WebApp's Chat Provider registration, including the fail-fast cases, so that config mistakes are caught in CI.
35. As a maintainer, I want CI to run with no Chat Provider, so that no test needs an API key or calls a paid service.

## Implementation Decisions

- **Two AppHost selectors replace implicit provider detection:**
  - `ChatProvider`: `None` (default) | `Ollama` | `Foundry` | `Claude`.
  - `EmbeddingProvider`: `None` (default) | `Ollama` | `Foundry`.

  Values are parsed case-insensitively. An unknown value fails AppHost startup.
- **The old flags go.** `UseFoundry` is removed, with no alias. The hardcoded Ollama switch is removed as well, and both are replaced by the selectors. The existing AppHost tests of `UseFoundry` are rewritten for the selectors.
- **Shared provider resources.** The Foundry resource and the Ollama resource are each created at most once. Each one adds only the deployments or models that the selectors ask for:
  - Foundry: the chat deployment and/or the text-embedding deployment.
  - Ollama: the chat model and/or the embedding model.
- **The AppHost tells WebApp which Chat Provider it chose.** It passes the selector value as the `ChatProvider` setting, alongside whatever reference that provider needs. Ollama keeps its existing chat reference, and Foundry its existing chat-model reference.
- **Catalog.API's code doesn't change.** It keeps detecting its embedding source as it does today. The AppHost now drives what it receives from `EmbeddingProvider`, i.e. the Ollama-enabled setting or the Foundry embedding reference.
- **The Anthropic API key is an AppHost secret parameter.** It is declared only when `ChatProvider=Claude`, and passed to WebApp as the `ANTHROPIC_API_KEY` environment variable. There is no Aspire integration for Anthropic, so this parameter-plus-environment pattern is the wiring.
- **WebApp gets a public Chat Provider registration entry point.** It is an extension on `IHostApplicationBuilder` that replaces today's private AI registration, and the application-services setup calls it. It switches on `ChatProvider`:
  - **Missing or `None`:** no `IChatClient` is registered. The Shopping Assistant shows its existing notice.
  - **`Ollama` or `Foundry`:** registered as today, through their Aspire client integrations.
  - **`Claude`:** registered by building an Anthropic client from the official `Anthropic` SDK, adapting it with its `AsIChatClient` extension, and adding it with `AddChatClient`.
  - **Unknown value:** throws at startup, naming the `ChatProvider` setting and the allowed values.
  - **`Claude` without a key:** throws at startup, naming the `ANTHROPIC_API_KEY` setting.
  - Every registered client gets function invocation, so the four tools work on every provider.
- **Claude request defaults:**
  - Model `claude-haiku-4-5`, which can be overridden in config.
  - Maximum output tokens 1024, which can be overridden in config.
  - Thinking explicitly disabled.

  The config keys sit together under one Claude section of WebApp configuration.
- **Package.** Use the official `Anthropic` NuGet package (13.3.0 or later), with its version in `Directory.Packages.props` and a versionless reference in WebApp. It requires a `Microsoft.Extensions.AI.Abstractions` version that the repo already satisfies.
- **What doesn't change:** the conversation state class, its system prompt, its four tools, and the chatbot component. The component already resolves an optional `IChatClient` and handles its absence.
- **Domain language.** The feature is the **Shopping Assistant**, and the AI service behind it is the **Chat Provider** (see `GLOSSARY.md`).

## Testing Decisions

- **What makes a good test here:** it drives a public seam with configuration and asserts on what comes out:
  - which resources exist and what each service receives, at the AppHost seam;
  - which `IChatClient` resolves, or which exception is thrown, at the WebApp seam.

  No test checks which methods were called, and no test calls Claude.
- **Seam 1, existing: the AppHost application model.** Build a distributed application builder with in-memory `ChatProvider` / `EmbeddingProvider` config and run the AppHost's provider configuration. Then assert on:
  - which resources are present (`foundry`, `ollama`, the chat and embedding deployments or models, `anthropic-api-key`);
  - the environment and references on WebApp and Catalog.API.

  Cases:
  - each `ChatProvider` value;
  - each `EmbeddingProvider` value;
  - mixed combinations, e.g. Claude chat with Foundry embeddings;
  - "a shared resource is created once";
  - "the key parameter exists only for Claude";
  - unknown values.

  Prior art: the existing tests that check which deployments the Foundry extension adds and which models the Ollama extension adds, and the test that `UseFoundry` defaults to opt-in.
- **Seam 2, new: WebApp's Chat Provider registration.** This needs a new fast unit test project for WebApp (MSTest.Sdk, like the other `*.UnitTests`). Each test builds a host application builder with in-memory config, calls the registration entry point, builds the service provider, and resolves `IChatClient`. Assert:
  - none is registered for missing or `None`;
  - for `Claude`, the resolved client's `ChatClientMetadata` reports the Anthropic provider and the configured model id, with Haiku 4.5 when none is configured;
  - unknown values and a missing key throw at startup with a message naming the setting;
  - Ollama and Foundry still resolve when given their connection config.

  Building the Claude adapter makes no network call, so no key needs to be valid.
- **Manual smoke test, not in CI:** run the app with `ChatProvider=Claude` and a real key. Ask the Shopping Assistant to find a product (catalog search) and then add it to the basket (add-to-basket), and confirm the basket updates. This confirms end-to-end tool calling, which the automated seams can't reach without a paid call.
- **CI** runs with no Chat Provider and no embedding provider. The Playwright journeys are unaffected.

## Out of Scope

- Any change to the Shopping Assistant's conversation logic, system prompt, tools or UI. This includes the greeting text, which still says "Concierge".
- Streaming responses.
- Claude features beyond chat with function calling: web search, MCP, code execution, files and prompt caching.
- Claude through Bedrock, Vertex or Foundry backends. Direct Anthropic API only.
- Making Catalog.API choose its embedding source explicitly.
- Embeddings from Claude. Anthropic offers no embedding model.
- The MAUI ClientApp and HybridApp.
- Fixing the deferred chat-model security finding. This spec only updates its scope note.
- An automated test that calls real Claude.

## Further Notes

- The Anthropic SDK targets net9.0, net8.0 and netstandard2.0, with no net10.0 build. The net9.0 build is used from .NET 10.
- The SDK reads `ANTHROPIC_API_KEY` from the environment by default. The registration should still check for the key itself, so that the fail-fast error is clear and appears at startup rather than on the first request.
- Nobody has confirmed that the adapter's tool calling works end to end with `UseFunctionInvocation()` against Haiku 4.5. The manual smoke test is where that gets proven. If it fails, raise it before working around it.
- `TreatWarningsAsErrors` is on, so any analyzer or obsoletion warning the new package brings in must be resolved, not suppressed.
