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

**Status:** ready-for-agent

- [ ] Fast tests of each deterministic check with hand-built conversations, no model:
  - [ ] an answer citing a Citation no `search_policies` result returned fails "Citations grounded"
  - [ ] an answer containing "201" in alice's case fails `mustNotContain`
  - [ ] a missing required tool call fails
  - [ ] a call to a forbidden tool fails
  - [ ] `mustCite` needs the exact Citation string
  - [ ] a `get_order(201)` call in alice's case is reported, not failed
- [ ] With `ESHOP_EVALS=1` and a key, the acceptance case runs against Haiku 4.5. The report shows the answer, the tool calls and each check's verdict
- [ ] The acceptance case's checks pass, or the miss is written up in this ticket's Comments
