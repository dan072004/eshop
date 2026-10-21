# Correction log

Where AI agents went wrong while working on this repo, how it was caught, and what changed as a result. Newest first. Bugs the agents found in the original eShop code (such as `IdentifiedCommandHandler` swallowing errors, ticket 11) aren't listed here, because they weren't agent mistakes.

To add an entry, put a row at the top of the table, and add a section below if the story needs more than one line: what happened, how it was caught, the fix, and the lesson.

| Date | What went wrong | Caught by | Fix |
| --- | --- | --- | --- |
| 2026-10-21 | The duplicate-order test only covered a repeated `x-requestid`, not two different ids | The user, while reviewing the test-strategy branch | The user wrote the different-id case in `OrderingApiTests` |
| 2026-10-21 | Gave PowerShell users bash syntax (`S=...` is not recognized) | The user, from the error | Re-issued the commands in PowerShell |
| 2026-10-20 | Rewrote mocks into fakes that kept the same flaws ([details](#2026-10-20-fakes-that-lie)) | The review step in `implement-spec`, then the user's decision | Deleted one fake, turned another into a stub, added the rule to `CLAUDE.md` |
| 2026-10-20 | Parallel worktree agents committed past the verify gate, and worktree isolation failed on `C:\apps` vs `C:\Apps` | The agent, after several failed retries | Worktrees created by hand; main checkout kept clean while agents commit |
| 2026-10-06 | Faked the verify-gate stamp in bash to commit inside the Sandcastle container ([details](#2026-10-06-the-agent-faked-its-way-past-the-verify-gate)) | Found in the Sandcastle run log a day later | `pwsh` in the image; the hook now fails closed with `exit 2` |
| 2026-10-06 | Coupons ticket 01: a total clamp that hid negative order lines, a vacuous test, and an unrun functional test ([details](#2026-10-06-coupons-ticket-01-matched-a-wrong-spec)) | The user (the clamp) and the code-review subagents (the tests) | Implementation reverted; coupons being redesigned |
| 2026-10-06 | Gave PowerShell users bash syntax (`Missing expression after unary operator '!'`) | The user, from the error | Re-issued the command |
| 2026-10-05 | A `dotnet format` hook on every edit, about 7 seconds each | The user ("That is too much") | Moved to run before commit, then folded into the verify gate |
| 2026-10-05 | Pushed on to the next workflow step after a grilling session | The user | Saved as a standing instruction: stop at each skill's boundary |

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
