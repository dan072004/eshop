# 03: Basket repository contract

**What to build:** An in-memory basket repository fake that is proved to behave like the Redis repository. Both run the same contract tests, so the fake can't drift from Redis. Ticket 04 then uses the fake to test the Basket service without Docker.

**Blocked by:** 01 (Write the testing convention in CLAUDE.md)

**Status:** ready-for-agent

- [ ] A small, dictionary-backed fake of the basket repository interface lives in the Basket functional test project
- [ ] An abstract xUnit contract class holds the repository behaviour tests (today's Redis repository cases move into it), asserting only through the repository interface
- [ ] One concrete subclass runs the contract against Redis (existing fixture), another against the fake
- [ ] Both subclasses pass
- [ ] Redis coverage is the same as before or better: no current case is lost
