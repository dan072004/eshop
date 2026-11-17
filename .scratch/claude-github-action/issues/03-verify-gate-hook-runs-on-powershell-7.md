# 03: The verify-gate hook runs on PowerShell 7

**What to build:** The verify-gate hook starts on a Linux runner as well as on Windows, so the Action is held to the same commit gate as the developer. Today the hook command starts Windows PowerShell, which Linux runners don't have; when the hook can't start, its fallback denies every shell call. The command changes to `pwsh`. Nothing else about the hook changes.

See the spec's "The verify-gate hook runs on `pwsh`".

**Blocked by:** None (can start immediately)

**Status:** done

- [ ] On the developer's Windows machine, a `git commit` with unverified code changes is still denied
- [ ] After a passing verify, the same commit is allowed
- [ ] Shell commands other than `git commit` still run with no change in behaviour
