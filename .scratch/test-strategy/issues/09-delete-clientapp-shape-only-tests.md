# 09: Delete the ClientApp shape-only tests

**What to build:** The ClientApp test suite has no tests that pass whatever the app does. Tests that only check a property is not null or empty after construction are deleted, along with the catalog service tests that only exercise the built-in fake data service. No replacements are written until ClientApp work is scheduled.

**Blocked by:** 01 (Write the testing convention in CLAUDE.md)

**Status:** done

- [x] The ViewModel tests that only assert a command or property is not null or empty on construction are deleted
- [x] The catalog service tests that only test the fake data service are deleted
- [x] Tests that assert real behaviour (state changes after an action) are kept
- [x] The ClientApp test project still builds and its remaining tests pass, through its own CI workflow (it is outside the web solution filter)

## Comments

Deleted (16):
- `Services/CatalogServiceTests` (3 tests): only assert the built-in `CatalogMockService` returns non-empty lists.
- `Services/BasketServiceTests.GetFakeBasketTest`: despite its name it only checks `CatalogMockService` is non-empty.
- `Services/OrdersServiceTests` (2 tests): same pattern against the fake `OrderMockService`; deleted with the catalog ones because they test only fake data, not app behaviour.
- `MainViewModelTests` (2 tests): command not null / `IsBusy` false on construction.
- `CatalogItemViewModelTests.AddCatalogItemCommandIsNotNullTest`.
- `CatalogViewModelTests`: the two command-not-null tests, the six construction-state tests (empty Products/Brands/Types, null SelectedBrand/SelectedType, IsFiltering false), and the three `...IsNotNullAfterViewModelInitializationTest` tests (the collections are never null, so these pass whatever `InitializeAsync` does).
- `OrderViewModelTests.OrderPropertyIsNullWhenViewModelInstantiatedTest`.

Kept (11):
- `CatalogItemViewModelTests.AddCatalogItemCommandSendsAddProductMessageTest`: executing the command publishes a message.
- `CatalogViewModelTests.SettingBadgeCountPropertyShouldRaisePropertyChanged`: initialisation updates the badge count.
- `CatalogViewModelTests.ClearFilterCommandResetsPropertiesTest`: borderline, kept because it acts and asserts state; it is weak (no filter is set before clearing), worth strengthening when ClientApp work is scheduled.
- `OrderViewModelTests.OrderPropertyIsNotNullAfterViewModelInitializationTest`: borderline, kept because `Order` starts null and only `InitializeAsync` loads it, so it fails if loading breaks.
- `OrderViewModelTests.SettingOrderPropertyShouldRaisePropertyChanged`.
- `MockViewModelTests` (6 tests): exercise the app's `ValidatableObject` validation rules and change notification.

The three edited test files also got `dotnet format whitespace` fixes. `dotnet test --project tests/ClientApp.UnitTests`: 11/11 passed.
