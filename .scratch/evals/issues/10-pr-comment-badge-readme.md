# 10: PR comment, README badge and Evals section

**What to build:** The score is visible on the PR page and in the README.

- **PR comment:** a separate job, with only `pull-requests: write` and never the key, posts one sticky comment from `summary.md` and updates it in place on later runs. It runs only for PRs from this repository, including the "not run" line.
- **Badge job:** on a push to `main` where evals ran, a separate job with `contents: write` commits `evals.json` to the `badges` branch. A run with an unchanged score commits nothing.
- **README:**
  - a shields.io endpoint badge reading the `badges` branch's `evals.json`, linked to the workflow;
  - an Evals section: what's tested (the five categories and the fixed world), how the gate works, how to run the evals locally (opt-in variable, key, Docker, filtering a case, opening the report), and what a run costs, using the measured cost from ticket 07.

See the evals spec's "CI" and "Docs".

**Blocked by:** 09 (CI workflow), 07 (40 cases and baseline)

**Status:** ready-for-agent

- [ ] A PR that changes the assistant gets one sticky comment with the score and failing cases; a second push updates the same comment
- [ ] A PR with no assistant changes gets the "not run" comment
- [ ] After a merge to `main` that runs the evals, the README badge shows the new score
- [ ] The README's Evals section matches how the suite actually runs
- [ ] (Maintainer) The eval check is marked required in branch protection once it has run on a PR
