# 03: Functional tests can act as any Buyer or as an Admin

**What to build:** Ordering functional tests can choose, per request, which user is signed in and whether that user has the `admin` role. Without a choice, requests act as today's fixed test user with no role, so existing tests keep working. This lets later tickets test admin endpoints and the Per-Buyer Limit with several different Buyers.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] A test can make a request as a specific user id
- [ ] A test can make a request as an Admin (with the `admin` role claim, using the same claim type the real tokens will carry)
- [ ] With no choice made, requests act as the existing fixed test user, without the role
- [ ] The mechanism exists only in the test project, never in production code
- [ ] All existing Ordering functional tests pass unchanged
- [ ] One new test shows two requests from different users each seeing only their own Orders
