# Implement one ticket

You are running unattended in GitHub Actions on an Ubuntu runner with Docker. Nobody will answer questions, so make
reasonable decisions and record them in the PR body. The environment variables below were set and checked by the
workflow before you started:

- `TICKET`: the ticket id, the path relative to `.scratch/` (e.g. `<feature>/issues/NN-<slug>.md`). Its status is
  `ready-for-agent`.
- `BRANCH`: the branch to work on, named after the ticket.

Use them as `"$TICKET"` and `"$BRANCH"` in the commands below, written exactly as shown: the shell expands them,
and the workflow allows only those commands, matched on their text. Run one command per call, from the repo root:
a command chained with `;`, `&&` or `|`, or prefixed with `cd` or `env`, matches no rule and is refused. Read files
with Read, Grep and Glob, not `cat` or `ls`.

## The loop

1. Read `CLAUDE.md`, then the ticket (`node .sandcastle/issues.mjs view "$TICKET"`) and its spec,
   `.scratch/<feature>/spec.md`. Read `GLOSSARY.md` and any ADR the ticket or spec names.
2. Create the branch: `git checkout -b "$BRANCH"`. Never commit to or push `main`.
3. Implement the ticket the way the repo's skills describe: the `tdd` skill (`.claude/skills/tdd`) for the
   red → green loop where the ticket adds behaviour, CLAUDE.md's **Testing** section for which seam each test uses,
   and the `clean-code` skill (`.claude/skills/clean-code`) for every `.cs` file. Two `tdd` steps change here:
   - Nobody can confirm the seams, so take them from the ticket, the spec and CLAUDE.md's Testing table, and list
     them in the PR body.
   - The `codebase-design` and `code-review` skills it mentions aren't installed; the review runs on the PR instead.

   Do only what the ticket asks; anything else goes in the PR body as a follow-up.
4. Run the `verify` skill (`.claude/skills/verify`). Skip its UI steps and the `aspire` commands (no Aspire CLI or
   browser here) and say so in the report. If a row fails, fix it and verify again. If a test fails that your change
   could not have affected, check whether it fails without your change: `git stash push -u`, run that test project,
   `git stash pop`. If it fails there too, it's pre-existing: mark its row SKIPPED with "fails on main too: <test names>", carry on,
   and list it in the PR body for a human. If a failure is yours and still can't pass, stop: don't commit, and end
   your run explaining what failed.
5. Mark the ticket done: `node .sandcastle/issues.mjs close "$TICKET"`.
6. Commit everything (code, tests and the ticket file) with a message that says what the change does. The repo's
   verify-gate hook denies `git commit` until the verify stamp matches; that means verify again, never work around it.
7. Push: `git push -u origin "$BRANCH"`.
8. Write the PR body to `artifacts/pr-body.md` (inside the repo and git-ignored; a file outside the repo is refused),
   then open the PR against `main` with
   `gh pr create --base main --head <branch> --title "..." --body-file artifacts/pr-body.md`, with the branch name
   from your prompt written out, not `"$BRANCH"`: a variable there is refused. The body:
   - names the ticket by its path (`.scratch/$TICKET`) and links its spec;
   - says what changed and why, in a few lines;
   - includes the verify report table;
   - lists decisions you made and anything left for a human.

## Rules

- Never write `#` followed by a number anywhere in a commit message or the PR (title or body): on GitHub it links an
  unrelated issue. Write "order 11" or "ticket 04" instead.
- Never set `ESHOP_EVALS` or an API key, and never print environment variables; the evals run on the PR in CI.
- Never force-push, rebase or merge.
