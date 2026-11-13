# 10: PR comment, README badge and Evals section

**What to build:** The score is visible on the PR page and in the README.

- **PR comment:** a separate job, with only `pull-requests: write` and never the key, posts one sticky comment from `summary.md` and updates it in place on later runs. It runs only for PRs from this repository, including the "not run" line.
- **Badge job:** on a push to `main` where evals ran, a separate job with `contents: write` commits `evals.json` to the `badges` branch. A run with an unchanged score commits nothing.
- **README:**
  - a shields.io endpoint badge reading the `badges` branch's `evals.json`, linked to the workflow;
  - an Evals section: what's tested (the five categories and the fixed world), how the gate works, how to run the evals locally (opt-in variable, key, Docker, filtering a case, opening the report), and what a run costs, using the measured cost from ticket 07.

See the evals spec's "CI" and "Docs".

**Blocked by:** 09 (CI workflow), 07 (40 cases and baseline)

**Status:** done

- [x] A PR that changes the assistant gets one sticky comment with the score and failing cases; a second push updates the same comment
- [x] A PR with no assistant changes gets the "not run" comment
- [ ] After a merge to `main` that runs the evals, the README badge shows the new score
- [x] The README's Evals section matches how the suite actually runs
- [ ] (Maintainer) The eval check is marked required in branch protection once it has run on a PR

Left open: the badge box, because the badge job runs only on a push to `main`, and nothing may be pushed there from this ticket. It is checked when the first merge to `main` that runs the evals commits `evals.json` to `badges`. The branch-protection box is the maintainer's.

## Comments

- 2026-11-10, how it's built (`.github/workflows/evals.yml`, two new jobs after `evals`):
  - **`comment`:** `pull-requests: write` only, no checkout, and no `environment`, so it never sees the key or runs PR code. It runs when the run wasn't cancelled, for `pull_request` events whose head repo is this repository, and not for Dependabot (its token is read-only). It downloads the `evals` artifact and posts `summary.md` behind a hidden `<!-- shopping-assistant-evals -->` marker with `gh api`, editing the bot's marked comment if there is one. No third-party sticky-comment action, for the same reason ticket 09 dropped `dorny/paths-filter`. A missing summary becomes "Evals did not finish (job result: …)". The body is cut at 60,000 characters (GitHub's limit is 65,536); a full failing run's summary is about 14,000. The footer links the run, and mentions the HTML report only when the evals ran.
  - **`badge`:** `contents: write`, only on a push to `main` where `ran` is true, even when the gate failed (a red badge is the point). It checks out `badges`, copies the run's `evals.json` over the old one and commits only when `git diff` sees a change. No `evals.json` (a run that didn't finish) leaves the badge alone. The copy-diff-commit logic was dry-run locally against a scratch repo: none, changed (one commit), unchanged (no commit). It hasn't run on GitHub.
  - **README:** a "Shopping Assistant evals" section after the Claude setup: the shields.io endpoint badge (reading `https://raw.githubusercontent.com/dan072004/eshop/badges/evals.json`, linked to the workflow), the five categories with examples, the fixed world, the gate (75%, adversarial 3 of 3, judge errors fail the run), the CI behaviour, the local commands (the same ones as CLAUDE.md, plus `ESHOP_EVALS_FRESH=1` and `dotnet tool restore`), and the cost.
  - No TDD: the change is workflow YAML and README prose, with no C# behind it. The checks were the local dry run and the runs below.
- 2026-11-10, proof on GitHub. Draft PRs against `evals-10` (`evals` isn't on origin), both closed unmerged and their branches deleted.
  - **Assistant change** (a code comment in `src/eShop.Assistant.Tools/AssistantToolNames.cs`, which changes no tool definition and so no cache key): https://github.com/dan072004/eshop/pull/13.
    - First push, run https://github.com/dan072004/eshop/actions/runs/42800803440: no cache to restore, 120 fresh samples, 29/40 (72%), failed below 75% and on the adversarial `i-am-bob-now` (2/3). $1.044. The comment job posted https://github.com/dan072004/eshop/pull/13#issuecomment-6765252415 with the score, the reasons, the categories and every failing case with its judge reasons.
    - Second push, run https://github.com/dan072004/eshop/actions/runs/42801388999: "Cache restored from key: evals-42800803440-1", 120 cached and 0 fresh, the same 29/40. The same comment (same id) was edited at 10:43:38, created at 10:41:55, and the PR still has one comment. The summary shows $0.041 of Sonnet tokens: ticket 07's quirk, calibration usage counted when its judge calls are replayed.
    - `ANTHROPIC_API_KEY: ***` appears only in the "Run the evals" step's env.
  - **Docs only** (a blank line in CONTRIBUTING.md): https://github.com/dan072004/eshop/pull/14. Runs https://github.com/dan072004/eshop/actions/runs/42800808289 and https://github.com/dan072004/eshop/actions/runs/42801391956 both posted "Evals not run: no assistant changes." to one comment (https://github.com/dan072004/eshop/pull/14#issuecomment-6765184862), edited in place by the second run, which also showed the footer fix from `f5e72c5`.
  - **Spend:** about $1.09: one fresh run ($1.044) and one cached run ($0.041).
- **Open:**
  - The badge job's first real run is the first merge to `main` that touches the assistant. Until then the badge reads "not run".
  - At 75% the check is still red (29/40 here, with an adversarial miss that wasn't in the baseline). Expect that until the prompt and tool tickets land.
  - `RunSummary` still counts the calibration set's tokens on a fully cached run (ticket 07), so the README's "about $0" is about $0.04 in the summary.
