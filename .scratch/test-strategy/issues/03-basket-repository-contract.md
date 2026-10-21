# 03: Basket repository contract

**What to build:** An in-memory basket repository fake that is proved to behave like the Redis repository. Both run the same contract tests, so the fake can't drift from Redis. Ticket 04 then uses the fake to test the Basket service without Docker.

**Blocked by:** 01 (Write the testing convention in CLAUDE.md)

**Status:** done

- [x] A small, dictionary-backed fake of the basket repository interface lives in the Basket functional test project
- [x] An abstract xUnit contract class holds the repository behaviour tests (today's Redis repository cases move into it), asserting only through the repository interface
- [x] One concrete subclass runs the contract against Redis (existing fixture), another against the fake
- [x] Both subclasses pass
- [x] Redis coverage is the same as before or better: no current case is lost

## Comments

- The fake stores JSON snapshots (using Basket.API's `BasketSerializationContext`), not object references. A new contract case, `StoredBasketIsASnapshotNotTheCallersObject`, failed against a reference-storing fake and passes against Redis, so it pins this behaviour.
- Added a second new case, `UpdateReplacesTheBuyersBasketAndLeavesOtherBuyersAlone`. It passed on first run for both, since it characterises existing behaviour.
- Redis now runs 4 cases (was 2). Both existing cases moved into `BasketRepositoryContract` unchanged; `RedisBasketRepositoryTests` keeps its name because CLAUDE.md cites it.
