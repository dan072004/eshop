# 03: Grace-period tests don't depend on the test machine's clock

**What to build:** The grace-period repository tests pass however far the test machine's clock drifts from the Postgres container's clock. The rule compares against the database clock (`CURRENT_TIMESTAMP - "OrderDate" >= @GracePeriodTime` in `src/OrderProcessor/Services/GracePeriodOrdersRepository.cs:27`). The tests, though, seed `OrderDate` from the test process's clock (`DateTime.UtcNow - placedAgo`, `tests/OrderProcessor.FunctionalTests/GracePeriodOrdersRepositoryTests.cs:53`) with only 5 seconds of margin either side of the boundary. Docker Desktop/WSL2 clocks commonly drift by more than that after the machine sleeps, which would make "just past" or "just inside" fail for reasons unrelated to the code.

**Blocked by:** None (can start immediately)

**Status:** needs-triage

- [ ] Seeded Orders get their `OrderDate` from the database's clock (e.g. set relative to `now()` in SQL after the insert), not from `DateTime.UtcNow`
- [ ] The three cases keep their meaning: a Submitted Order just past the grace period is returned, one just inside it is not, and an old Order that is no longer Submitted is not
- [ ] The boundary margins stay tight (seconds, not minutes), so the tests still fail if the comparison is off by the grace period
- [ ] The production SQL is unchanged

## Comments

Raised in the test-strategy branch review, 2026-10-21. Widening the margins to about 30 seconds would be the cheaper fix, but it only lowers the odds of drift breaking the tests; using the database clock removes the dependence.
