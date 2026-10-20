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
- Review follow-up: the service test's repository is now a canned-answer stub (`StubGracePeriodOrdersRepository`): it returns the Order ids the test configures and records the grace period it was asked for, with no age rule of its own. Both the age and status rules live in the SQL and are covered against Postgres, so the stub needs no contract test. The test asserts one `GracePeriodConfirmedIntegrationEvent` per returned id with the matching OrderId, nothing else published (`IEventBus` stays NSubstitute, a boundary), and that the repository was asked for `TimeSpan.FromMinutes(1)` when `GracePeriodTime = 1`. Mutation-checked: switching the service's `FromMinutes` to `FromSeconds` fails the test.
- The new project adds two `NU1902` (MessagePack, via Aspire.Hosting) build warnings, the same pre-existing warning every other Aspire-hosted functional test project already emits; they are not escalated to errors.
- The new project is also added to `eShop.slnx`.
