# 08: Clean up Ordering domain and command-handler tests

**What to build:** The remaining Ordering unit tests fail when the behaviour they're named after breaks, and each one sits with the behaviour it covers.

**Blocked by:** 01 (Write the testing convention in CLAUDE.md)

**Status:** done

- [x] The create-Order-item domain test asserts the item's units, unit price, discount and product, not just non-null
- [x] The "throws when no buyer id" command-handler test becomes a `Buyer` domain test if no equivalent exists there; otherwise it is deleted
- [x] The "returns false if the Order is not persisted" create-Order handler test is rewritten over a fake Order repository whose save can be made to fail. It no longer uses NSubstitute on our own types. The mediator is no longer mocked: either the handler test needs no mediator or the dependency is a fake
- [x] No NSubstitute on our own types remains in the Ordering unit tests (apart from anything ticket 05 or 06 deletes)

## Comments

- "Throws when no buyer id" was deleted: `BuyerAggregateTest.Create_buyer_item_fail` already covers it.
- Review follow-up: the "returns false if the Order is not persisted" test (`NewOrderCommandHandlerTest`) and its fakes (`Application/Fakes.cs`) were deleted. The test asserted the handler passes through `UnitOfWork.SaveEntitiesAsync` returning `false`, but the real `OrderingContext.SaveEntitiesAsync` never returns false (it returns true or throws), so the fake invented a state the real adapter can't produce. Success is covered end-to-end by ticket 05's HTTP test; the real "save throws" path belongs to ticket 11.
- NSubstitute (package references and `global using`) is removed from `Ordering.UnitTests`; nothing there uses it any more.
