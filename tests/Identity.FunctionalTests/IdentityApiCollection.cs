namespace eShop.Identity.FunctionalTests;

/// <summary>
/// One Identity.API and Postgres for every test class. A second fixture's in-process Aspire host fails to start
/// (DcpDependencyCheck times out) while the first one starts.
/// </summary>
[CollectionDefinition(Name)]
public sealed class IdentityApiCollection : ICollectionFixture<IdentityApiFixture>
{
    public const string Name = "Identity.API";
}
