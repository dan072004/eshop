using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace eShop.AppHost.UnitTests;

/// <summary>
/// eShop.Mcp's wiring, read from the real AppHost model (AppHost.cs) without starting anything, so it is compared
/// with how the other services are wired rather than with a copy of the wiring.
/// </summary>
[TestClass]
public class EshopMcpWiringTests
{
    private const string UseHttpEndpoints = "ESHOP_USE_HTTP_ENDPOINTS";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DoNotParallelize]
    public async Task EshopMcpReferencesTheToolBackendsAndWaitsForIdentityWithTheUrlTheApisGet()
    {
        await using var appHost = await CreateAppHostAsync(useHttpEndpoints: false);
        var mcp = Resource(appHost, "eshop-mcp");

        var environment = await EnvironmentOf(appHost, mcp);
        Assert.AreEqual("{policy-api.bindings.http.url}", environment["services__policy-api__http__0"]);
        Assert.AreEqual("{catalog-api.bindings.http.url}", environment["services__catalog-api__http__0"]);
        Assert.AreEqual("{ordering-api.bindings.http.url}", environment["services__ordering-api__http__0"]);

        // Tokens carry the issuer Identity.API was reached on, so eShop.Mcp must use the URL Ordering.API validates.
        var orderingEnvironment = await EnvironmentOf(appHost, Resource(appHost, "ordering-api"));
        Assert.AreEqual("{identity-api.bindings.https.url}", environment["Identity__Url"]);
        Assert.AreEqual(orderingEnvironment["Identity__Url"], environment["Identity__Url"]);

        Assert.IsTrue(mcp.Annotations.OfType<WaitAnnotation>().Any(wait => wait.Resource.Name == "identity-api"));
        // WithHttpHealthCheck keys its check "{resource}_{endpoint}_{path}_{status}_check": GET /health on the
        // https endpoint, expecting 200.
        var healthCheck = mcp.Annotations.OfType<HealthCheckAnnotation>().Single();
        Assert.AreEqual("eshop-mcp_https_/health_200_check", healthCheck.Key);
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task EshopMcpHasOnlyLocalEndpointsFromItsHttpsProfile()
    {
        await using var appHost = await CreateAppHostAsync(useHttpEndpoints: false);
        var endpoints = Resource(appHost, "eshop-mcp").Annotations.OfType<EndpointAnnotation>().ToArray();

        CollectionAssert.AreEquivalent(new[] { "https", "http" }, endpoints.Select(e => e.Name).ToArray());
        Assert.IsFalse(endpoints.Any(e => e.IsExternal));
        // Fixed in launchSettings.json, so a client's configured URL keeps working across restarts.
        Assert.AreEqual(7229, endpoints.Single(e => e.Name == "https").Port);
    }

    // One value names eShop.Mcp's API resource in Identity.API and is the audience and metadata "resource" eShop.Mcp
    // uses, so the three can't disagree (ADR 0005). It follows the active launch profile.
    [TestMethod]
    [DoNotParallelize]
    [DataRow(false, "{eshop-mcp.bindings.https.url}/mcp")]
    [DataRow(true, "{eshop-mcp.bindings.http.url}/mcp")]
    public async Task IdentityApiAndEshopMcpReceiveTheSameMcpUrl(bool useHttpEndpoints, string expectedUrl)
    {
        await using var appHost = await CreateAppHostAsync(useHttpEndpoints);

        var identityEnvironment = await EnvironmentOf(appHost, Resource(appHost, "identity-api"));
        var mcpEnvironment = await EnvironmentOf(appHost, Resource(appHost, "eshop-mcp"));

        Assert.AreEqual(expectedUrl, identityEnvironment["EshopMcpUrl"]);
        Assert.AreEqual(expectedUrl, mcpEnvironment["EshopMcpUrl"]);
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task EshopMcpUsesTheHttpProfileWhenHttpEndpointsAreForced()
    {
        await using var appHost = await CreateAppHostAsync(useHttpEndpoints: true);
        var mcp = Resource(appHost, "eshop-mcp");

        var endpoint = mcp.Annotations.OfType<EndpointAnnotation>().Single();
        Assert.AreEqual("http", endpoint.Name);
        Assert.AreEqual(5229, endpoint.Port);
        Assert.AreEqual("{identity-api.bindings.http.url}", (await EnvironmentOf(appHost, mcp))["Identity__Url"]);
    }

    // The AppHost reads ESHOP_USE_HTTP_ENDPOINTS from the process environment, hence [DoNotParallelize] above.
    private async Task<IDistributedApplicationTestingBuilder> CreateAppHostAsync(bool useHttpEndpoints)
    {
        var previous = Environment.GetEnvironmentVariable(UseHttpEndpoints);
        Environment.SetEnvironmentVariable(UseHttpEndpoints, useHttpEndpoints ? "1" : null);
        try
        {
            return await DistributedApplicationTestingBuilder.CreateAsync<Program>(TestContext.CancellationToken);
        }
        finally
        {
            Environment.SetEnvironmentVariable(UseHttpEndpoints, previous);
        }
    }

    private static IResource Resource(IDistributedApplicationTestingBuilder appHost, string name) =>
        appHost.Resources.Single(resource => resource.Name == name);

    // Publish mode renders references as manifest expressions, so nothing has to be provisioned or started.
    private async Task<Dictionary<string, string>> EnvironmentOf(IDistributedApplicationTestingBuilder appHost, IResource resource)
    {
        using var services = appHost.Services.BuildServiceProvider();
        var executionContext = new DistributedApplicationExecutionContext(
            new DistributedApplicationExecutionContextOptions(DistributedApplicationOperation.Publish) { Services = services });

        var configuration = await ExecutionConfigurationBuilder.Create(resource)
            .WithEnvironmentVariablesConfig()
            .BuildAsync(executionContext, cancellationToken: TestContext.CancellationToken);

        Assert.IsNull(configuration.Exception);
        return configuration.EnvironmentVariables.ToDictionary();
    }
}
