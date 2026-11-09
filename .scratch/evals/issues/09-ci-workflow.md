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

**Status:** ready-for-agent

- [ ] A PR that changes a Policy runs the evals and shows the summary in the job summary, with the HTML report as an artifact
- [ ] A PR that changes only docs shows "Evals not run: no assistant changes." and succeeds
- [ ] A rerun of the same PR restores the cache and reports cached samples
- [ ] A manual run with "fresh" samples every case again
- [ ] The workflow log shows the key on no step other than the eval step
