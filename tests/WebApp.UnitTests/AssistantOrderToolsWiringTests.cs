using System.Net;
using System.Text;
using eShop.Assistant.Tools;
using eShop.WebApp.Services.AccessTokens;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace eShop.WebApp.UnitTests;

// WebApp's own registration, end to end from the tool to the outbound request: only Ordering's HTTP is faked.
[TestClass]
public class AssistantOrderToolsWiringTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ListMyOrdersSendsTheCircuitsTokenToOrdering()
    {
        var ordering = new OrderingHandler();
        using var services = BuildWebAppServices(ordering);
        using var circuit = services.CreateScope();
        circuit.ServiceProvider.GetRequiredService<AccessTokenHolder>().AccessToken = "alice-access-token";
        services.GetRequiredService<CircuitServicesAccessor>().Services = circuit.ServiceProvider;

        var tools = circuit.ServiceProvider.GetRequiredService<AssistantTools>();
        await tools.ListMyOrders.InvokeAsync(new AIFunctionArguments(), TestContext.CancellationToken);

        var authorization = ordering.Requests.Single().Headers.Authorization;
        Assert.IsNotNull(authorization);
        Assert.AreEqual("Bearer alice-access-token", authorization.ToString());
    }

    private static ServiceProvider BuildWebAppServices(OrderingHandler ordering)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["IdentityUrl"] = "http://identity-api",
            ["CallBackUrl"] = "http://webapp",
            ["ConnectionStrings:EventBus"] = "amqp://localhost",
        });
        builder.AddApplicationServices();
        builder.Services.AddHttpClient<eShop.Assistant.Tools.Ordering.OrderingClient>()
            .ConfigurePrimaryHttpMessageHandler(() => ordering);
        return builder.Services.BuildServiceProvider();
    }

    private sealed class OrderingHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json"),
            });
        }
    }
}
