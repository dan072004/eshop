# 03: Measure the saving on Haiku and keep or remove the marker

**What to build:** A measured answer, in dollars, to whether prompt caching saves money on Claude Haiku 4.5, and a keep-or-remove decision recorded in the PR. See `.scratch/prompt-caching/spec.md` ("Measuring on Haiku, and the proof", stories 9–12).

The CI evals are paused (since 2026-11-18), so the measurement runs locally instead of from the PR's CI run:

- "Before": a fresh eval run (`ESHOP_EVALS=1 ESHOP_EVALS_FRESH=1`) on main, unless a recent fresh run summary from main exists with the same prompt and cases. About $1.
- "After": a fresh eval run on the branch with tickets 01 and 02. About $1.
- Both runs must use the same system prompt and cases.

Say which paid runs a step starts before running it.

**Blocked by:** 01, 02

**Status:** ready-for-human

- [ ] Token count of the tools plus system prompt confirmed (the spec estimates ~2k, below Haiku 4.5's 4,096-token minimum)
- [ ] A few scripted local conversations with `ChatProvider=Claude` (a Policy question, a follow-up, an order question): cache tokens and estimated cost read from the model call spans in the dashboard
- [ ] Before/after estimated chat cost from the two eval run summaries, recorded in the PR
- [ ] Proven when cache reads are above zero on model calls after a Policy search **and** "after" chat cost is lower than "before"
- [ ] If either part fails: marker removed, the finding recorded in the PR; only then consider a Sonnet comparison
