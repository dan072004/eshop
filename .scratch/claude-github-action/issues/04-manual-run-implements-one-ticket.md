# 04: A manual run implements one named ticket and opens a PR

**What to build:** The developer starts a manual workflow run and names one `ready-for-agent` ticket. Claude implements it on its own branch, runs the verify gate, marks the ticket done, and opens a PR.

- **Trigger:** `workflow_dispatch` with one required input, the ticket id as the issue tracker writes it (`<feature>/issues/NN-<slug>.md`).
- **Checked before any model call:** the id matches that pattern, the file exists, and its status is `ready-for-agent`. Otherwise the run fails with a message saying which. The id reaches the prompt through an environment variable, never by expression interpolation into a shell command.
- **Job:** the `claude-action` environment; contents write, pull requests write, id-token write; the .NET SDK from `global.json`, the server solution restored, the runner's Docker for functional tests; Claude Opus 5.5, about 80 turns, a 45-minute timeout. The key is set only on the Action's step. No `github_token` is passed: GitHub access goes through the Claude GitHub App.
- **Claude's instructions:** read the ticket and its spec; implement it the way the repo's skills describe; run the verify skill; commit on a branch named after the ticket, never `main`; mark the ticket done with the tracker script; push; open a PR whose body names the ticket and never uses `#<number>`.
- **Tools:** file edits, plus `dotnet`, `git`, `gh pr create`, `node` for the tracker script and `pwsh` for the hook.

See the spec's "Implement trigger", "Input validation before any model call" and "Implement job".

**Blocked by:** 01, 03

**Status:** ready-for-agent

- [ ] A run naming a file that doesn't exist fails before the Action's step
- [ ] A run naming an existing ticket whose status isn't `ready-for-agent` fails before the Action's step
- [ ] A run naming a path outside the ticket pattern (e.g. a spec) fails before the Action's step
- [ ] The workflow log shows the key on no step other than the Action's
