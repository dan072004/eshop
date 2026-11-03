using System.Net;
using eShop.WebApp.Services.AccessTokens;
using Microsoft.Extensions.DependencyInjection;

namespace eShop.WebApp.UnitTests;

[TestClass]
public class AccessTokenHandlerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task HolderWithATokenSendsItAsABearerToken()
    {
        var inner = new RecordingHandler();
        using var client = BuildClient(inner, accessToken: "alice-access-token");

        await client.GetAsync("http://ordering-api/api/orders", TestContext.CancellationToken);

        var authorization = inner.Requests.Single().Headers.Authorization;
        Assert.IsNotNull(authorization);
        Assert.AreEqual("Bearer", authorization.Scheme);
        Assert.AreEqual("alice-access-token", authorization.Parameter);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "empty holder")]
    [DataRow("", DisplayName = "empty token")]
    public async Task EmptyHolderSendsNoAuthorizationHeader(string? accessToken)
    {
        var inner = new RecordingHandler();
        using var client = BuildClient(inner, accessToken);

        await client.GetAsync("http://ordering-api/api/orders", TestContext.CancellationToken);

        Assert.IsNull(inner.Requests.Single().Headers.Authorization);
    }

    [TestMethod]
    public async Task OutsideACircuitSendsNoAuthorizationHeader()
    {
        var inner = new RecordingHandler();
        using var client = new HttpClient(new AccessTokenHandler(new CircuitServicesAccessor()) { InnerHandler = inner });

        await client.GetAsync("http://ordering-api/api/orders", TestContext.CancellationToken);

        Assert.IsNull(inner.Requests.Single().Headers.Authorization);
    }

    // The holder lives in the circuit's scope; the handler finds it through the accessor, as it does during a circuit event.
    private static HttpClient BuildClient(RecordingHandler inner, string? accessToken)
    {
        var circuitServices = new ServiceCollection().AddScoped<AccessTokenHolder>().BuildServiceProvider().CreateScope().ServiceProvider;
        circuitServices.GetRequiredService<AccessTokenHolder>().AccessToken = accessToken;
        var accessor = new CircuitServicesAccessor { Services = circuitServices };

        return new HttpClient(new AccessTokenHandler(accessor) { InnerHandler = inner });
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
