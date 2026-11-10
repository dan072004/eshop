# 11: ADR 0006 and the docs that change with it

**What to build:** The decision to call a real model from CI is on record, and the repo's agent docs know the evals exist.

- **ADR 0006, "Evals call a real model, outside the deterministic suite":**
  - **Why:** the behaviour lives in a model we don't own, reacting to our prompt and tools.
  - **What it reverses:** the policies-and-orders rule that CI calls no LLM and needs no key, for the eval project only.
  - **The models:** Haiku 4.5 chats and Sonnet 5.5 judges.
  - **The seam:** real policy-api; Ordering and Catalog canned.
  - **Caching:** cached results are reused evidence, not new samples.
  - **The baseline** from ticket 07.
  - **Rejected options:** everything canned; the whole stack; a Haiku judge.
- **The policies-and-orders spec:** a note on user story 40 that ADR 0006 supersedes it for the eval project.
- **CLAUDE.md:**
  - a Testing-table row for evals (model behaviour with our prompt and tools, tested with a real model in the eval project);
  - the opt-in eval commands in Commands.
- **The `verify` skill:** reports "evals: not run, opt-in" when the diff touches the assistant's paths, and never runs the evals itself.
- **GLOSSARY:** no new terms.

See the evals spec's "Docs".

**Blocked by:** 07 (40 cases and baseline), 09 (CI workflow)

**Status:** done

- [x] ADR 0006 exists in `docs/adr/`, following the existing ADRs' format, with the measured baseline
- [x] Story 40's note points to ADR 0006
- [x] CLAUDE.md's Testing table and Commands mention the evals and how to opt in
- [x] `verify` on a diff that touches the tools library reports "evals: not run, opt-in"

## Comments

- 2026-11-10, commit `fa5822e`:
  - **ADR 0006** (`docs/adr/0006-evals-call-a-real-model-outside-the-deterministic-suite.md`), in 0005's format plus a Baseline section: 30/40 fresh for $1.02 (ticket 07, per category), and CI's fresh runs at 28 and 29 of 40 for about $1.04 (ticket 09). It records the 75% bar (lowered from 90% to the baseline), adversarial 3 of 3 and judge errors failing the run, and the key in the `evals` environment. Rejected options: everything canned, the whole stack, Haiku as judge, and the built-in quality evaluators (spec Out of Scope).
  - **Story 40** gets an indented "Superseded for the eval project by ADR 0006" line.
  - **CLAUDE.md:** a Testing-table row for model behaviour, the user-secrets, full-run, filtered-run and `aieval report` commands, and a bullet on the opt-in, the cache and CI.
  - **verify:** a step 4 "Evals" block whose paths are `evals.yml`'s filter; on a match it adds `Evals | SKIPPED | evals: not run, opt-in`, which is the one SKIPPED row that still allows **Verified**. It must never set `ESHOP_EVALS`, `ESHOP_EVALS_FRESH` or a key. Also new: test-table rows for `src/eShop.Assistant.Tools` and `src/Policy.API`/`policies/` (none existed), and `policies/` counts as code in step 1, so a Policy-only change is not "docs-only". The report example's "order #12" is now "order 12".
- **How the verify change was checked** (it is prose, so red/green was by hand, not a test): red, the old skill had no mention of evals at all (`grep -c -i eval` gave 0). Green, a probe line added to `src/eShop.Assistant.Tools/AssistantToolNames.cs` made the scope step's diff match the assistant paths, which gives the evals row. The probe was then reverted.
- **Verify report for this ticket:** docs-only, nothing to verify. No code path changed and `evals.yml` didn't change, so there is no evals row.
- **Open:**
  - The verify-gate hook's `$codePaths` doesn't include `policies/`, so a Policy-only change can still be committed without a stamp. Left alone, since the hook isn't in this ticket.
  - The README's Evals section is ticket 10.
