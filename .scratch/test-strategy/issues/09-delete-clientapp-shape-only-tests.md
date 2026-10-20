# 09: Delete the ClientApp shape-only tests

**What to build:** The ClientApp test suite has no tests that pass whatever the app does. Tests that only check a property is not null or empty after construction are deleted, along with the catalog service tests that only exercise the built-in fake data service. No replacements are written until ClientApp work is scheduled.

**Blocked by:** 01 (Write the testing convention in CLAUDE.md)

**Status:** ready-for-agent

- [ ] The ViewModel tests that only assert a command or property is not null or empty on construction are deleted
- [ ] The catalog service tests that only test the fake data service are deleted
- [ ] Tests that assert real behaviour (state changes after an action) are kept
- [ ] The ClientApp test project still builds and its remaining tests pass, through its own CI workflow (it is outside the web solution filter)
