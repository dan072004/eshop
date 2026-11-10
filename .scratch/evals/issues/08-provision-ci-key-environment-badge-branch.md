# 08: Provision the CI key, environment and badge branch

**What to build:** The accounts and repo settings the CI workflow needs. These steps need the maintainer's own accounts.

- **Anthropic Console:**
  - a workspace for CI (e.g. `eshop-ci`) with a monthly spend limit (e.g. $10);
  - an API key created in that workspace, used only by CI.
- **GitHub:**
  - an environment named `evals` on `dan072004/eshop`, with the key as the secret `ANTHROPIC_API_KEY`;
  - no required reviewer for now, while there are no other collaborators.
- **A `badges` branch,** created as an orphan branch holding an initial `evals.json`: `{"schemaVersion":1,"label":"evals","message":"not run","color":"lightgrey"}`.

See the evals spec's "CI".

**Blocked by:** None (can start immediately)

**Status:** done

- [x] The CI key exists only in the `evals` environment, not as a repository secret
- [x] The CI workspace's spend limit is set
- [x] `https://raw.githubusercontent.com/dan072004/eshop/badges/evals.json` serves the initial document
