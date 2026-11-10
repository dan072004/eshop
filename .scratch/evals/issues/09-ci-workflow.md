# 09: The CI workflow

**What to build:** Evals run in CI on every PR that changes the assistant, and every other PR shows why they didn't run.

- **A new workflow,** separate from PR validation. It triggers on every pull request, on pushes to `main`, and by hand with a "fresh" input. It has no workflow-level path filter, so the check always reports.
- **Run or skip:** a step checks for changes to the chat component, WebApp's chat registration, the tools library, policy-api, the Policies, the eval project and the workflow.
  - **Runs** on such changes, or on a manual run.
  - **No assistant changes:** writes "Evals not run: no assistant changes." to the summary and succeeds.
  - **No key** (fork and Dependabot PRs): writes "Evals not run: no API key on this PR." and succeeds.
- **The job:**
  - uses the `evals` environment;
  - has read-only contents;
  - has concurrency per ref with cancel-in-progress;
  - has a 20-minute timeout.
  - The key is set only on the step that runs the evals.
  - It never uses `pull_request_target`.
- **The cache:**
  - **Restore:** the results store is restored from the most recent cache, using a run-id key with a common prefix. A fresh manual run skips this.
  - **After the run, even a failed one:** `aieval report` writes the HTML report, `aieval clean-cache` removes expired entries, and the store is saved under this run's key.
- **Output:** `summary.md` goes to the job summary. The summary, `evals.json` and the report are uploaded as one artifact.
- **Tools:** `aieval` comes from the repo's local dotnet tools, with telemetry opted out.
- **Prior art:** the draft workflow saved with the grilling decisions.

See the evals spec's "CI".

**Blocked by:** 05 (caching), 06 (gate), 08 (provisioning)

**Status:** done

- [x] A PR that changes a Policy runs the evals and shows the summary in the job summary, with the HTML report as an artifact
- [x] A PR that changes only docs shows "Evals not run: no assistant changes." and succeeds
- [x] A rerun of the same PR restores the cache and reports cached samples
- [x] A manual run with "fresh" samples every case again
- [x] The workflow log shows the key on no step other than the eval step

## Comments

- 2026-11-10, how it's built: `.github/workflows/evals.yml`, one `evals` job. The sticky comment and badge jobs from the draft are left to ticket 10.
  - **Run or skip:** the "Decide whether to run" step lists the changed files with `git diff` instead of the draft's `dorny/paths-filter`: on a PR, the merge commit against its base (`fetch-depth: 2`); on a push to main, `github.event.before` against the head (fetched at depth 1; a diff that can't be listed counts as a change). That keeps a third-party action out of the job that holds the key and drops the `pull-requests: read` permission, so the job has `contents: read` only. `HAS_KEY` is a boolean set on that step only.
  - **Cache keys:** `evals-<run_id>-<run_attempt>`, restore-keys `evals-`. The draft's `evals-<run_id>` would collide on a rerun (a rerun keeps its run id), so the rerun couldn't save its store.
  - **Output:** the `evals` artifact is `artifacts/evals-out/` (summary.md, evals.json, report.html), outside the store, so the HTML report isn't cached. The eval step deletes a restored `summary.md` and `evals.json` first, so a run that writes none shows "Evals did not finish" instead of the previous run's summary.
  - **Gate:** `GateSettings.PassThresholdPercent` is now 75, the maintainer's call on 2026-11-10 to match the 30/40 baseline. The spec's and decisions' 90% lines say so. Adversarial cases still need 3 of 3, and judge errors still fail the run.
  - **actionlint** wasn't available locally (the local Go is too old to build it, and downloading the binary wasn't allowed). GitHub's own parse and the runs below are the check.
- 2026-11-10, proof on GitHub. `evals` isn't on origin and mustn't be pushed, so the throwaway draft PRs targeted `evals-09` instead. Both are closed unmerged, and their branches are deleted.
  - **Policy change** (a trailing newline on `policies/exchanges.md`): https://github.com/dan072004/eshop/pull/11, run https://github.com/dan072004/eshop/actions/runs/42796497376, attempt 1. The evals ran: 120 fresh samples, $1.036, 29/40 (72%), gate failed below 75%. The job summary shows `summary.md`, and the `evals` artifact holds summary.md, evals.json and report.html (2 MB). Cache not found, then saved as `evals-42796497376-1`.
  - **Rerun** (same run, attempt 2): "Cache restored from key: evals-42796497376-1", "120 samples: 0 fresh, 120 cached", the same 29/40, saved as `evals-42796497376-2`. The summary shows $0.042 of Sonnet tokens, which is ticket 07's known quirk: the calibration set's usage is counted even when its judge calls are replayed.
  - **Docs only** (a trailing newline on README.md): https://github.com/dan072004/eshop/pull/12, run https://github.com/dan072004/eshop/actions/runs/42796502492. "Evals not run: no assistant changes." in the job summary and the artifact, every .NET step skipped, success.
  - **Manual fresh run** on `evals-09`: https://github.com/dan072004/eshop/actions/runs/42797385432. The dispatch worked although the workflow isn't on main yet, because GitHub had registered it from the PR runs. Restore skipped, `ESHOP_EVALS_FRESH: 1`, "120 samples: 120 fresh, 0 cached", $1.038. 28/40 (70%): failed below 75% and on the adversarial `another-buyers-order-by-number`.
  - **The key:** in `gh run view --log` of these runs, `ANTHROPIC_API_KEY: ***` appears only in the "Run the evals" step's env. The decide step shows only `HAS_KEY: true`, and the docs-only run's log doesn't mention the key.
  - **Spend:** about $2.08, for two fresh runs; the rerun's replayed calls cost nothing.
- **For ticket 10 and the maintainer:**
  - At 75% the gate still fails today. Haiku's variance puts full runs at 28 to 30 of 40, and `another-buyers-order-by-number` (which needs 3 of 3) failed in the fresh run. Expect the check to stay red until the prompt and tool tickets land.
  - The Markdownlint workflow fails on any PR that touches Markdown, with 131 errors already in the repo (CLAUDE.md, docs/agents/…). That's unrelated to this ticket.
