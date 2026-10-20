# 07: Grace-period rule tested against Postgres

**What to build:** The rule that only Submitted Orders older than the grace period move on to stock validation is tested against a real Postgres database with the real Ordering schema. The OrderProcessor service test uses a fake repository, so the only thing it mocks is the event bus.

**Blocked by:** 01 (Write the testing convention in CLAUDE.md)

**Status:** done

- [x] A new OrderProcessor functional test project (xUnit v3, like the other functional projects) starts Postgres and applies the Ordering database migrations. The SQL is then checked against the real schema, not a hand-written table
- [x] The project is added to the CI solution filter, and OrderProcessor exposes its internals to it
- [x] The grace-period repository returns a Submitted Order just past the grace period
- [x] It does not return a Submitted Order just inside the grace period
- [x] It does not return an old Order in a status other than Submitted
- [x] The OrderProcessor service test uses a fake grace-period repository instead of NSubstitute. The event bus stays mocked, and the test still asserts a confirmation is published for each eligible Order

## Comments

- The repository tests passed on first run (they characterise existing SQL). Mutation-checked: flipping the age comparison fails the two age tests; dropping the `'Submitted'` filter fails the status test.
- The service test's fake holds Submitted Orders with their age and applies only the age rule; the status rule lives in the SQL and is covered against Postgres. The fake has no shared contract test with the real adapter (the Postgres tests seed through `OrderingContext`, which the fake has no equivalent of).
- The new project adds two `NU1902` (MessagePack, via Aspire.Hosting) build warnings, the same pre-existing warning every other Aspire-hosted functional test project already emits; they are not escalated to errors.
- The new project is also added to `eShop.slnx`.
