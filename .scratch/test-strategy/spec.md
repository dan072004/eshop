# Spec: Test strategy — mocks to seams

Status: ready-for-agent

## Problem Statement

About half of the server test suite checks behaviour through public interfaces. The other half substitutes our own classes with NSubstitute and asserts on how they were called. Those tests fail when code is refactored without any change in behaviour. Some of them pass without running the logic they are named after:

- The Ordering endpoint test for listing a Buyer's Orders sets up the query for a different user id from the one the identity mock returns. It passes only because it checks the result type.
- The test for "cancelling another Buyer's Order returns not found" mocks both the current user and the ownership check, so all it covers is an `if`. The ownership rule actually lives in the Ordering read query. If that query regressed, a Buyer could see or cancel another Buyer's Orders, and no test would fail.
- The OrderProcessor test mocks the grace-period repository. The rule that decides which Orders leave the grace period lives in that repository's SQL, and nothing tests it.
- Basket service tests assert that the repository was called, not that the basket was stored or removed.
- Some tests assert only that a value is not null, or that a response returned status 200.

There is also no written testing convention. `CLAUDE.md` says unit tests "use MSTest.Sdk with NSubstitute", and the nearest examples an agent finds are the mock-heavy Ordering tests. The `/tdd` skill says the opposite: mock only at system boundaries. As a result, agents working in parallel produce a mix of both styles. The coupons work has been reverted so that the tests can be fixed first.

## Solution

Choose each test's seam by where the behaviour lives:

| Where the behaviour lives | How it is tested |
|---|---|
| Inside the infrastructure call (a query, a Redis script, serialisation) | Integration test against the real thing, at the HTTP or repository seam |
| Above infrastructure that only stores or transports data | Fast test with an in-memory fake at that seam |
| In a system we don't own (payment, email, clock, event bus) | Fake or mock at that boundary only |
| Nowhere near infrastructure | Plain fast test |

The convention goes in `CLAUDE.md`, so agents and `/code-review` use the same standard. Existing tests are moved onto these seams. The ownership rule and the grace-period rule get real tests, and tests that only check shape are strengthened or deleted.

The work comes in two batches:
- **Batch 1:** the written convention, Ordering ownership tests against real Postgres, and a timing baseline.
- **Batch 2:** everything else.

## User Stories

1. As a developer, I want a written testing convention in `CLAUDE.md`, so that I know which kind of test to write without reverse-engineering existing tests.
2. As a coding agent running `/tdd`, I want `CLAUDE.md` to say "NSubstitute only at external boundaries", so that I don't copy the mock-heavy examples I find nearby.
3. As a coding agent, I want `CLAUDE.md` to name reference tests for each seam, so that I have a good example to copy.
4. As a reviewer using `/code-review`, I want a documented test standard, so that I can flag a mocked internal collaborator as a standards violation.
5. As a Buyer, I want a request to cancel another Buyer's Order to be refused, so that nobody can cancel my Orders.
6. As a Buyer, I want a request to view another Buyer's Order to be refused, so that nobody can read my Orders.
7. As a developer, I want the ownership tests to run against the real Ordering read query and Postgres, so that a regression in the ownership filter fails a test.
8. As a developer, I want a test proving that a Buyer can cancel their own Order and then sees it as Cancelled, so that the "not found" test can't pass just because cancelling is broken.
9. As a developer, I want the mock-based ownership tests deleted when their HTTP replacements land, so that there is one source of truth and no misleading example.
10. As a developer, I want a timing baseline for the Ordering functional tests before and after batch 1, so that the cost of moving tests onto containers is measured, not guessed.
11. As a developer, I want the Basket service tested against an in-memory basket repository, so that the tests check what was stored instead of which method was called.
12. As a developer, I want the in-memory basket repository and the Redis repository to pass the same contract tests, so that the fake can't drift from the real behaviour.
13. As a developer, I want Basket service tests to stay free of Docker, so that the fast feedback loop stays fast.
14. As a developer, I want a test proving that repeating a create-Order request with the same request id creates only one Order, so that de-duplication is checked as behaviour.
15. As a developer, I want the interaction-only de-duplication unit test deleted when that functional test lands, so that no test mocks the mediator.
16. As an operator, I want the grace-period rule tested against Postgres, so that only Submitted Orders older than the grace period move on to stock validation.
17. As a developer, I want the OrderProcessor service tested with a fake grace-period repository and a mocked event bus, so that the only thing mocked is RabbitMQ.
18. As a developer, I want the Ordering endpoint unit tests removed once HTTP tests cover the same behaviour, so that endpoints are tested through HTTP only.
19. As a developer, I want the endpoint test that lists a Buyer's Orders to assert that a seeded Order appears in the list, so that it fails when listing is broken.
20. As a developer, I want the domain test for creating an Order item to assert the item's values, so that it fails when the item is built wrongly.
21. As a developer, I want the command-handler test that only exercises the `Buyer` constructor to be deleted or moved into the domain tests, so that each test sits with the behaviour it covers.
22. As a developer, I want the ClientApp tests that only check shape deleted, so that the suite has no tests that pass whatever the behaviour.
23. As a developer, I want functional tests isolated by unique data per test, so that they can share containers and run in parallel without resetting the database.

## Implementation Decisions

**Convention (batch 1)**
- `CLAUDE.md` gets a testing section containing the "where the behaviour lives" table and the rules below. It replaces the line saying unit tests use NSubstitute.
- **No NSubstitute on our own types.** That covers the mediator, command handlers, and query and repository interfaces. NSubstitute is allowed only for types we don't own: the event bus, HTTP message handlers, framework types such as the web host environment, and the clock.
- **Fakes are allowed for our own repository interfaces whose real adapter only persists data.** A fake is a small working implementation, and tests assert through the public interface, not with `Received(...)`.
- **Behaviour inside an adapter is tested against the real database, with no fake standing in for it.** Two current examples: the ownership filter in the Ordering read query, and the grace-period SQL.
- **Every fake has a contract test shared with the real adapter.**
- **The current user is not a boundary.** Endpoint behaviour is tested only through HTTP in the functional tests, where the user comes from the test authentication middleware. Endpoint static methods are not unit-tested directly.
- **A mock test that has been replaced is deleted in the same change** that adds its replacement.
- Reference examples named in `CLAUDE.md`:
  - Webhooks subscription lifecycle (functional, through HTTP).
  - Redis basket repository (real adapter).
  - The Order aggregate test that checks the total of two identical items with a literal expected value (domain).
  - The webhook client test that fakes only the HTTP handler (boundary).
- The convention is not recorded as an ADR, because it is cheap to reverse.

**Ordering ownership (batch 1)**
- The Ordering functional tests get a seed helper. It writes a Buyer and an Order for a given owner identity directly through the Ordering database context, and returns the Order number.
- **Cancelling an Order owned by another identity returns 404.**
- **Viewing an Order owned by another identity returns 404.** This keeps the current behaviour of answering "not found" rather than "forbidden".
- **A Buyer can cancel their own Order.** The test seeds an Order for the fixed test identity, cancels it, then reads it back and expects the status to be Cancelled.
- **These tests need no RabbitMQ.** Cancelling writes an integration event to the outbox. Publishing it then fails because there is no connection, and the integration event service catches that and marks the event Failed. The Order is still Cancelled, and the log shows a publish error. (Found during ticket 02: before that point, the outbox looked up event types in the entry assembly, which is the test host under `WebApplicationFactory`. So any command that writes to the outbox returned 500 in functional tests. Ticket 02 fixed the lookup.)
- The mock-based tests for "cancel not owned" and "get not owned" are deleted.

**Baseline (batch 1)**
- Record the per-test and total durations of the Ordering functional test project before and after batch 1, in the ticket.

**Basket (batch 2)**
- An in-memory, dictionary-backed fake of the basket repository and an abstract xUnit contract class both live in the Basket functional test project. That project has two concrete subclasses: one for Redis and one for the fake.
- The Basket unit test project (MSTest) links the fake's source file and does not reference the functional project. Service tests then cover these through the service's public methods:
  - Getting, updating and deleting the basket, observed with a get afterwards.
  - The authentication check.
  - Not-found when a save fails.

**Ordering application tests (batch 2)**
- De-duplication gets a functional test: POST the same Order twice with one `x-requestid`, then assert the Buyer has exactly one new Order. The interaction-only de-duplication unit test is deleted.
- The endpoint unit-test class is removed once every case it covers has an HTTP equivalent or is no longer needed.
- The command-handler test that only checks for a missing Buyer id is deleted, or moved to the domain tests as a `Buyer` constructor test if none exists there.

**OrderProcessor (batch 2)**
- The grace-period repository gets an integration test against Postgres. It needs:
  - an Order just past the grace period;
  - an Order just inside it;
  - an old Order in a status other than Submitted.
- The OrderProcessor service test switches from NSubstitute on the repository to a fake. The event bus stays mocked.

**Weak assertions (batch 2)**
- The domain test that creates an Order item asserts the item's units, unit price, discount and product. (The coupons test that previously covered this was reverted.)
- The functional test that gets all stored Orders seeds an Order and asserts that it is in the list.
- The ClientApp shape-only tests are deleted, with no replacements until ClientApp work is scheduled.

**Speed (batch 2)**
- Functional fixtures may move to shared xUnit collection or assembly fixtures.
- Tests are isolated by unique data, such as fresh owner identities, never by resetting the database.
- Check whether the Ordering fixture needs to start Identity API and its database at all. It only reads Identity's URL, and authentication is bypassed in the tests.

## Testing Decisions

- **A good test asserts observable behaviour through a public seam.** Examples are an HTTP response, a value read back through the repository interface, or an event published on the event bus boundary. It never asserts on calls to our own collaborators.
- **Modules under test, and their seams:**
  - Ordering HTTP API, using the existing functional fixture with real Postgres: ownership, cancelling, de-duplication, listing.
  - Basket service, through its public methods over the fake.
  - Basket repository, through the contract against Redis and against the fake.
  - OrderProcessor grace-period repository, against real Postgres.
  - OrderProcessor service, over the fake repository and the mocked event bus.
  - Ordering domain aggregates, with no infrastructure.
- **Prior art:**
  - Webhooks subscription lifecycle functional test: HTTP, scoped to the user.
  - Redis basket repository tests: a real adapter in a container.
  - Ordering functional tests that seed through the database context and then assert over HTTP.
  - Webhook tests that fake only the HTTP handler.
  - Order aggregate tests with literal expected totals.
- **No new seams are introduced.** Every seam above already exists.

## Out of Scope

- Coupons. That work is reverted and on hold until this spec is done.
- Letting functional tests act as any user or as an Admin. That is coupons ticket 03. The ownership tests here seed an Order for another identity instead, so they don't depend on it.
- Replacing the ClientApp tests with behaviour tests.
- Starting RabbitMQ in functional fixtures.
- Changing the "not found rather than forbidden" response for another Buyer's Order.
- Catalog and Webhooks tests, which already follow the convention.
- Production code changes, apart from any a new test exposes as a real bug. If that happens, it is reported in the ticket rather than silently fixed.

## Further Notes

- The decisions come from a grilling session on 2026-10-20, recorded in `docs/refactoring/test-strategy-mocks-to-seams-2026-10-20.md`.
- When coupons resumes, ticket 03 can replace the ownership tests' seed-for-another-identity approach with acting as that user. The tests' assertions don't change.
- The reverted coupons ticket 01 also took away the domain and functional discount tests. Bring them back when ticket 01 is reimplemented, written to this convention.
