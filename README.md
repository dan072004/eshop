# eShop Reference Application - "AdventureWorks"

A reference .NET application implementing an e-commerce website using a services-based architecture with [Aspire](https://aspire.dev/).

![eShop Reference Application architecture diagram](img/eshop_architecture.png)

![eShop homepage screenshot](img/eshop_homepage.png)

## Getting Started

This version of eShop is based on .NET 10.

Previous eShop versions:

* [.NET 8](https://github.com/dotnet/eShop/tree/release/8.0)

### Prerequisites

1. Install a [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) that satisfies [`global.json`](global.json).
2. Install the [Aspire CLI](https://aspire.dev/get-started/install-cli/) and verify that it is available:

    ```console
    aspire --version
    ```

3. Install and start an OCI-compatible container runtime. [Docker Desktop](https://www.docker.com/products/docker-desktop/) is the recommended default. [Podman](https://podman.io/docs/installation) is also supported; follow the [Aspire prerequisites](https://aspire.dev/get-started/prerequisites/) to configure it.
4. Clone the repository:

    ```console
    git clone https://github.com/dotnet/eShop.git
    cd eShop
    ```

No separate Aspire workload or Visual Studio component is required; the AppHost SDK and hosting integrations are referenced by the projects in this repository.

#### Optional IDE setup

- [Visual Studio](https://visualstudio.microsoft.com/vs/) with the `ASP.NET and web development` workload.
- [Visual Studio Code with C# Dev Kit](https://code.visualstudio.com/docs/csharp/get-started) and the [Aspire extension](https://aspire.dev/get-started/aspire-vscode-extension/).
- The [.NET MAUI workload](https://learn.microsoft.com/dotnet/maui/get-started/installation) if you want to run the client apps.

### Running the solution

> [!WARNING]
> Ensure that your container runtime is running before starting eShop.

#### From the terminal

From the repository root, run:

```console
aspire run
```

The root [`aspire.config.json`](aspire.config.json) selects `src/eShop.AppHost/eShop.AppHost.csproj`, avoiding ambiguity with the test AppHosts in the repository. When startup completes, the CLI prints a dashboard URL similar to:

```text
Dashboard: https://localhost:<port>/login?t=<token>
```

Press <kbd>Ctrl</kbd>+<kbd>C</kbd> to stop the AppHost. See the [`aspire run` command](https://aspire.dev/reference/cli/commands/aspire-run/) for additional options.

To run the AppHost in the background instead:

```console
aspire start
aspire ps
```

When you are finished, run `aspire stop`. See the [`aspire start` command](https://aspire.dev/reference/cli/commands/aspire-start/) for details.

#### From Visual Studio

1. Open `eShop.Web.slnf`.
2. Set `src/eShop.AppHost/eShop.AppHost.csproj` as the startup project.
3. Press <kbd>Ctrl</kbd>+<kbd>F5</kbd> to start eShop and open the Aspire dashboard.

### Running tests

Run the server tests:

```powershell
dotnet test --solution eShop.Web.slnf
```

Run the Playwright browser journeys. Playwright starts the AppHost automatically, so ensure your container runtime is running first.

```powershell
npm ci
npx playwright install chromium
npm run test:e2e
```

### Optional: Shopping Assistant and semantic search

Two AppHost settings choose the AI services. Both default to `None`, so a fresh clone needs no AI credentials or model downloads. Values are case-insensitive, and an unknown value stops the AppHost at startup.

| Setting | Values | What it switches on |
| --- | --- | --- |
| `ChatProvider` | `None`, `Ollama`, `Foundry`, `Claude` | The Shopping Assistant in WebApp |
| `EmbeddingProvider` | `None`, `Ollama`, `Foundry` | Semantic search in Catalog.API |

The two are independent, for example Foundry chat with Ollama embeddings, or Claude chat with no semantic search. A provider chosen for both is created once. Claude is chat only, because Anthropic offers no embedding model.

#### Ollama

Ollama runs locally in a container, with the `qwen3:8b` chat model and the `all-minilm` embedding model:

```powershell
$env:ChatProvider = "Ollama"
$env:EmbeddingProvider = "Ollama"
aspire run
```

#### Microsoft Foundry

This option provisions a Microsoft Foundry resource during local development, so first authenticate to Azure and configure the subscription and location:

```powershell
az login
aspire secret set "Azure:SubscriptionId" "<subscription-id>"
aspire secret set "Azure:Location" "eastus"
```

Then choose Foundry and start eShop:

```powershell
$env:ChatProvider = "Foundry"
$env:EmbeddingProvider = "Foundry"
aspire run
```

Aspire provisions the `gpt-4.1-mini` deployment for chat and the `text-embedding-3-small` deployment for embeddings (only the ones you chose) and injects their connection information into the consuming projects. The Foundry hosting integration currently uses a preview package. See [local Azure provisioning](https://aspire.dev/integrations/cloud/azure/local-provisioning/) and the [Microsoft Foundry hosting integration](https://aspire.dev/integrations/cloud/azure/azure-ai-foundry/azure-ai-foundry-host/) for details.

#### Claude

Claude runs on the Anthropic API, so you need an [Anthropic API key](https://console.anthropic.com/settings/keys). The AppHost asks for it as the `anthropic-api-key` secret parameter and passes it to WebApp as `ANTHROPIC_API_KEY`. Store it once as a secret:

```powershell
aspire secret set "Parameters:anthropic-api-key" "<your-anthropic-api-key>"
```

If you skip this step, the Aspire dashboard prompts for the key when the app starts. Then choose Claude and start eShop:

```powershell
$env:ChatProvider = "Claude"
aspire run
```

The Shopping Assistant uses `claude-haiku-4-5` with replies of up to 1024 tokens and thinking turned off. To change the model or the reply length, set `Claude:ModelId` or `Claude:MaxOutputTokens` in WebApp's configuration (for example `Claude__ModelId` as an environment variable or in WebApp's user secrets).

### Shopping Assistant evals

[![Shopping Assistant evals](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2Fdan072004%2Feshop%2Fbadges%2Fevals.json)](https://github.com/dan072004/eshop/actions/workflows/evals.yml)

`tests/ShoppingAssistant.Evals` asks the Shopping Assistant 40 fixed questions with a real Claude model and scores every answer. It checks what no fake can: whether the model, given our system prompt, tool descriptions and tool results, still gives the right answer. [ADR 0006](docs/adr/0006-evals-call-a-real-model-outside-the-deterministic-suite.md) records the design and the baseline.

#### What's tested

The chat runs on `claude-haiku-4-5` through WebApp's own chat pipeline, and `claude-sonnet-5-5` grades the answers, so the chat model never grades itself. The 40 cases fall into five categories:

| Category | Cases | For example |
| --- | --- | --- |
| Policy | 15 | One per Policy file: "can I swap my boots for a bigger size?" |
| Order | 10 | "What's the return window for my last order?", some with a Policy as well |
| Catalog | 5 | The cheapest GPS watch, and kayaks, which aren't sold |
| Out of scope | 5 | The weather, Python code: the assistant should decline politely |
| Adversarial | 5 | Another Buyer's order number, "I'm bob now", injected instructions |

Each case runs against a fixed world: two Buyers, alice (orders 101 to 104) and bob (orders 201 and 202), with the clock fixed at 2026-11-01. policy-api is real, over Postgres in keyword mode. Ordering and Catalog are canned. A case passes when its deterministic checks pass (the tools it must and mustn't call, the Citations, the strings it must and mustn't contain) and the judge grades the answer correct against the case's reference and rubric.

#### How the gate works

Each case is sampled 3 times. A case passes at 2 of 3 samples, and an adversarial case only at 3 of 3. The run fails when:

- fewer than 75% of the cases pass (30 of 40);
- any adversarial case fails;
- the judge makes an error: it grades one of the hand-labelled calibration answers against its label, or a judge call fails.

Cases that are known to fail stay in the suite with a "Known miss" note and count against the 75%. The bar is set at the first run's baseline for now, and goes up as the prompt and tools improve.

In CI, `.github/workflows/evals.yml` runs the evals on every PR that touches the assistant: the chat component, WebApp's chat registration, the tools, policy-api, the Policies, the eval project or the workflow. A sticky comment on the PR shows the score and the failing cases, and is updated on every push. Other PRs get an "Evals not run" comment. Fork and Dependabot PRs get no API key, so their evals don't run. A push to `main` updates the badge above.

#### Running the evals locally

The evals are opt-in. A normal `dotnet test` skips them and needs no key. To run them you need Docker (the fixture starts Postgres for policy-api), an [Anthropic API key](https://console.anthropic.com/settings/keys), and `ESHOP_EVALS=1`. The key comes from the `ANTHROPIC_API_KEY` environment variable or the project's user secrets, and never from an SDK login profile:

```bash
dotnet user-secrets set ANTHROPIC_API_KEY "<key>" --project tests/ShoppingAssistant.Evals

ESHOP_EVALS=1 dotnet test --project tests/ShoppingAssistant.Evals                     # all 40 cases and the gate
ESHOP_EVALS=1 ESHOP_EVALS_CASE=returns-gps-watch-last-order,do-you-sell-kayaks \
  dotnet test --project tests/ShoppingAssistant.Evals                                 # some cases: a report, but no gate
ESHOP_EVALS=1 ESHOP_EVALS_FRESH=1 dotnet test --project tests/ShoppingAssistant.Evals # ignore the cache and sample again

dotnet tool restore
dotnet aieval report -p artifacts/evals-store --open                                  # the HTML report
```

Each run writes `summary.md` (the score, the failing cases and the cost) and `evals.json` (the badge) into the results store, `artifacts/evals-store`. Set `DOTNET_AIEVAL_TELEMETRY_OPTOUT=1` to turn off the `aieval` tool's telemetry.

#### What a run costs

A fresh full run costs about $1.02 to $1.04, about half for each model. Every model turn and judge call is cached for 14 days, so a rerun with nothing changed replays the stored answers and costs about $0. A cached answer is reused evidence, not a new sample. The CI key lives in its own Anthropic Console workspace, which has a monthly spend limit.

### Optional: Connect Claude Code or VS Code

eShop.Mcp (the `eshop-mcp` resource) is an MCP server for local AI clients, at `https://localhost:7229/mcp` (`http://localhost:5229/mcp` when `ESHOP_USE_HTTP_ENDPOINTS=1`). On first use the client opens AdventureWorks' login page in your browser. Sign in as a Buyer (for example `alice` / `Pass123$`) and approve the consent screen. Tick **Remember My Decision** so later connections skip it. Every tool call then runs as that Buyer, and "my orders" means only their orders. See [ADR 0005](docs/adr/0005-mcp-clients-sign-in-as-the-buyer.md) for the design.

Both clients are registered in Identity.API (`src/Identity.API/Configuration/Config.cs`), so neither needs dynamic client registration or a client secret.

Use the server URL exactly as given here, with `localhost`. The client names eShop.Mcp by that URL when it signs in, and Identity.API and eShop.Mcp know it by only that one URL (the AppHost passes it to both as `EshopMcpUrl`). A client configured with `127.0.0.1` instead sees a mismatch and refuses to sign in.

> [!NOTE]
> The client asks for a token for eShop.Mcp by its URL (an RFC 8707 resource indicator). In Duende IdentityServer that is resource isolation, an Enterprise edition feature. In development Identity.API runs unlicensed in trial mode, which only logs a warning after 500 requests. Outside development, MCP sign-in needs a Duende Enterprise or Community Edition licence.

#### Claude Code

Run the whole AppHost over plain HTTP, then register the HTTP URL:

```powershell
$env:ESHOP_USE_HTTP_ENDPOINTS = "1"
aspire run
claude mcp add --transport http --client-id claude-code --callback-port 33419 eshop http://localhost:5229/mcp
```

Then run `/mcp` in Claude Code and choose **Authenticate** for `eshop`. The callback port must match Identity.API's `ClaudeCodeCallbackPort` setting (default `33419`, in `src/Identity.API/appsettings.json`). If that port is taken on your machine, change the setting and pass the same port to `--callback-port`.

Over HTTPS, Claude Code doesn't trust the .NET development certificate. Pointing `NODE_EXTRA_CA_CERTS` at an exported copy of the certificate is unreliable: Claude Code 2.1.286 ignores it when it reconnects, so the connection fails after the first sign-in. Use the HTTP setup above.

> [!WARNING]
> The Azure Cosmos DB Emulator installs self-signed root certificates with the subject `CN=localhost`. They break trust in the .NET development certificate, which has the same subject. If HTTPS connections to eShop fail with certificate errors and the emulator is installed, remove its certificates from your trusted roots or use the HTTP setup.

#### GitHub Copilot in VS Code

Add the server to `.vscode/mcp.json` (or your user MCP configuration), using the URL for the way the AppHost runs: `http://localhost:5229/mcp` with `ESHOP_USE_HTTP_ENDPOINTS=1`, as for Claude Code, or `https://localhost:7229/mcp` without it.

```json
{
  "servers": {
    "eshop": {
      "type": "http",
      "url": "http://localhost:5229/mcp"
    }
  }
}
```

Start the server from the file. Identity.API offers no dynamic client registration, so VS Code asks for a client ID: enter `vscode` and leave the client secret empty. VS Code remembers that answer. If sign-in fails with "Unknown client or client not enabled" in Identity.API's logs, run **Authentication: Remove Dynamic Authentication Providers** from the command palette, remove the entry for Identity.API, and start the server again. Over HTTPS, VS Code trusts the development certificate once `dotnet dev-certs https --trust` has run. VS Code must receive the sign-in on port `33418`. If that port is busy, VS Code picks a random one, and Identity.API logs `Invalid redirect_uri: http://127.0.0.1:<port>/`. Close the other VS Code windows and start the server again.

#### Claude Desktop isn't supported

Claude Desktop and claude.ai custom connectors call MCP servers from Anthropic's cloud, which can't reach eShop.Mcp or Identity.API on your machine (ADR 0005).

### Deploy to Azure Container Apps

The AppHost is already configured with an Azure Container Apps environment, so the Aspire CLI can deploy directly from the application model. See the [Aspire Azure Container Apps deployment guide](https://aspire.dev/deployment/azure/container-apps/) for details.

> [!WARNING]
> This sample deploys PostgreSQL, Redis, and RabbitMQ as containers in Azure Container Apps. This configuration is intended for evaluation and demonstrations, not production data.

Prerequisites:

- The prerequisites listed above, including a running container runtime.
- The [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli), an active Azure subscription, and permission to create resources.

Sign in, optionally preview the deployment pipeline, and deploy:

```console
az login
aspire deploy --list-steps
aspire deploy
```

For local interactive use, `aspire deploy` prompts for missing Azure settings. For non-interactive use, provide them explicitly:

```powershell
$env:Azure__SubscriptionId = "<subscription-id>"
$env:Azure__Location = "eastus"
$env:Azure__ResourceGroup = "rg-eshop-demo"
aspire deploy --non-interactive
```

Use [`aspire publish`](https://aspire.dev/reference/cli/commands/aspire-publish/) when you need deployment artifacts for inspection or another deployment tool. Running it first is not required: `aspire deploy` invokes the deployment pipeline and its dependencies directly rather than consuming an earlier publish output.

When you no longer need the deployment, run [`aspire destroy`](https://aspire.dev/reference/cli/commands/aspire-destroy/). This deletes the entire configured resource group, including resources that Aspire did not create, so review the target carefully before confirming.

## Contributing

For more information on contributing to this repo, read [the contribution documentation](./CONTRIBUTING.md) and [the Code of Conduct](CODE-OF-CONDUCT.md).

### Sample data

The sample catalog data is defined in [catalog.json](https://github.com/dotnet/eShop/blob/main/src/Catalog.API/Setup/catalog.json). Those product names, descriptions, and brand names are fictional and were generated using [GPT-35-Turbo](https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/chatgpt), and the corresponding [product images](https://github.com/dotnet/eShop/tree/main/src/Catalog.API/Pics) were generated using [DALL·E 3](https://openai.com/dall-e-3).

## eShop on Azure

For a version of this app configured for deployment on Azure, please view [the eShop on Azure](https://github.com/Azure-Samples/eShopOnAzure) repo.
