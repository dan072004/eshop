# 05: Order de-duplication tested through HTTP

**What to build:** Sending the same create-Order request twice with the same `x-requestid` creates only one Order, proved through the Ordering HTTP API against real Postgres. The de-duplication unit tests mock the mediator and assert nothing about the result, so they are deleted.

**Blocked by:** 02 (Ordering ownership tested through HTTP). It doesn't depend on 02's behaviour, but both edit the Ordering functional tests, and running them one after the other avoids conflicts.

**Status:** ready-for-agent

- [ ] A functional test posts the same Order twice with one `x-requestid` and asserts that exactly one new Order exists for the Buyer
- [ ] The test can't pass by accident: it checks the Order count before and after, or uses a fresh identity
- [ ] Both identified-command-handler unit tests are deleted. The "sends the command when new" case is already covered by the existing add-new-Order functional test
