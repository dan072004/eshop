# Evals call a real model, outside the deterministic suite

The Shopping Assistant's answers depend on a model we don't own, reacting to our system prompt, our tool descriptions and what our tools return. No fake can show whether a change to any of them still gets the right answer, so `tests/ShoppingAssistant.Evals` asks 40 fixed questions of a real Claude model and scores every answer, and CI runs it on PRs that touch the assistant. This reverses the policies-and-orders spec's rule that CI calls no LLM and needs no API key (its user story 40), for the eval project only. The deterministic suite keeps that rule: the model-calling tests are skipped unless `ESHOP_EVALS=1`, so a normal `dotnet test` stays free and needs no key.

`claude-haiku-4-5` (WebApp's default) chats, through WebApp's own chat pipeline (`ClaudeChatPipeline.Build`). `claude-sonnet-5-5` judges, so the chat model never grades itself. The seam is the Shopping Assistant's conversation class: the runner adds the case's user turns and reads back the answer, every tool call and every tool result. Behind it, policy-api is real, over real Postgres in keyword mode. Ordering.API and Catalog.API are canned at the outbound `HttpMessageHandler` from a fixed world of two Buyers (alice's orders 101–104, bob's 201–202), with the clock fixed at 2026-11-01.

Each case is sampled 3 times. A case passes at 2 of 3, an adversarial case only at 3 of 3. The gate fails the run below 75%, on any failing adversarial case, and on any judge error (a calibration answer graded against its label, or a failed judge call). The bar was 90% until the baseline came in, and the maintainer lowered it to the baseline for now, to be raised as the prompt and tools improve.

## Baseline

The first full fresh run (ticket 07, 2026-11-09): **30/40 (75%)**, with no judge errors, for **$1.02** (Haiku 401k input / 21k output tokens, $0.51; Sonnet 176k / 16k, $0.52) and just under 3 minutes.

| Category | Passed |
|---|---|
| Policy | 9/15 |
| Order | 7/10 |
| Catalog | 5/5 |
| Out of scope | 5/5 |
| Adversarial | 4/5 |

In CI (ticket 09), fresh runs scored 28/40 and 29/40 at about $1.04 each. Haiku's variance puts a full run at 28 to 30, so the gate stays red until the prompt and tool tickets land. The 10 misses keep failing with a `knownMiss` note. The largest group (5 cases) is Haiku not calling `search_policies` when a question sounds personal ("my parcel", "my helmet"). Nothing of bob's leaked in any sample of any adversarial case.

## Considered Options

- **Everything canned, policy-api included.** Rejected: the case author would decide what retrieval found, so a Citation could pass that no search returns.
- **The whole stack through Aspire** (real Identity, Ordering and Catalog). Rejected: seeded buyer ids are `Guid.NewGuid()` and no orders are seeded, so order numbers and owners would be unknowable when a case is written.
- **Haiku as the judge.** Rejected: a model grading its own answers is the bias an LLM judge is most known for, and a separate, stronger judge costs about as much as the chat.
- **Built-in quality evaluators** (Relevance, Groundedness, Equivalence, ToolCallAccuracy). Not chosen: our own rubric evaluator grades against each case's reference and rubric items, and the tool and Citation checks are deterministic. The tool-call ones are also experimental (`AIEVAL001`).

## Consequences

- **Credentials:** CI's key is the secret `ANTHROPIC_API_KEY`, stored only in the `evals` environment, from its own Anthropic Console workspace with a monthly spend limit. It is set on the eval step only. Fork and Dependabot PRs get no secrets and skip with a notice. The workflow never uses `pull_request_target`.
- **Local runs** need Docker, `ESHOP_EVALS=1`, and a key from the `ANTHROPIC_API_KEY` environment variable or the project's user-secrets. There is no fallback to an SDK login profile, so a run never spends from whichever profile is active.
- **Cached results are reused evidence, not new samples.** Every model turn and judge call is cached for 14 days, keyed by Reporting's own key plus hashes of the tool definitions, the case's entry, the world file and the judge's settings. An unchanged case replays the same 3 answers, so a replayed pass says nothing new about the model's variance. `ESHOP_EVALS_FRESH=1` (or the workflow's `fresh` input) samples every case again. The summary counts cached and fresh samples separately.
- **Cases are never reworded to pass.** They were written from the Policy files and the world before the first run, as with the golden set. A failure is fixed in the prompt, the tools or the Policies, in their own tickets.
- **Model behaviour is tested only here.** The fast tests in the eval project (checks, gate, stubs, case file) run in every normal `dotnet test`. Retrieval quality with a real embedding model stays with the on-demand golden set.
- **Cost** is about $1 per fresh run, so a PR that changes the assistant costs about that much per push, unless its cases are cached.

## Prior art

- The Policy golden set (`GoldenSetTests`, from the policies-and-orders spec): an opt-in suite behind an environment variable, with known misses left failing with notes.
- Microsoft.Extensions.AI.Evaluation: evaluators, the Reporting store and response cache, and the `aieval` report.
- Lianmin Zheng et al., "Judging LLM-as-a-Judge with MT-Bench and Chatbot Arena" (2023): LLM judges agree well with human graders, but favour their own answers (self-enhancement bias).
- Anthropic, "Define your success criteria" and "Create strong empirical evaluations": task-specific cases, automated grading by a model with a clear rubric, and many samples over hand-picked ones.
- Hamel Husain, "Your AI Product Needs Evals" (2024): assertion tests first, then model-graded ones checked against human labels.
