# 04: Basket service tests over the fake

**What to build:** The Basket service's tests check what was stored, not which repository methods were called. They run against the in-memory fake from ticket 03 and stay free of Docker.

**Blocked by:** 03 (Basket repository contract)

**Status:** done

- [x] The Basket unit test project links the fake's source file; it does not reference the functional test project
- [x] Every current Basket service test is rewritten to assert through the service's public methods (e.g. get the basket after update or delete) instead of `Received(...)`
- [x] Covered: get for no user, get for a valid user, get for an unknown user, update for an authenticated user, update rejected for an anonymous user, not-found when the save fails, delete for an authenticated user, and the Order-started event clearing the basket
- [x] The "save fails" case is driven by the fake (e.g. a switch that makes it refuse saves), not by a mock
- [x] NSubstitute is no longer used on our own types in the Basket unit tests

## Comments

- The fake gained a `RefusesSaves` switch (UpdateBasketAsync stores nothing and returns null, as Redis does when the write fails). It also now has explicit usings, because Basket.UnitTests compiles it via a linked `<Compile Include>` and doesn't share the functional project's global usings.
- NSubstitute is removed from Basket.UnitTests entirely (package references and global using), since nothing there needs it any more.
- "Get for an invalid user" became `GetBasketReturnsEmptyForUnknownUserId`; delete and Order-started tests also check another buyer's basket is untouched.
