# Fakes with shared contract tests instead of mocks of our own types

Tests replace our own ports (repository and query interfaces, the mediator, handlers) with a small working fake or a canned-answer stub, never with NSubstitute. NSubstitute is kept for boundaries we don't own, such as the event bus, outbound HTTP, the clock and framework types. Every fake that implements logic runs the same abstract contract test as its real adapter (for example `BasketRepositoryContract`, with a Redis subclass and an in-memory subclass), so the fake can't drift from the behaviour production has. The mock-based tests we replaced asserted on calls to our own collaborators: they broke on refactors that didn't change behaviour, and some passed without running the logic they were named after.

## Considered Options

- **Keep NSubstitute for our own types.** Rejected: interaction assertions couple tests to the implementation, and a mocked query hides the behaviour that lives in its SQL (ownership, grace period).
- **Test everything against real infrastructure.** Rejected for service-level tests: containers make the feedback loop slow, and Basket service tests stay Docker-free because of the fake.
- **Fakes without contract tests.** Rejected: nothing would stop a fake from keeping behaviour that the real adapter has dropped.

## Consequences

- Changing a port's observable behaviour touches three places: the contract, the real adapter and the fake. Internal adapter changes touch none of them, because the contract run against the real adapter catches regressions.
- Behaviour only the real adapter has (e.g. a Redis TTL) is tested in that adapter's subclass, not in the contract, unless callers start relying on it.
- Behaviour that lives inside an adapter's query is tested against the real database, with no fake standing in for it.

## Prior art

- J.B. Rainsberger, "Integrated Tests Are a Scam" (2009–2010): pairing collaboration tests (against a double) with contract tests (every implementation honours the interface). *JUnit Recipes* (2004) describes the abstract-test-case mechanics.
- Titus Winters, Tom Manshreck and Hyrum Wright, *Software Engineering at Google* (2020), chapter 13 "Test Doubles": prefer fakes, and test a fake against the real implementation to keep its fidelity.
- James Shore, "Testing Without Mocks" (2018, updated 2023): Nullables and narrow integration tests of our own infrastructure wrappers.
- Steve Freeman and Nat Pryce, *Growing Object-Oriented Software, Guided by Tests* (2009): "Don't mock types you don't own".
- Martin Fowler, "ContractTest" (bliki, 2011): the related service-level idea of checking test doubles against the real service.
