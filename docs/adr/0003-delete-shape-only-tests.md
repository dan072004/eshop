# Shape-only tests are deleted, not kept for coverage

A shape-only test asserts that a result exists, is not empty, has a type or returned 200, but never that its values are right, so it still passes when the code it is named after is broken. We delete such tests, or rewrite them to assert values from an independent source of truth, rather than keep them for coverage or test counts. They give no protection against regressions, they make the suite look safer than it is, and one with the right name can hide a bug: the old ClientApp `ClearFilterCommandResetsPropertiesTest` cleared a filter that was never set, so it passed while `ClearFilterAsync` left the selected brand and type in place.

## Considered Options

- **Keep them as cheap smoke tests.** Rejected: they cost little to run but still cost reading and maintenance, and they inflate coverage without checking anything.
- **Rewrite every one before deleting it.** Rejected where no work is scheduled in that area: ClientApp lost 16 such tests with no replacements (ticket 09), and behaviour tests get written when ClientApp work starts.

## Consequences

- The check before keeping a test: break the code it is named after (or let a mutation tool do it). If the test still passes, it goes.
- A test that resets or clears state must first set that state up and assert it took effect, otherwise its assertions are true before the action runs.

## Prior art

- James Carr, "TDD Anti-Patterns" (2006): "The Liar", a test that passes without checking the behaviour its name claims.
- Kent Beck, "Test Desiderata" (2019): a good test is behavioural, sensitive to changes in behaviour.
- Vladimir Khorikov, *Unit Testing: Principles, Practices, and Patterns* (2020): tests are judged on protection against regressions, resistance to refactoring, fast feedback and maintainability, and low-value tests are deleted rather than kept.
- Arie van Deursen et al., "Refactoring Test Code" (2001), and later test-smell catalogues such as tsDetect (Peruma et al., 2020): related smells such as Empty Test, Unknown Test and Redundant Assertion.
- Brian Marick, "How to Misuse Code Coverage" (1999): coverage measures what ran, not what was checked.
- Richard DeMillo, Richard Lipton and Frederick Sayward, "Hints on Test Data Selection" (1978): mutation testing, the automated form of the "break the code" check (Stryker.NET for C#).
- The `tdd` skill in mattpocock-skills: tests that check "the _shape_ of things rather than user-facing behavior", and tautological tests.
