# Spec: Claude reviews every PR and implements one ticket from GitHub

Status: done

## Problem Statement

Every change to eShop is reviewed and implemented in a local Claude Code session, and only while the developer is at the keyboard. Nothing reviews a PR against the repo's own rules (the Testing and Readable tests sections of CLAUDE.md, the clean-code rules, the ticket's spec) once it is on GitHub, so a breach the developer missed locally reaches `main` unnoticed. And nothing shows, to the developer or to someone reading the repo, that an agent can take a ticket from the issue tracker to a reviewed, CI-checked PR on its own.

## Solution

Two GitHub workflows built on the Claude Code GitHub Action.

- **Review:** when a same-repo PR is opened or marked ready for review, Claude reviews it once against the repo's rules and the ticket's spec, and leaves advisory comments. A `@claude review` comment asks for another pass. It never approves, blocks, or changes code.
- **Implement:** the developer starts a manual workflow run and names one `ready-for-agent` ticket. Claude implements it on a new branch, runs the repo's verify gate, marks the ticket done, and opens a PR. CI, the evals and the Claude review then run on that PR, and the developer reviews and merges it.

The demo is one run of each: the Assistant Resilience ticket implemented by the Action, with Claude's review on its PR, written up in the README.

## User Stories

1. As the developer, I want every same-repo PR reviewed by Claude when it is opened, so that I get a second reader without asking for one.
2. As the developer, I want a draft PR not reviewed until I mark it ready, so that throwaway probes and work in progress cost nothing.
3. As the developer, I want pushes after the first review not to start another review, so that the cost per PR stays predictable.
4. As the developer, I want to ask for another review with a `@claude review` comment, so that I can get a second pass after I fix the findings.
5. As the developer, I want the review to check the PR against CLAUDE.md's conventions, especially the Testing and Readable tests rules, so that it catches breaches of rules this repo wrote down, not generic style.
6. As the developer, I want the review to check the PR against the clean-code rules the repo already uses for C#, so that local and CI review agree.
7. As the developer, I want the review to check the change against the ticket and spec it implements, when the PR names one, so that missing or extra scope is flagged.
8. As the developer, I want findings as inline comments on the lines they concern, with a short summary, so that I can act on each one where it applies.
9. As the developer, I want the review to be advisory only, so that a noisy finding never blocks a merge and the evals stay the quality gate.
10. As the developer, I want the review to have no way to change the repo, so that a prompt injected through a PR's text or code can at most produce a misleading comment.
11. As the developer, I want PRs from forks and from Dependabot never reviewed, so that no outside PR can run with the review's key.
12. As the developer, I want a PR opened by the Action itself reviewed like any other, so that the implement-then-review loop runs on its own.
13. As the developer, I want to start an implementation by picking one ticket in a manual workflow run, so that the Action only ever works on what I chose.
14. As the developer, I want a run naming something that isn't a `ready-for-agent` ticket in the issue tracker to fail before any model call, so that a typo or a stray path costs nothing and can't steer the agent.
15. As the developer, I want the Action to work on its own branch and open a PR, never push to `main`, so that every change still goes through my review and CI.
16. As the developer, I want the Action to follow the repo's normal loop (read the ticket and its spec, implement, run the verify gate, commit), so that its PR meets the same bar as one I'd open.
17. As the developer, I want the verify-gate hook to work on the Linux runner as well as on my Windows machine, so that the Action is held to the same commit gate I am.
18. As the developer, I want the Action to mark the ticket done in the same PR, so that merging the PR closes the ticket, as with Sandcastle.
19. As the developer, I want the PR opened by the Action to start PR validation, the evals and the review, so that I review a PR that has already been checked.
20. As the developer, I want PR titles and bodies written by the Action never to use `#<number>`, so that they don't link to issues in the upstream dotnet/eShop repo.
21. As the developer, I want the implement run limited to about 120 turns and 45 minutes, so that a stuck run fails without a PR instead of spending without limit.
22. As the developer, I want the review limited to about 30 turns and 15 minutes, so that one review can't cost more than a few cents' worth of surprise.
23. As the developer, I want the Action's key separate from the evals' key, with a $10 limit, so that reviews can't use up the evals' budget and each cost is visible on its own.
24. As the developer, I want the key exposed only to the step that runs the Action, so that no other step or third-party action can read it.
25. As the developer, I want the review on Claude Sonnet 5.5 and the implementation on Claude Opus 5.5, so that the frequent, shallow job is cheap and the rare, deep job is capable.
26. As someone reading the repo, I want a short README section linking the reviewed PR and the PR the Action wrote, so that I can see the agentic workflow working on real code.
27. As someone reading the repo, I want a few lines comparing the Action with the local implement skill and with local review, so that I understand when each one fits.

## Implementation Decisions

- **Two workflows, both on `anthropics/claude-code-action@v1`.** One reviews, one implements. They are separate so that only the implement job ever has write access to the repo's contents.
- **Credentials.** A new GitHub environment holds `CLAUDE_CODE_OAUTH_TOKEN`, made with `claude setup-token`, so runs use the developer's Claude subscription and stop at its usage limits instead of billing. (It first held a dedicated `ANTHROPIC_API_KEY` with a $10 spend limit.) It is separate from the `evals` environment, whose evals call the Messages API directly and still need an API key. The token is set only on the Action's step. GitHub access goes through the Claude GitHub App over OIDC (`id-token: write`); no `github_token` is passed. A PR opened through the app's token starts other workflows, which a PR opened with the default workflow token would not.
- **Review triggers.** `pull_request` on `opened` and `ready_for_review`, skipping drafts, and a PR comment containing `@claude review` from a user with write access (the Action checks write access by default). Never `pull_request_target`. The job runs only when the PR's head repository is this repository and the actor isn't Dependabot, the same guard as the evals workflow. `allowed_bots` is set to `claude[bot]` only, because the Action ignores bot-triggered runs by default and would otherwise skip the Action's own PR.
- **Review permissions and tools.** Contents read, pull requests write, id-token write. Claude's tools are limited to reading the repo and the PR and posting comments: no file edits, no shell commands beyond read-only `git` and `gh` PR commands. Concurrency is one review per PR, a newer one cancelling an older one.
- **Review instructions live in a file in the repo,** not inline in the workflow, so they can be read and changed like any other prompt. They tell Claude to read CLAUDE.md and the clean-code skill, find the ticket and spec the PR names (if any) in the issue tracker, review the diff on two axes (the repo's standards, and the spec), and post inline comments plus one summary comment. Findings are ranked; style nits that the rules don't mention are left out. The review never approves or requests changes.
- **Review model and limits.** Claude Sonnet 5.5, about 30 turns, a 15-minute job timeout.
- **The verify-gate hook runs on `pwsh`.** The project's hook command currently starts Windows PowerShell, which doesn't exist on Linux runners; when the hook can't start, its fallback denies every shell call. It changes to PowerShell 7 (`pwsh`), which runs on both. Nothing else about the hook changes.
- **Implement trigger.** `workflow_dispatch` with one required input: the ticket id, written as the issue tracker writes it (the path relative to the tracker's root, `<feature>/issues/NN-<slug>.md`). The Action's source supports `workflow_dispatch` as an automation event, although its docs still say "coming soon".
- **Input validation before any model call.** A step checks that the id matches the ticket pattern, that the file exists, and that its status is `ready-for-agent`. Otherwise the run fails with a message. The id reaches the prompt through an environment variable, never by expression interpolation into a shell command.
- **Implement job.** Contents write, pull requests write, id-token write. It sets up the .NET SDK pinned in `global.json` and restores the server solution; the runner's Docker serves the functional tests. Claude works on a branch named after the ticket, never on `main`. Its instructions: read the ticket and its spec, implement it the way the repo's skills describe, run the verify skill, commit, mark the ticket done with the tracker script, push, and open a PR whose body names the ticket and never uses `#<number>`. Its tools are file edits plus the shell commands that loop needs (`dotnet`, `git`, `gh pr create`, `node` for the tracker script, `pwsh` for the hook).
- **Implement model and limits.** Claude Opus 5.5, about 120 turns (the first run needed 93 of 80), a 45-minute job timeout. A run that hits either limit ends without a PR.
- **The demo ticket** is the Assistant Resilience ticket, which nobody implements locally first. It is the only ticket the implement workflow runs as part of this spec.
- **The write-up.** A short "Agentic workflow" section in the README, linking the PR with Claude's review and the PR the Action wrote, plus three or four lines comparing the Action with the local implement skill and local review. The project spec's checkboxes for the Action and for Resilience are then ticked.
- **Steps only the developer can do:** create the key and its spend limit in the Console, create the GitHub environment and its secret, and install the Claude GitHub App on the repo.

## Testing Decisions

- The workflows have no code seam of their own, so each is tested where it runs: on GitHub, by its effect on a real PR. This follows the evals workflow, whose behaviour was proved with throwaway probe PRs.
- **Review workflow, through a throwaway probe PR** (titled as a probe, never merged):
  - opened ready for review, it gets exactly one review;
  - a push afterwards starts no review;
  - an `@claude review` comment starts one;
  - opened as a draft, it gets none until marked ready.
- **Hook, on the developer's Windows machine:** a `git commit` with unverified code changes is still denied after the switch to `pwsh`, and allowed after a passing verify. The Linux side is proved by the implement run, which can't commit unless the hook runs there. It's a one-word change, so there's no automated test.
- **Implement workflow, through the real run on the Assistant Resilience ticket.** There is no dry run: it would cost about the same and prove the same thing, so a failed real run counts as the dry run. It passes when:
  - a PR is opened by `claude[bot]` from a branch that isn't `main`;
  - PR validation, the evals and the Claude review all run on it;
  - the ticket's status is `done` on the branch;
  - a run naming a file that isn't a `ready-for-agent` ticket fails before the Action step.
- The resilience change itself is tested as its own spec says; this spec adds no C# tests.

## Out of Scope

- Turning on GitHub Issues, or triggering the Action from a GitHub issue.
- Making the review a required check, or letting it approve or request changes.
- Reviewing fork or Dependabot PRs.
- Scheduled runs, auto-fixing CI failures, and auto-merge.
- Changes to Sandcastle.
- Implementing the OpenTelemetry or prompt-caching items from Week 3.
- Keeping the review on long-term: after the first few PRs, it stays on only if it has caught something real.

## Further Notes

- Pin the Action at `@v1`, which is the current major release.
- Cost estimate, not measured: about $0.30–0.50 per review and $3–4 per implement run, so about $10 for the demo. Check the key's usage in the Console after the demo.
- Until the Action has run on the Assistant Resilience ticket, don't run Sandcastle or the implement skill on it: either would pick it up as `ready-for-agent`.
- The evals run on the Action's PR because it touches the Shopping Assistant; they bill against the `evals` key, about $1.
