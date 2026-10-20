# 02: Ordering ownership tested through HTTP, plus a timing baseline

**What to build:** The rule that a Buyer can only see and cancel their own Orders is tested against the real Ordering read query and Postgres, through the Ordering HTTP API. Today the only tests mock both the current user and the ownership check, so a regression in the query's ownership filter (a security bug) would go unnoticed. Also record how long the Ordering functional tests take, so ticket 10 can be measured against it.

**Blocked by:** 01 (Write the testing convention in CLAUDE.md)

**Status:** done

- [x] Before any change, the per-test and total durations of the Ordering functional test project are recorded in a `## Comments` section of this ticket
- [x] The Ordering functional tests have a seed helper that writes a Buyer and an Order for a given owner identity through the Ordering database context, and returns the Order number
- [x] Cancelling an Order owned by another identity returns 404
- [x] Getting an Order owned by another identity returns 404
- [x] A Buyer can cancel their own Order: seed it for the fixed test identity, cancel it, read it back, and its status is Cancelled
- [x] The tests need no RabbitMQ. Cancelling writes the integration event to the outbox, and the failed publish is caught and logged
- [x] The mock-based "cancel not owned" and "get not owned" endpoint unit tests are deleted
- [x] Each new test uses its own fresh identity or Order, so it doesn't depend on test order
- [x] After the change, durations are recorded again in this ticket

## Comments

**Bug found and fixed (production code, shared library).** The owner-cancel test returned 500. The cause was `IntegrationEventLogService`, which looked up integration event types in the entry assembly. Under `WebApplicationFactory`, the entry assembly is the test host, not the service, so reading back any pending outbox entry threw. That made every outbox-writing command untestable through HTTP. Production was unaffected. The fix resolves each entry's stored full type name across loaded assemblies, accepting only `IntegrationEvent` types. Fixing it was agreed with the user during implementation. It unblocks the cancel and ship tests in ticket 06.

**Mutation check.** With the ownership condition removed from the Ordering read query, both 404 tests fail, and both pass once it is restored.

**Timing baseline, Ordering functional tests (run alone, Docker warm, 2026-10-20).** The first test absorbs host warm-up. Container start-up sits outside the per-test numbers.

| | Before (11 tests) | After (14 tests) |
|---|---|---|
| Test run duration | 9.8 s | 11.1 s |
| First test (warm-up) | 5.8 s | 5.8 s |
| Slowest other test | 0.37 s (AddNewOrder) | 0.49 s (GettingAnotherBuyersOrderReturnsNotFound) |
| New tests | – | 0.49 s, 0.31 s, 0.02 s |

Most of the run is fixture and host start-up, not the tests themselves. That is what ticket 10 should target.
