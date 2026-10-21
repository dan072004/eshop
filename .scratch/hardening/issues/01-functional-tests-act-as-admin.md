# 01: Ordering functional tests can act as an Admin

**What to build:** An Ordering functional test can choose, per request, to be signed in as an Admin: the fixed test user plus the `admin` role claim. With no choice made, requests act as today's fixed test user with no role, so existing tests keep working. Ticket 02 needs this to test that only an Admin can ship an Order.

**Blocked by:** None (can start immediately)

**Status:** needs-triage

- [ ] A test can make a request as an Admin, with the `admin` role in the same claim type that ticket 02 makes real tokens carry
- [ ] With no choice made, requests act as the existing fixed test user (`AutoAuthorizeMiddleware.IDENTITY_ID`), without the role
- [ ] The mechanism exists only in `tests/Ordering.FunctionalTests`, never in production code
- [ ] All existing Ordering functional tests pass unchanged

## Comments

Overlaps coupons ticket 03 (on the `coupons` branch), which also lets a test act as any Buyer. If coupons resumes after this lands, ticket 03 only needs to add the "any Buyer" part.
