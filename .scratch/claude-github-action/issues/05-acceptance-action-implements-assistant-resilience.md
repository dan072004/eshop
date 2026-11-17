# 05: Acceptance: the Action implements Assistant Resilience end to end

**What to build:** The demo. The developer runs the implement workflow on the Assistant Resilience ticket, and the full loop runs on its own: the Action opens a PR, CI, the evals and the Claude review run on it, and the developer reviews and merges.

- Nobody implements the Assistant Resilience ticket locally first: no Sandcastle run and no implement skill on it.
- There is no dry run. A failed run counts as the dry run: fix the cause and run again.
- After the run, check the Action's spend in the Console against the spec's estimate (about $3–4 per implement run, $0.30–0.50 per review).

See the spec's "Testing Decisions".

**Blocked by:** 02, 04, and `assistant-resilience/issues/01` still `ready-for-agent`

**Status:** ready-for-human

- [ ] A PR is opened by `claude[bot]` from a branch that isn't `main`
- [ ] PR validation, the evals and the Claude review all run on it
- [ ] The Assistant Resilience ticket's status is `done` on the branch
- [ ] The PR's title and body name the ticket and use no `#<number>`
- [ ] The developer has reviewed and merged it
