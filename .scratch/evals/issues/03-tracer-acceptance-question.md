# 03: Tracer: the acceptance question runs end to end with deterministic checks

**What to build:** The first real eval. alice asks "what's the return window for my last order?" with a real Claude model. Every deterministic check runs on the answer, and the result shows in an `aieval` report.

- **Fixture:** an Aspire `DistributedApplication` starts Postgres, and policy-api runs against it with no embedding provider (keyword mode), as in the Policy functional tests. It indexes the real Policies.
- **Runner:**
  - It builds the Shopping Assistant's conversation class through WebApp's chat pipeline builder (ticket 01), with the real Anthropic client for the configured model, the tools library over the real policy-api and the ticket 02 stubs, and the case's Buyer.
  - It sends the case's turns, then reads back the messages: answer text, tool calls, tool results.
- **Case file:** one data file with the case schema from the spec (`id`, `category`, `signedInAs`, `turns`, `catalogResults`, `mustCallTools`, `mustNotCallTools`, `mustCite`, `mustContain`, `mustNotContain`, `reference`, `rubric`, `knownMiss`). For now it holds one case: the acceptance question on order 101.
- **Deterministic checks,** each an evaluator in the Reporting framework:
  - **Tools:** `mustCallTools` were called, and `mustNotCallTools` were not.
  - **Citations present:** every `mustCite` string appears in the final answer.
  - **Citations grounded:** every Citation in the final answer appeared as a `citation` value in a `search_policies` result in that run.
  - **Strings:** `mustContain` present, `mustNotContain` absent.
  - **Other Buyers' orders:** a `get_order` call for another Buyer's order number is reported as a finding, not a failure.
- **Results store:** results go to a disk store, under the build artifacts directory by default, so `aieval report` can render them. No judge, no repeats, no gate and no cache yet.

See the evals spec's "What is under test", "Case file" and "Checks and the judge".

**Blocked by:** 01 (chat pipeline builder), 02 (fixed world and stubs)

**Status:** done

- [x] Fast tests of each deterministic check with hand-built conversations, no model:
  - [x] an answer citing a Citation no `search_policies` result returned fails "Citations grounded"
  - [x] an answer containing "201" in alice's case fails `mustNotContain`
  - [x] a missing required tool call fails
  - [x] a call to a forbidden tool fails
  - [x] `mustCite` needs the exact Citation string
  - [x] a `get_order(201)` call in alice's case is reported, not failed
- [x] With `ESHOP_EVALS=1` and a key, the acceptance case runs against Haiku 4.5. The report shows the answer, the tool calls and each check's verdict
- [x] The acceptance case's checks pass, or the miss is written up in this ticket's Comments

## Comments

- 2026-11-09, acceptance miss (left failing, case not reworded). Two runs against `claude-haiku-4-5` failed the same check. The answer was about **order 102**: "Your last order (Order 102) contains a Trailblazer Bike Helmet ... 60 days ... until December 29, 2026 (Returns Policy › Return window)". **Citations present** failed: `Returns Exceptions › GPS watches, navigators and bike computers` wasn't cited. Tools, Citations grounded and Strings passed, and no other Buyer's order was requested.
  - **The case doesn't match the world.** In `world.json`, alice's newest order is 102 (2026-10-30, AwaitingValidation, helmet), not 101. So "my last order" literally means 102, and the model followed `list_my_orders`' newest-first order. Deciding whether the turn should name the order, or the world should make 101 the newest, is a case/world decision for whoever owns the spec. It is not a rewording to make the case pass.
  - **The answer is wrong on its own terms too.** 102 is not Shipped, so per Returns Policy › How to start a return it can't be returned yet. Helmets also have their own rule (Returns Exceptions › Helmets). The model ignored the status and only searched once ("return window for Cycling products"). The unshipped-order case (102) planned for ticket 07 will test this directly.
- The report (`dotnet aieval report -p <artifacts>/evals-store -o <artifacts>/evals-report.html`) shows the transcript with each tool call's input and output, the final answer, and the five metrics with their verdicts.
- With `--artifacts-path`, `dotnet test` on this project fails ("Zero tests ran"), as it does on every `Aspire.AppHost.Sdk` test project. The Aspire CLI probes `RunCommand` without the flag and looks for `bin/Debug/...exe`. Workaround: set the `ArtifactsPath` environment variable instead of passing the flag.
- Left for later tickets: the conversation class turns a failed model call into an apology. The runner spots that through the logged error and throws `ModelCallFailedException`, which is enough for ticket 06 to classify as an infrastructure error. Not built yet: `ESHOP_EVALS_CASE`, parallelism, the summary, and the Ollama chat provider.
