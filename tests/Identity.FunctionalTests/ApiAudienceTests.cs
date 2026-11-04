using Duende.IdentityServer.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace eShop.Identity.FunctionalTests;

/// <summary>
/// Each API accepts only tokens for its own audience (ServiceDefaults). Duende builds a token's <c>aud</c> from the API
/// resources its scopes belong to, so each API's scope must belong to that API alone.
/// </summary>
[Collection(IdentityApiCollection.Name)]
public sealed class ApiAudienceTests(IdentityApiFixture fixture)
{
    [Theory]
    [InlineData("orders", "orders")]
    [InlineData("basket", "basket")]
    [InlineData("webhooks", "webhooks")]
    [InlineData("eshop-mcp", IdentityApiFixture.EshopMcpUrl)]
    public async Task AScopeNamesItsOwnApiAsTheAudience(string scope, string audience)
    {
        var resources = await fixture.Services.GetRequiredService<IResourceStore>().FindApiResourcesByScopeNameAsync([scope]);

        Assert.Equal([audience], resources.Select(r => r.Name));
    }
}
