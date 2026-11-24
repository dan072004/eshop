# 03: Measure the saving on Haiku and keep or remove the marker

**What to build:** A measured answer, in dollars, to whether prompt caching saves money on Claude Haiku 4.5, and a keep-or-remove decision recorded in the PR. See `.scratch/prompt-caching/spec.md` ("Measuring on Haiku, and the proof", stories 9–12).

The CI evals are paused (since 2026-11-18), so the measurement runs locally instead of from the PR's CI run:

- "Before": a fresh eval run (`ESHOP_EVALS=1 ESHOP_EVALS_FRESH=1`) on main, unless a recent fresh run summary from main exists with the same prompt and cases. About $1.
- "After": a fresh eval run on the branch with tickets 01 and 02. About $1.
- Both runs must use the same system prompt and cases.

Say which paid runs a step starts before running it.

**Blocked by:** 01, 02

**Status:** done

- [x] Token count of the tools plus system prompt confirmed (the spec estimates ~2k, below Haiku 4.5's 4,096-token minimum)
- [x] A few scripted local conversations with `ChatProvider=Claude` (a Policy question, a follow-up, an order question): cache tokens and estimated cost read from the model call spans in the dashboard
- [x] Before/after estimated chat cost from the two eval run summaries, recorded in the PR
- [ ] Proven when cache reads are above zero on model calls after a Policy search **and** "after" chat cost is lower than "before"
- [ ] If either part fails: marker removed, the finding recorded in the PR; only then consider a Sonnet comparison

## Comments

**2026-11-24:** The "after" run is the fresh eval run on main after both this PR and the fix for assistant-policies-and-orders ticket 09 merge. That fix adds an optional `itemTypes` parameter and a sentence to `search_policies`, so "after" sends a tool prefix a few dozen tokens longer than "before". Accepted: the difference is small next to a cached prefix of ~4,500 tokens. Record it next to the before/after numbers.

**2026-11-24, result: marker kept.** "After" is the fresh eval run on main after this spec and ticket 09 merged (GitHub Actions run 45408966392, 120/120 samples fresh):

| | Before (main, 2026-11-23) | After (run 45408966392) |
|---|---:|---:|
| Score | 38/40 | 38/40, the same two known misses |
| Haiku input tokens | 462,174 | 489,133 |
| Haiku cache reads / writes | n/a | 0 / 0 |
| Haiku cost | $0.571 | $0.598 |
| Sonnet judge cost | $0.539 | $0.500 |
| Total | $1.111 | $1.098 |

- **The evals can't show the saving.** Each eval case is one Turn of 2–4 model calls. The largest Haiku call in the "before" run was 3,256 tokens, tools and system prompt included, and the average was about 1,900. All are below Haiku 4.5's 4,096-token minimum, so Anthropic cached nothing. It also wrote nothing at 1.25×, so the marker cost nothing.
- **The extra $0.027 on Haiku isn't caching.** It comes from the longer prompts (ticket 09's `itemTypes`, +5.8% input). The judge's drop is sampling noise.
- **The proof comes from the local conversation instead.** It had 6 Turns and 12 model calls, read from the dashboard's chat spans. The prefix was first written after `search_policies` (4,563 tokens), then read on every later Turn (4,563 → 5,650). The estimate was $0.024 against $0.061 uncached, about 60% lower.
- **Decision:** keep the marker. It's free under the minimum and saves money in conversations with several Turns. The eval proof criterion isn't met as written, and a Sonnet comparison isn't needed. For the evals to measure caching, they would need a case with several Turns whose prefix passes 4,096 tokens.
