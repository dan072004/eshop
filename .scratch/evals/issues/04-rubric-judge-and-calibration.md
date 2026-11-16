# 04: The judge: rubric evaluator and calibration set

**What to build:** Each answer is graded against its `reference` and `rubric` by Sonnet 5.5, and a labelled calibration set proves the judge grades correctly.

- **Rubric evaluator,** in the Reporting framework:
  - It makes one judge call per sample. The judge sees the question, the final answer, `reference`, `rubric` and the Policy Sections retrieved in that run.
  - It returns one pass/fail per rubric item, with a reason.
  - A sample passes only when every deterministic check and every rubric item pass.
- **Judge client:** a fresh Anthropic client for `claude-sonnet-5-5` at low effort.
  - It must not go through WebApp's pipeline: its thinking-disabled setting gets a 400 from Sonnet 5.5.
  - Anthropic SDK 13.x sends no sampling settings, so the judge can't be pinned to temperature 0.
- **Calibration set:** about 10 fixed answers, each labelled with the rubric verdicts it should get.
  - Some are correct; others are subtly wrong (a wrong number of days, a Citation that doesn't match the rule, an invented product).
  - A fact runs the judge over all of them.
  - Any verdict that differs from its label is a judge error, reported apart from eval misses.
- **The tracer case** (ticket 03) gets a `reference` and `rubric`, and is now judged.

See the evals spec's "Checks and the judge".

**Blocked by:** 03 (tracer)

**Status:** done

- [x] The judge's client sends no thinking-disabled setting, and a call to Sonnet 5.5 succeeds
- [x] The calibration fact grades every labelled answer as labelled, or each mismatch is listed in this ticket's Comments with a fix (rubric wording, then effort)
- [x] The report shows each rubric item's verdict and reason for the acceptance case
- [x] Fast test: a calibration mismatch is reported as a judge error, separate from case results

## Comments

- 2026-11-09, first commit: order 101 re-dated to 2026-10-31 (time of day kept), so alice's list is 101, 102, 104, 103. The acceptance case's end dates are now November 30 (GPS watch, 30 days per Returns Exceptions › GPS watches, navigators and bike computers) and December 30, 2026 (jacket, 60 days per Returns Policy › Return window). Its `knownMiss` is removed. The turn is unchanged; the reference no longer says "that can be returned", because 101 is now simply the last order.
- 2026-11-09, judge: `Judge/JudgeClient.cs` wraps a fresh `AnthropicClient.AsIChatClient("claude-sonnet-5-5")` with `ReasoningEffort.Low`. The SDK sends that as `thinking: {type: adaptive}` plus `output_config.effort: low`, and no thinking-disabled setting. A fast test checks the request on the wire. `RubricEvaluator` makes one structured-output call per sample and returns a `BooleanMetric` named `Rubric: {item}` for each rubric item, with the judge's reason.
- 2026-11-09, live runs (one each):
  - **Calibration:** `JudgeGradesTheCalibrationSetAsLabelled` graded all 11 labelled answers as labelled at low effort, so the rubric wording and the effort are unchanged. It used about 14.6k input and 1.5k output tokens in total (about 1.0–1.4k input and 100–200 output per call), roughly $0.04.
  - **Acceptance:** `returns-gps-watch-last-order` passed every deterministic check and both rubric items (30 days to November 30 for the watch, 60 days to December 30 for the jacket, both cited). The judge call used 1,827 input and 138 output tokens. The report JSON shows each rubric item's value, reason and interpretation.
- For tickets 05–06:
  - **The judge's settings aren't in Reporting's cache key.** The model and effort are applied inside `JudgeClient` (`ConfigureOptions`), below where Reporting caches. A change to `JudgeClient.Effort` or the judge instructions in `RubricEvaluator` needs to go into `cachingKeys`, or it will be served stale verdicts.
  - **How the gate tells results apart:**
    - Calibration scenarios are named `judge-calibration.{id}` (`JudgeCalibration.ScenarioPrefix`).
    - `JudgeCalibration.Compare` returns `JudgeError`s and re-interprets each calibration metric as failed only when the verdict differs from its label.
    - A failed or malformed judge call shows up as an error diagnostic on a metric named `Rubric`. Reporting's composite evaluator catches the exception and records it there. `JudgeFailedException.ThrowIfAny` turns that into an infrastructure error.
  - **The two facts run in separate processes** when filtered separately, so their execution names differ. In one test run they share `EvalReporting.ExecutionName`.
