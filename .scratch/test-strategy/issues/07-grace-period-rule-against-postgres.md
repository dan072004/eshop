# 07: Grace-period rule tested against Postgres

**What to build:** The rule that only Submitted Orders older than the grace period move on to stock validation is tested against a real Postgres database with the real Ordering schema. The OrderProcessor service test uses a fake repository, so the only thing it mocks is the event bus.

**Blocked by:** 01 (Write the testing convention in CLAUDE.md)

**Status:** ready-for-agent

- [ ] A new OrderProcessor functional test project (xUnit v3, like the other functional projects) starts Postgres and applies the Ordering database migrations. The SQL is then checked against the real schema, not a hand-written table
- [ ] The project is added to the CI solution filter, and OrderProcessor exposes its internals to it
- [ ] The grace-period repository returns a Submitted Order just past the grace period
- [ ] It does not return a Submitted Order just inside the grace period
- [ ] It does not return an old Order in a status other than Submitted
- [ ] The OrderProcessor service test uses a fake grace-period repository instead of NSubstitute. The event bus stays mocked, and the test still asserts a confirmation is published for each eligible Order
