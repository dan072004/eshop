# 01: Provision the Action's key, environment and GitHub App

**What to build:** The accounts and repo settings the two workflows need. These steps need the maintainer's own accounts.

- **Anthropic Console:**
  - a workspace for the Action (e.g. `eshop-claude-action`), separate from the evals' `eshop-ci`, with a monthly spend limit of $10;
  - an API key created in that workspace, used only by the Action.
- **GitHub:**
  - an environment named `claude-action` on `dan072004/eshop`, with the key as the secret `ANTHROPIC_API_KEY`;
  - no required reviewer for now, while there are no other collaborators.
- **The Claude GitHub App** installed on `dan072004/eshop` only, so the Action can open PRs that start other workflows.

See the spec's "Credentials".

**Blocked by:** None (can start immediately)

**Status:** ready-for-human

- [ ] The Action's key exists only in the `claude-action` environment: not as a repository secret, and not in the `evals` environment
- [ ] The Action's workspace has a $10 spend limit
- [ ] The Claude GitHub App is installed on `dan072004/eshop` and on no other repository
