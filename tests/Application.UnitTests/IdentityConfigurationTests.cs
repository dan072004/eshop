using eShop.Identity.API.Configuration;
using eShop.Identity.API.Services;
using Microsoft.Extensions.Configuration;

namespace eShop.Application.UnitTests;

[TestClass]
public class IdentityConfigurationTests
{
    // MCP clients name eShop.Mcp by its URL as the resource indicator, so its resource carries that name and lists the
    // eshop-mcp scope; no resource is named eshop-mcp any more (ADR 0005).
    [TestMethod]
    public void EshopMcpsResourceIsNamedByItsConfiguredUrlAndOtherResourcesKeepTheirNames()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["EshopMcpUrl"] = "http://localhost:5229/mcp" })
            .Build();

        var resources = Config.GetApis(configuration).ToDictionary(resource => resource.Name);
        var scopes = Config.GetApiScopes().Select(scope => scope.Name).ToArray();

        CollectionAssert.AreEquivalent(new[] { "basket", "http://localhost:5229/mcp", "orders", "webhooks" }, resources.Keys);
        CollectionAssert.AreEqual(new[] { "eshop-mcp" }, resources["http://localhost:5229/mcp"].Scopes.ToArray());
        CollectionAssert.AreEqual(new[] { "orders" }, resources["orders"].Scopes.ToArray());
        CollectionAssert.AreEquivalent(new[] { "basket", "eshop-mcp", "orders", "webhooks" }, scopes);
    }

    [TestMethod]
    public void ClientsUseConfiguredCallbackUrlsAndExpectedScopes()
    {
        var values = new Dictionary<string, string?>
        {
            ["MauiCallback"] = "maui://callback",
            ["WebAppClient"] = "https://webapp.test",
            ["WebhooksWebClient"] = "https://webhooks-client.test",
            ["BasketApiClient"] = "https://basket.test",
            ["OrderingApiClient"] = "https://ordering.test",
            ["WebhooksApiClient"] = "https://webhooks.test",
            ["ClaudeCodeCallbackPort"] = "33419"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        var clients = Config.GetClients(configuration).ToDictionary(client => client.ClientId);

        CollectionAssert.Contains(clients["webapp"].RedirectUris.ToList(), "https://webapp.test/signin-oidc");
        CollectionAssert.Contains(clients["webhooksclient"].AllowedScopes.ToList(), "webhooks");
        CollectionAssert.Contains(clients["maui"].AllowedScopes.ToList(), "basket");
        CollectionAssert.Contains(clients["maui"].AllowedScopes.ToList(), "orders");
        // With a resource indicator "orders" would be dropped from an MCP client's token, so they can't ask for it.
        foreach (var mcpClient in new[] { "claude-code", "vscode" })
        {
            CollectionAssert.AreEquivalent(
                new[] { "openid", "profile", "offline_access", "eshop-mcp" },
                clients[mcpClient].AllowedScopes.ToArray());
        }
    }

    [TestMethod]
    [DataRow("/connect/authorize?redirect_uri=https%3A%2F%2Fweb.test%2Fsignin-oidc&scope=openid", "https://web.test/")]
    [DataRow("/connect/authorize?client_id=webapp", "")]
    public void RedirectServiceExtractsConfiguredRedirect(string returnUrl, string expected)
    {
        var service = new RedirectService();

        Assert.AreEqual(expected, service.ExtractRedirectUriFromReturnUrl(returnUrl));
    }
}
