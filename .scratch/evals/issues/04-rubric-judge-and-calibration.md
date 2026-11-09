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

**Status:** ready-for-agent

- [ ] The judge's client sends no thinking-disabled setting, and a call to Sonnet 5.5 succeeds
- [ ] The calibration fact grades every labelled answer as labelled, or each mismatch is listed in this ticket's Comments with a fix (rubric wording, then effort)
- [ ] The report shows each rubric item's verdict and reason for the acceptance case
- [ ] Fast test: a calibration mismatch is reported as a judge error, separate from case results
