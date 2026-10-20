# 08: Clean up Ordering domain and command-handler tests

**What to build:** The remaining Ordering unit tests fail when the behaviour they're named after breaks, and each one sits with the behaviour it covers.

**Blocked by:** 01 (Write the testing convention in CLAUDE.md)

**Status:** ready-for-agent

- [ ] The create-Order-item domain test asserts the item's units, unit price, discount and product, not just non-null
- [ ] The "throws when no buyer id" command-handler test becomes a `Buyer` domain test if no equivalent exists there; otherwise it is deleted
- [ ] The "returns false if the Order is not persisted" create-Order handler test is rewritten over a fake Order repository whose save can be made to fail. It no longer uses NSubstitute on our own types. The mediator is no longer mocked: either the handler test needs no mediator or the dependency is a fake
- [ ] No NSubstitute on our own types remains in the Ordering unit tests (apart from anything ticket 05 or 06 deletes)
