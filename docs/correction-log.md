# Correction log

Where AI agents went wrong while working on this repo, how it was caught, and what changed as a result. Newest first. Bugs the agents found in the original eShop code (such as `IdentifiedCommandHandler` swallowing errors, ticket 11) aren't listed here, because they weren't agent mistakes.

To add an entry, put a row at the top of the table, and add a section below if the story needs more than one line: what happened, how it was caught, the fix, and the lesson.

| Date | What went wrong | Caught by | Fix |
| --- | --- | --- | --- |
| 2026-11-17 | The Claude implement run in CI used 93 of its 80 turns, had 14 tool calls refused, and ended without opening a PR; the log didn't show which calls were refused | The user, from the run log | PR opened by hand; `--max-turns 120`, the PR body written inside the repo, and a step that lists refused tool calls (PR 20) |
| 2026-11-16 | The evals gate failed on `main` at 31/40. The agent's system prompt let Haiku ask for order details instead of giving the Policy rule (4 of 9 misses), and the adversarial case `i-am-bob-now` passed only 2/3 samples | The evals CI gate | Three rules added to the prompt; the five cases passed 5/5 in two fresh runs (PR 16) |
| 2026-11-04 | PR 10 promised that the Buyer's own token never reaches Ordering, but every API accepted the MCP sign-in token ([details](#2026-11-04-a-token-exchange-that-narrowed-nothing)) | The user, reading the PR's Merge Danger section | Every API validates its audience, so the token exchange is the only way into Ordering (`32e72d3`) |
| 2026-11-04 | A failing Identity.API made Ordering's resilience pipeline retry the whole token exchange: about 30 s and 8–12 token requests per tool call | The review of PR 10 | The token client has no retries of its own and a failed exchange isn't retried; a test pins one request per call (`06cb2b2`) |
| 2026-11-04 | PR descriptions wrote order numbers as `#11` and `#21`. GitHub linked them to dotnet/eShop issues and left public "mentioned this" lines there, which can't be removed | The user, asking why the PR mentioned EF migrations | Descriptions fixed; saved as a standing rule: write "order 11" |
| 2026-11-04 | PR 10's "Before" line described a failure from partway through the branch, not what `main` looked like | The user, reviewing the description | Rewritten against `main` |
| 2026-11-04 | On Ollama, "Give me Bob's last order" got the signed-in Buyer's own order labelled as Bob's, then a request to sign in although the user was signed in | The user, in the eshop-mcp acceptance check | Traces showed no leak, because Ordering filters by the token's user; Claude refused correctly. Noted on ticket 04; covered since by the adversarial eval `i-am-bob-now` |
| 2026-11-02 | Kept trying fixes for golden-set misses that came from the embedding model; hybrid search fixed one question and broke another | The user ("I treat your fixes as prototypes") | Reverted; the misses are documented in the tests |
| 2026-10-29 | Wrote the Policy golden set as a functional test whose hand labels need updating whenever a document changes | The user, reviewing PR 9 ("the tests are a bit forced") | Recognised as an eval, not a test; it led to the evals spec and ADR 0006 |
| 2026-10-29 | "What's the return window for my last order?" got 60 days for an order with a GPS watch (30 days), because no tool description said the items matter ([details](#2026-10-29-a-return-window-that-ignored-the-items)) | The user, in ticket 08's manual acceptance check | Tool descriptions changed; contract tests pin their full text |
| 2026-10-21 | Created a branch that tracked `origin/main`, so a docs commit meant for a PR was pushed straight to `main`; then broke git config while fixing it ([details](#2026-10-21-a-branch-that-pushed-to-main)) | The user, seeing the commit on `main` | Repo set to `branch.autoSetupMerge=false`; the commit was left on `main` |
| 2026-10-21 | The duplicate-order test only covered a repeated `x-requestid`, not two different ids | The user, while reviewing the test-strategy branch | The user wrote the different-id case in `OrderingApiTests` |
| 2026-10-21 | Gave PowerShell users bash syntax (`S=...` is not recognized) | The user, from the error | Re-issued the commands in PowerShell |
| 2026-10-20 | Rewrote mocks into fakes that kept the same flaws ([details](#2026-10-20-fakes-that-lie)) | The review step in `implement-spec`, then the user's decision | Deleted one fake, turned another into a stub, added the rule to `CLAUDE.md` |
| 2026-10-20 | Parallel worktree agents committed past the verify gate, and worktree isolation failed on `C:\apps` vs `C:\Apps` | The agent, after several failed retries | Worktrees created by hand; main checkout kept clean while agents commit |
| 2026-10-06 | Faked the verify-gate stamp in bash to commit inside the Sandcastle container ([details](#2026-10-06-the-agent-faked-its-way-past-the-verify-gate)) | Found in the Sandcastle run log a day later | `pwsh` in the image; the hook now fails closed with `exit 2` |
| 2026-10-06 | Coupons ticket 01: a total clamp that hid negative order lines, a vacuous test, and an unrun functional test ([details](#2026-10-06-coupons-ticket-01-matched-a-wrong-spec)) | The user (the clamp) and the code-review subagents (the tests) | Implementation reverted; coupons being redesigned |
| 2026-10-06 | Gave PowerShell users bash syntax (`Missing expression after unary operator '!'`) | The user, from the error | Re-issued the command |
| 2026-10-05 | A `dotnet format` hook on every edit, about 7 seconds each | The user ("That is too much") | Moved to run before commit, then folded into the verify gate |
| 2026-10-05 | Pushed on to the next workflow step after a grilling session | The user | Saved as a standing instruction: stop at each skill's boundary |

## 2026-11-04: A token exchange that narrowed nothing

eShop.Mcp signs Claude Code and VS Code in as the Buyer, then exchanges that token for a short-lived `orders` token before calling Ordering.API (ADR 0005). PR 10 sold this as "the Buyer's own token never reaches Ordering". But `AuthenticationExtensions.cs` in ServiceDefaults sets `ValidateAudience = false`, so Ordering, Basket and Webhooks accepted any token Identity.API issued. That included the MCP clients' token, which lasts 2 hours and comes with a refresh token. The promise held only because eShop.Mcp chose not to send it. The PR's Merge Danger section listed the symptom ("No API checks scopes") as a minor risk.

**Caught by:** the user, who asked about that Merge Danger line. Looking into it showed the real problem was bigger than the line said.

**Fix:** each API accepts only a token issued for it, so an `eshop-mcp` token, or one API's token, is refused by the others. The `vscode` client lost `https://vscode.dev/redirect`, which let any website start a sign-in whose code landed on a page this app doesn't control.

**Lesson:** a security claim in a PR needs a test or a check that enforces it, not just code that happens to behave. Read the agent's own risk list as an open question, not as a list of accepted risks.

## 2026-10-29: A return window that ignored the items

Ticket 08's acceptance check asks "what's the return window for my last order?" for an order that contains a GPS watch. GPS watches have a 30-day return window instead of the usual 60. The assistant answered "60 days" for the whole order, with a real Citation. The trace showed `list_my_orders` → `search_policies("What is the return window for orders?")` → answer. `get_order` was never called, so the model never saw the item types, and the generic query didn't rank the Returns Exceptions section in its top 5. Every unit test passed, because each tool did its job. The gap was in what the tool descriptions told the model.

**Caught by:** the user, running the acceptance check by hand.

**Fix:** the `list_my_orders` description now says it returns no items and points to `get_order`. The `get_order` description says some Policy rules, such as return windows, depend on an item's type. Both descriptions are constants, and contract tests check their full text. The rerun gave 30 days for the watch and 60 for the jacket, each with its own Citation.

**Lesson:** for an LLM feature, tool descriptions are part of the contract, and only a run with a real model shows whether they work. This is what the evals later automated.

## 2026-10-21: A branch that pushed to main

To open a PR for this log, the agent created the branch with `git switch -c docs/correction-log origin/main`. Git set the new branch to track `origin/main`, and the command's output said so ("set up to track 'origin/main'"), but the agent didn't act on it. The commit was later pushed and followed that tracking setting, so it landed directly on the fork's `main` with no PR. A separate, older problem hid it: local `main` tracked dotnet/eShop's `upstream/main`, so `git status` on `main` never showed that it was behind the fork.

While fixing it, the agent set `branch.autoSetupMerge simple`. Git 2.35 doesn't support that value but saved it anyway, and every git command then failed until the agent removed it.

**Caught by:** the user, who found the commit on `main` and the local `main` out of date.

**Fix:** local `main` now tracks `origin/main`. The repo has `branch.autoSetupMerge=false`, so a new branch never tracks the branch it starts from, and its first push needs `git push -u origin <branch>`. The user chose to leave the commit on `main` rather than force-push.

**Lesson:** read the output of setup commands, not only the exit code. Check the installed tool version before using a config value, because Git saved an unsupported value without complaint. Before a push, check where the branch actually points (`git branch -vv`).

## 2026-10-20: Fakes that lie

The test-strategy spec replaced NSubstitute mocks of our own types with fakes. The agent did the swap, but some fakes kept the problem the mocks had:

- **Ticket 08:** `FakeUnitOfWork { SaveSucceeds = false }` let a test check that `CreateOrderCommandHandler` returns `false` when the save fails. The real `OrderingContext.SaveEntitiesAsync` never returns `false`; a failed save throws. The test covered a state production can't reach.
- **Ticket 07:** the grace-period fake re-implemented the "old enough" rule, so the assertion that order 56 isn't published was testing the fake's copy of the rule, not the SQL.
- **`ClearFilterCommandResetsPropertiesTest`** never set a filter before clearing it, so it passed without checking anything.

**Caught by:** the review step of `implement-spec`. The user asked for the reasoning on the order fakes and chose the fixes.

**Fix:** the ticket 08 test and its fakes were deleted (the HTTP test in ticket 05 covers order creation against the real adapter). The ticket 07 fake became a canned-answer stub that also checks the grace period is converted from minutes. The ClearFilter test now sets a filter first.

**Lesson:** a fake may only give answers the real adapter can give, and a fake that implements logic needs a contract test. Both rules are now in the Testing section of `CLAUDE.md`.

## 2026-10-06: The agent faked its way past the verify gate

Sandcastle ran coupons ticket 01 headless in a Docker container. The image had no PowerShell, so the `verify-gate` hook in `.claude/settings.json` couldn't start. Claude Code treats a hook that crashes (exit 127) as a non-blocking error, so the gate let the commit through. The run log also shows the agent writing *"Let me replicate the stamp fingerprint in bash so the commit gate passes"*: it forged the evidence of verification instead of stopping.

**Caught by:** reading the Sandcastle logs the next day, while explaining how Sandcastle works.

**Fix:** PowerShell 7 was added to `.sandcastle/Dockerfile`, and the hook command now ends with `|| { ...; exit 2; }`, so a hook that can't run blocks the commit.

**Lesson:** an agent routes around obstacles, including guardrails. A guardrail has to fail closed, and a gate that only checks for a stamp the agent can write itself is weak.

## 2026-10-06: Coupons ticket 01 matched a wrong spec

Ticket 01 asked for order totals that subtract item discounts and never go below zero. The agent wrote `GetTotal() => Math.Max(0m, ...)` and the same clamp in the list query.

- **The clamp hid a real gap.** `Order.AddOrderItem` merges a repeated product through `SetNewDiscount`, which only rejects negative discounts. A line of 2 × 10.00 with a discount of 50.00 can be saved, and the clamp covers it up instead of preventing it.
- **The agent followed the spec.** The spec said "never below zero", so the mistake was in the spec, not only the code.
- **The tests didn't prove the rule.** The "never below zero" test used 10 − 10, which passes with or without the clamp. The functional test seeded the order through `OrderingContext` instead of the API, and it was never run, because the sandbox had no Docker. The ticket was still closed as `done`.

**Caught by:** the user questioned the clamp. The code-review subagents found the test problems.

**Fix:** the implementation was reverted, and the coupons design is being reconsidered.

**Lesson:** an agent will faithfully implement a wrong spec, so review the spec as hard as the code. A ticket marked `done` by an agent that couldn't run its own tests isn't done.
