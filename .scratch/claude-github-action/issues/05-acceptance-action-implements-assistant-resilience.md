# 05: Acceptance: the Action implements a ticket end to end

**What to build:** The demo. The developer runs the implement workflow on a `ready-for-agent` ticket, and the full loop runs on its own: the Action opens a PR, CI and the Claude review run on it, and the developer reviews and merges.

- There is no dry run. A failed run counts as the dry run: fix the cause and run again.

See the spec's "Testing Decisions".

**Blocked by:** 02, 04

**Status:** done

- [x] A PR is opened by `claude[bot]` from a branch that isn't `main`
- [ ] PR validation, the evals and the Claude review all run on it
- [x] The ticket's status is `done` on the branch
- [x] The PR's title and body name the ticket and use no `#<number>`
- [x] The developer has reviewed and merged it

## Comments

**Changed on 2026-11-18: the demo ran on assistant-telemetry ticket 01, not Assistant Resilience.** Assistant Resilience ticket 01 was implemented locally (PR 20) while the implement workflow was still failing, so it was no longer `ready-for-agent`. The first successful end-to-end run was on `assistant-telemetry/issues/01-one-price-table-with-cache-rates.md`: https://github.com/dan072004/eshop/pull/28 (run 44362576360, 54 of 120 turns, no refused tool calls). The developer reviewed and merged it.

Two things differ from the plan:

- **The evals didn't run.** The evals workflow was paused for speed on 2026-11-18, so only PR validation and the Claude review ran. That box stays unticked. The PR changed only cost arithmetic, not the assistant's prompt.
- **No Console spend to check.** Both workflows moved to the Claude subscription token (`CLAUDE_CODE_OAUTH_TOKEN`), so there is no per-run bill. The run log's `total_cost_usd` (about $1.02) is only an estimate. The earlier runs on the API key are what the spec's estimate was measured against.

The failed attempts counted as the dry run, and each fix shipped as its own PR: the turn limit, the final message and one command per call (PR 22), the `gh pr create` rule (PR 24), and `ANTHROPIC_BASE_URL` together with ticking the boxes (PR 25).
