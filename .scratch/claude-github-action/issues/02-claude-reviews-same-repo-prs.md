# 02: Claude reviews same-repo PRs, with `@claude review` for another pass

**What to build:** When a same-repo PR is opened ready for review, or marked ready, Claude reviews it once against the repo's rules and the ticket's spec and leaves advisory comments. A `@claude review` comment asks for another pass.

- **Triggers:** `pull_request` on `opened` and `ready_for_review`, skipping drafts; a PR comment containing `@claude review` from a user with write access. Never `pull_request_target`.
- **Guard:** runs only when the PR's head repository is this repository and the actor isn't Dependabot, as in the evals workflow. `allowed_bots` is `claude[bot]` only, so the Action's own PRs are reviewed.
- **Job:** the `claude-action` environment; contents read, pull requests write, id-token write; one review per PR, a newer one cancelling an older one; Claude Sonnet 5.5, about 30 turns, a 15-minute timeout. The key is set only on the Action's step.
- **Tools:** reading the repo and the PR and posting comments only. No file edits, no shell beyond read-only `git` and `gh` PR commands.
- **Review instructions in a file in the repo:** read CLAUDE.md and the clean-code skill; find the ticket and spec the PR names, if any; review the diff for the repo's standards and for the spec; post ranked inline comments plus one summary comment; leave out nits the rules don't mention; never approve or request changes.

See the spec's "Review triggers", "Review permissions and tools" and "Review instructions".

**Blocked by:** 01

**Status:** ready-for-agent

- [ ] A throwaway probe PR, opened ready for review, gets exactly one review: inline comments and one summary comment
- [ ] A push to that PR afterwards starts no review
- [ ] An `@claude review` comment on it starts one
- [ ] A probe PR opened as a draft gets no review until it is marked ready
- [ ] The workflow log shows the key on no step other than the Action's
- [ ] The probe PRs are closed unmerged
