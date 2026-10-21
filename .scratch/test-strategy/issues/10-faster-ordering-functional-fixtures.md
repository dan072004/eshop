# 10: Faster Ordering functional fixtures

**What to build:** Moving tests onto the Ordering HTTP seam doesn't slow the suite down noticeably. Containers are shared across test classes, tests are isolated by their own data, and the fixture starts only the containers Ordering actually needs. The improvement is measured against ticket 02's baseline.

**Blocked by:** 02 (Ordering ownership tested through HTTP), 05 (Order de-duplication tested through HTTP), 06 (Retire the Ordering endpoint unit tests)

**Status:** done

- [x] Find out whether Ordering starts and passes its tests without Identity API and its database in the fixture (the fixture only reads Identity's URL, and authentication is bypassed). If so, remove them
- [x] The Ordering fixture is shared across test classes (xUnit v3 collection or assembly fixture) if there is more than one class
- [x] No test relies on a database reset or on another test's data. Each test seeds its own Buyer or Order
- [x] Per-test and total durations are recorded in this ticket and compared with ticket 02's baseline
- [x] All Ordering functional tests pass

## Comments

**Identity removed from the fixture.** Ordering.API only reads `Identity:Url` as a string (JWT bearer authority, OpenAPI security scheme). `AutoAuthorizeMiddleware` signs every request in, so no token is ever validated and the URL is never called. The fixture now passes a placeholder (`http://identity-api.invalid`) and starts only the OrderingDB Postgres container. The Identity API project resource, the IdentityDB container and the test project's reference to `Identity.API` are gone, so building the tests no longer compiles Identity.API either. No production change was needed.

**Sharing.** There is one test class (`OrderingApiTests`), so its `IClassFixture` already shares one fixture across every test. No collection or assembly fixture is needed until a second class appears.

**Isolation.** Nothing resets the database. Each test that needs data seeds its own Order (and Buyer if missing), uses a fresh `other-{Guid}` identity or a fresh `x-requestid`, and finds its data by the returned Order number. The de-duplication test compares the Buyer's Order count before and after its own two posts, which holds because tests in one class run sequentially.

**Review follow-up.** `PostingTheSameOrderTwiceWithOneRequestIdCreatesOneOrder` counts the shared test identity's Orders, so it relies on tests within the class running sequentially. Giving it a fresh identity needs functional tests to be able to act as another user (coupons ticket 03).

**Timings (run alone, Docker warm, 2026-10-20).** Measured with the ticket 02 method (xUnit TRX). Another agent may have been running Basket tests at the same time, so each side was run at least twice and the better run is reported. "Wall" is the whole `dotnet test --no-build` command, including container start-up.

| | Ticket 02 baseline | Before (best of 4) | After (best of 2) |
|---|---|---|---|
| Tests | 14 | 17 | 17 |
| Test run duration | 11.1 s | 10.0 s | 9.0 s |
| Wall clock | – | 11.0 s | 10.0 s |
| First test (host + DB warm-up) | 5.8 s | 5.3 s (CancelWithEmptyGuidFails) | 4.9 s |
| OwnerCanCancelTheirOrder | – | 0.83 s | 0.72 s |
| Slowest other test (excl. the two above) | 0.49 s | 0.14 s (PostingTheSameOrderTwice…) | 0.14 s |
| Sum of the other 16 tests | – | 1.4 s | 1.2 s |

Every remaining test other than the first two takes under 0.15 s. The first test still absorbs host start-up, EF migrations and waiting for Postgres to accept connections.
