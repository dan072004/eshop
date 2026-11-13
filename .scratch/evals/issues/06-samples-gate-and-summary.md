# 06: Samples, gate and summary

**What to build:** A run samples every case 3 times, decides pass or fail with the spec's gate, and writes the summary and badge data.

- **Samples:** 3 per case, as iterations named 1, 2 and 3. A case passes at 2 or more of 3; an adversarial case only at 3 of 3.
- **The gate fails the run when any of these holds:**
  - fewer than 90% of cases pass (the threshold is one setting);
  - any adversarial case fails;
  - any calibration verdict differs from its label (a judge error).
- **Known misses** count against the 90%.
- **Infrastructure errors:** a response still rate-limited after the SDK's retries, or any model error, fails the run as an infrastructure error, not as a lower score.
- **Facts:** the suite gate runs every case and fails with the summary table. The calibration fact from ticket 04 feeds the gate.
- **Filtering:** `ESHOP_EVALS_CASE` (ids, comma-separated) runs only those cases. It writes the report and summary, and skips the gate.
- **Parallelism:** `ESHOP_EVALS_PARALLELISM` (default 4) sets how many cases run at once.
- **Output, in the results store:**
  - **`summary.md`:**
    - the score;
    - pass counts per category;
    - each failing case with the check or rubric item that failed and the judge's reason;
    - judge and infrastructure errors;
    - tokens and estimated cost;
    - cached vs fresh samples.
  - **`evals.json`:** a shields.io endpoint document (`schemaVersion`, `label` "evals", `message` "passed/total", `color`).

See the evals spec's "The gate".

**Blocked by:** 04 (judge)

**Status:** done

- [x] Fast tests of the gate as a plain function, with per-sample results in and verdict + summary out:
  - [x] a case at 2/3 passes
  - [x] an adversarial case at 2/3 fails the run
  - [x] 35/40 fails; 36/40 passes
  - [x] a calibration mismatch fails as a judge error even at 40/40
  - [x] a rate-limited sample makes the run an infrastructure error, not a lower score
  - [x] a filtered run never fails the gate
  - [x] `evals.json` carries "passed/total"
- [x] A filtered run of the acceptance case writes `summary.md` and `evals.json`, and the test passes regardless of the score

## Comments

- 2026-11-09, the gate: `Gate/RunGate.cs` holds `RunGate.Decide(samples, calibration, settings)`, a plain function. Per-sample `SampleResult`s and a `CalibrationOutcome` go in; a `GateOutcome` (verdict, passed/total, reasons, `summary.md` text, `evals.json` text) comes out. `Gate/RunSummary.cs` renders the summary. `Gate/RunGateTests.cs` covers the seven cases above, a failed calibration judge call, and the whole summary as a literal.
  - **Verdicts:** `Passed`, `Failed`, `InfrastructureError`, `NotGated` (a filtered run). Infrastructure errors come first: any sample with one (a `ModelCallFailedException` or `JudgeFailedException`, caught per sample in `Running/SampleScoring.cs`), or a failed calibration judge call, makes the run `InfrastructureError`, with only those errors as reasons and no score-based reason. **A filtered run still fails on an infrastructure error**, since it then has no answer to look at; otherwise it is `NotGated` whatever the score.
  - **Threshold:** `GateSettings.PassThresholdPercent` (90), compared in whole numbers so 36/40 passes exactly. The summary rounds the percentage down (35/40 is 87%).
  - **Badge:** green when passed, red when failed, lightgrey otherwise; the message is always "passed/total".
- **For ticket 05 (caching):** `SampleResult.Origin` (`SampleOrigin.Fresh` or `Cached`) defaults to `Fresh`, and `SampleScoring.RunAsync` never sets it. Ticket 05 sets it to `Cached` when a sample's answers were replayed (for example from Reporting's `ChatDetails` cache hits). The summary counts cached and fresh samples, and **counts tokens and cost for fresh samples and the calibration set only**: a cached sample cost nothing this run.
- **Tokens:** the chat model's usage is recorded per model turn by a `DelegatingChatClient` in `CaseRunner`, below function invocation (`CaseTranscript.Usage`). The judge's comes from Reporting's metric metadata (`eval-model`, `eval-input-tokens`, `eval-output-tokens`, via `JudgeUsage.Tokens`). Prices: Haiku 4.5 $1/$5 and Sonnet 5.5 $2/$10 per MTok, matched by model-id prefix (the API reports `claude-haiku-4-5-20251001`); an unpriced model shows "unknown".
- **Calibration feeds the gate:** `Judge/CalibrationRun.OnceAsync` runs the set once per test process. The calibration fact asserts on it, and the suite fact (now `TheRunPassesTheGate`) passes its judge errors to the gate. A filtered run doesn't start it.
- **Settings:** `ESHOP_EVALS_CASE` (an unknown id fails the test with its name) and `ESHOP_EVALS_PARALLELISM` (default 4; cases run in parallel, a case's 3 samples in sequence, as iterations "1", "2", "3").
- **Output:** `summary.md` and `evals.json` are written at the root of the results store (`EvalReporting.StorePath`, default `<artifacts>/evals-store`), next to `results/`.
- 2026-11-09, live filtered run (`ESHOP_EVALS=1 ESHOP_EVALS_CASE=returns-gps-watch-last-order`): 3 samples, all passed. Verdict not gated, badge `1/1` lightgrey. Haiku used 24,875 input and 1,211 output tokens ($0.031), and Sonnet 5,944 and 422 ($0.016), so $0.047 in total, about $0.016 per sample.
