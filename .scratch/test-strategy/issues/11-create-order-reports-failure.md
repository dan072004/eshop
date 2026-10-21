# 11: Creating an Order reports failure when it fails

**What to build:** A Buyer whose create-Order request fails (for example, it doesn't pass validation) gets a failure response from the Ordering API, not 200 OK. Today `POST api/orders` answers 200 whatever happens. The identified-command handler catches the failure and returns `false`, and the endpoint returns OK regardless. Found during ticket 05.

**Blocked by:** None (can start immediately)

**Status:** needs-triage

- [ ] An invalid create-Order request (e.g. a card security number that fails validation) gets a non-2xx response through HTTP
- [ ] A valid create-Order request still gets 200 and creates exactly one Order
- [ ] A repeated request with the same `x-requestid` still creates no second Order, and its response is decided deliberately (the ticket 05 de-duplication test keeps passing)
- [ ] The existing add-new-Order functional test sends a valid request and asserts that an Order was created, so it no longer passes only because of this bug
- [ ] Decide whether other commands sent through the identified-command handler (cancel, ship) have the same problem, and record the answer here

## Comments

This is a production behaviour change, outside the test-strategy spec, which only allows agreed fixes. Triage before implementing. The status code to use, and what callers (WebApp checkout) do with it, are product decisions.
