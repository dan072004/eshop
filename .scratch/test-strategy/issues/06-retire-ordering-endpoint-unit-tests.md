# 06: Retire the Ordering endpoint unit tests

**What to build:** Ordering endpoint behaviour is tested only through HTTP. Every remaining case in the endpoint unit-test class either gets a functional equivalent or is shown to be covered already. Then the class, and its mocks of the current user, mediator and queries, is deleted.

**Blocked by:** 02 (Ordering ownership tested through HTTP), for the seed helper

**Status:** ready-for-agent

- [ ] Cancelling an Order with a request id succeeds through HTTP (may already be covered by 02's owner-cancel test; if so, note it)
- [ ] Shipping a Paid Order succeeds through HTTP, and reading it back shows it as Shipped. The seed helper can produce an Order in the needed status
- [ ] Getting one of the Buyer's Orders through HTTP returns that Order's details
- [ ] The existing get-all-stored-Orders functional test seeds an Order and asserts it appears in the list, replacing the status-only check and the unused variable
- [ ] Bad-request, non-existent-Order and card-type cases are confirmed covered by existing functional tests
- [ ] The endpoint unit-test class is deleted
