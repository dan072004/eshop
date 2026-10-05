---
name: verify
description: Verify a change before declaring work done. Use at the end of any task that modified code, config or tests in this repo, and before saying "done", committing, or opening a PR. Builds, tests and checks the diff, then reports evidence. Also required when a `git commit` is denied because changes haven't passed verification.
---

# Verify before done

Never say a task is complete until this skill's report has no FAIL rows. Every SKIPPED row needs a reason the user can act on. Run the commands; don't infer results from reading code.

## 1. Scope

```bash
git status --porcelain
git diff main...HEAD --name-only     # committed on this branch
git diff HEAD --name-only            # uncommitted
```

Focus on the files changed in this task. If nothing under `src/`, `tests/`, `e2e/`, `*.props`, `global.json` or `eShop.Web.slnf` changed, report "docs-only, nothing to verify" and stop.

## 2. Build (always)

- Run `aspire ps`. If the app is running, its DLLs are locked, so add `--artifacts-path artifacts-verify` to build and test commands (see `.github/skills/aspire-orchestration`).
- `dotnet build eShop.Web.slnf`. `TreatWarningsAsErrors` is on, so the bar is zero warnings.
- If `src/ClientApp/**` or `src/HybridApp/**` changed, build those projects separately, because they're outside the slnf.

## 3. Format (changed `.cs` files)

```bash
dotnet format whitespace . --folder --verify-no-changes --include <changed .cs files>
```

## 4. Tests: choose by changed path

| Changed path | Run |
|---|---|
| `src/Ordering.*`, `src/OrderProcessor` | `tests/Ordering.UnitTests`, `tests/Ordering.FunctionalTests` |
| `src/Basket.API` | `tests/Basket.UnitTests`, `tests/Basket.FunctionalTests` |
| `src/Catalog.API` | `tests/Catalog.FunctionalTests` |
| `src/Webhooks.API` | `tests/Webhooks.FunctionalTests` |
| `src/WebApp`, `src/WebAppComponents` | `tests/Application.UnitTests` + UI check (step 6) |
| `src/eShop.AppHost` | `tests/eShop.AppHost.UnitTests` |
| `src/EventBus*`, `src/IntegrationEventLogEF`, `src/eShop.ServiceDefaults`, `src/Shared`, `Directory.*.props`, `global.json` | everything: `dotnet test --solution eShop.Web.slnf` |
| `tests/X` | `tests/X` |

- Use `dotnet test --project <path>` (Microsoft.Testing.Platform). VSTest flags like `-nologo` fail.
- Functional tests start Postgres, Redis and Identity containers. If no container runtime is running, mark them SKIPPED with that reason. Never drop them silently.
- If the change adds behaviour but no test was added or changed, add a row saying so.

## 5. Contract checks

- **Catalog.API endpoints changed**: the build regenerates `src/Catalog.API/Catalog.API.json` and `Catalog.API_v2.json`. They must appear in the diff, or the report says why they didn't change.
- **`.proto`, integration event types or `AddSubscription` calls changed**: grep the event or message name and confirm the publisher and every subscriber agree.
- **EF entities or a `DbContext` changed**: a matching migration exists in that service's `Migrations` folder (`src/Ordering.Infrastructure`, `src/Catalog.API/Infrastructure`, `src/Webhooks.API`).
- **A `Version=` attribute added to a `PackageReference` in a csproj**: FAIL. Versions go in `Directory.Packages.props`.

## 6. UI check (`src/WebApp` or `src/WebAppComponents` changed)

- If a spec in `e2e/` covers the flow (`BrowseItemTest`, `CartTest`, `CheckoutTest`), run `npm run test:e2e -- e2e/<spec>`.
- Otherwise run `aspire start` and drive the changed page with the `playwright-cli` skill (log in as `alice` / `Pass123$`). Confirm the new behaviour, check the browser console for errors, then run `aspire stop`.
- If you can't run the UI, mark the row SKIPPED and give the reason. Don't claim the UI works because it compiled.

## 7. Security (conditional)

If the diff touches `Apis/`, `Identity.API`, auth or JWT setup, the `mobile-bff` YARP config, `MarkupString`, raw SQL, or anything that looks like a secret, run the `security-reviewer` agent and summarise its result in a row.

## 8. Diff self-review

- Read the whole diff once more. Look for debug output, commented-out code, stray TODOs, unrelated files, and a leftover `artifacts-verify` directory.
- Re-read the user's original request and check that each part is addressed. List any part that isn't.

## 9. Report

Use exactly this format:

| Check | Result | Evidence |
|---|---|---|
| Build | PASS | 0 warnings, 0 errors |
| Format | PASS | 3 files clean |
| Ordering.UnitTests | PASS | 142/142 passed |
| Ordering.FunctionalTests | SKIPPED | Docker not running. Start it and re-run |
| UI: My Orders cancel | PASS | cancelled order #12, status shows Cancelled |
| Request coverage | PASS | 3/3 asks addressed |

End with one line: **Verified** or **Not verified: \<what's left\>**. Never round a FAIL or SKIPPED up to "done".

If no row is FAIL, record the stamp so the commit gate lets the changes through:

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File .claude/hooks/verify-gate.ps1 -Stamp
```

Staging files doesn't affect the stamp, but editing any code file after it does, so verify again before committing.
