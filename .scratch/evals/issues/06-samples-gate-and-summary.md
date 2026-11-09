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

**Status:** ready-for-agent

- [ ] Fast tests of the gate as a plain function, with per-sample results in and verdict + summary out:
  - [ ] a case at 2/3 passes
  - [ ] an adversarial case at 2/3 fails the run
  - [ ] 35/40 fails; 36/40 passes
  - [ ] a calibration mismatch fails as a judge error even at 40/40
  - [ ] a rate-limited sample makes the run an infrastructure error, not a lower score
  - [ ] a filtered run never fails the gate
  - [ ] `evals.json` carries "passed/total"
- [ ] A filtered run of the acceptance case writes `summary.md` and `evals.json`, and the test passes regardless of the score
