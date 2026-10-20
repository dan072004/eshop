# 10: Faster Ordering functional fixtures

**What to build:** Moving tests onto the Ordering HTTP seam doesn't slow the suite down noticeably. Containers are shared across test classes, tests are isolated by their own data, and the fixture starts only the containers Ordering actually needs. The improvement is measured against ticket 02's baseline.

**Blocked by:** 02 (Ordering ownership tested through HTTP), 05 (Order de-duplication tested through HTTP), 06 (Retire the Ordering endpoint unit tests)

**Status:** ready-for-agent

- [ ] Find out whether Ordering starts and passes its tests without Identity API and its database in the fixture (the fixture only reads Identity's URL, and authentication is bypassed). If so, remove them
- [ ] The Ordering fixture is shared across test classes (xUnit v3 collection or assembly fixture) if there is more than one class
- [ ] No test relies on a database reset or on another test's data. Each test seeds its own Buyer or Order
- [ ] Per-test and total durations are recorded in this ticket and compared with ticket 02's baseline
- [ ] All Ordering functional tests pass
