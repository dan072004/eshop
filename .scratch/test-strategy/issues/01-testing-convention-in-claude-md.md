# 01: Write the testing convention in CLAUDE.md

**What to build:** `CLAUDE.md` has a testing section, so agents and `/code-review` work to one standard instead of copying whatever tests are nearby. It replaces the line saying unit tests "use MSTest.Sdk with NSubstitute". See `.scratch/test-strategy/spec.md` (Implementation Decisions → Convention).

**Blocked by:** None (can start immediately)

**Status:** done

- [x] The "where the behaviour lives" table is in `CLAUDE.md`: real infrastructure / in-memory fake at the seam / fake or mock at a boundary we don't own / plain test
- [x] Rule: no NSubstitute on our own types (mediator, handlers, query and repository interfaces); allowed only for types we don't own (event bus, HTTP message handlers, framework types, clock)
- [x] Rule: fakes are allowed for our repository interfaces whose real adapter only persists data; tests assert through the public interface, never with `Received(...)`
- [x] Rule: behaviour that lives inside an adapter (e.g. the Ordering ownership query, the grace-period SQL) is tested against the real database
- [x] Rule: every fake has a contract test shared with the real adapter
- [x] Rule: endpoint behaviour is tested through HTTP in the functional tests only; the current user is not a boundary
- [x] Rule: a mock test is deleted in the same change that adds its replacement
- [x] Reference examples are named: the Webhooks subscription lifecycle functional test, the Redis basket repository tests, the Order aggregate test that checks the total of two identical items, and the webhook test that fakes only the HTTP handler
- [x] The existing note that unit tests use MSTest and functional tests use xUnit v3 is kept
