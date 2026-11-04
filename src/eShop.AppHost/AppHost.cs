using System.IO.Pipes;
using eShop.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddForwardedHeaders();
builder.AddAzureContainerAppEnvironment("aca");

var redis = builder.AddRedis("redis");
var rabbitMq = builder.AddRabbitMQ("eventbus")
    .WithLifetime(ContainerLifetime.Persistent);
var postgres = builder.AddPostgres("postgres")
    .WithImage("ankane/pgvector")
    .WithImageTag("latest")
    .WithLifetime(ContainerLifetime.Persistent);

var catalogDb = postgres.AddDatabase("catalogdb");
var identityDb = postgres.AddDatabase("identitydb");
var orderDb = postgres.AddDatabase("orderingdb");
var webhooksDb = postgres.AddDatabase("webhooksdb");

var launchProfileName = ShouldUseHttpForEndpoints() ? "http" : "https";

// Services
var identityApi = builder.AddDotnetProject("identity-api", "../Identity.API", o => o.LaunchProfileName = launchProfileName)
    .WithExternalHttpEndpoints()
    .WithReference(identityDb)
    .WithHttpHealthCheck("/health");

var identityEndpoint = identityApi.GetEndpoint(launchProfileName);

var basketApi = builder.AddDotnetProject("basket-api", "../Basket.API")
    .WithReference(redis)
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithEnvironment("Identity__Url", identityEndpoint);
redis.WithParentRelationship(basketApi);

var catalogApi = builder.AddDotnetProject("catalog-api", "../Catalog.API")
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithReference(catalogDb);

var orderingApi = builder.AddDotnetProject("ordering-api", "../Ordering.API")
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithReference(orderDb).WaitFor(orderDb)
    .WithHttpHealthCheck("/health")
    .WithEnvironment("Identity__Url", identityEndpoint);

builder.AddDotnetProject("order-processor", "../OrderProcessor")
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithReference(orderDb)
    .WaitFor(orderingApi); // wait for the orderingApi to be ready because that contains the EF migrations

builder.AddDotnetProject("payment-processor", "../PaymentProcessor")
    .WithReference(rabbitMq).WaitFor(rabbitMq);

var policyApi = builder.AddPolicyApi(postgres);

var webHooksApi = builder.AddDotnetProject("webhooks-api", "../Webhooks.API")
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithReference(webhooksDb)
    .WithEnvironment("Identity__Url", identityEndpoint);

// Reverse proxies
builder.AddYarp("mobile-bff")
    .WithExternalHttpEndpoints()
    .ConfigureMobileBffRoutes(catalogApi, orderingApi, identityApi);

// Apps
var webhooksClient = builder.AddDotnetProject("webhooksclient", "../WebhookClient", o => o.LaunchProfileName = launchProfileName)
    .WithReference(webHooksApi)
    .WithEnvironment("IdentityUrl", identityEndpoint);

var webApp = builder.AddDotnetProject("webapp", "../WebApp", o => o.LaunchProfileName = launchProfileName)
    .WithExternalHttpEndpoints()
    .WithUrls(c => c.Urls.ForEach(u => u.DisplayText = $"Online Store ({u.Endpoint?.EndpointName})"))
    .WithReference(basketApi)
    .WithReference(catalogApi)
    .WithReference(orderingApi)
    .WithPolicySearch(policyApi)
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WaitFor(identityApi)
    .WithEnvironment("IdentityUrl", identityEndpoint);

// MCP server for local AI clients (Claude Code, VS Code). Buyers sign in through Identity.API (ADR 0005), so its
// tokens must have the same issuer as the APIs'; it stays local, with fixed ports from its launch settings.
var eshopMcp = builder.AddDotnetProject("eshop-mcp", "../eShop.Mcp", o => o.LaunchProfileName = launchProfileName)
    .WithReference(policyApi)
    .WithReference(catalogApi)
    .WithReference(orderingApi)
    .WaitFor(identityApi)
    .WithEnvironment("Identity__Url", identityEndpoint)
    .WithHttpHealthCheck("/health");

// eShop.Mcp's /mcp URL for the active profile: the name of its API resource in Identity.API, and the audience and
// metadata "resource" eShop.Mcp itself uses (ADR 0005). One value, so they can't disagree.
var eshopMcpUrl = ReferenceExpression.Create($"{eshopMcp.GetEndpoint(launchProfileName)}/mcp");
eshopMcp.WithEnvironment("EshopMcpUrl", eshopMcpUrl);

// The generated secret authenticates eShop.Mcp as the confidential client eshop-mcp-server when it exchanges a
// Buyer's token for an Ordering token (ADR 0005).
var eshopMcpServerSecret = builder.AddParameter(
    "eshop-mcp-server-secret", new GenerateParameterDefault { MinLength = 32, Special = false }, secret: true);
identityApi.WithEnvironment("EshopMcpServerSecret", eshopMcpServerSecret);
eshopMcp.WithEnvironment("EshopMcpServerSecret", eshopMcpServerSecret);

// Set ChatProvider (None, Ollama, Foundry or Claude) and EmbeddingProvider (None, Ollama or Foundry),
// both default None, to switch on the Shopping Assistant and semantic catalog and Policy search.
builder.AddChatAndEmbeddingProviders(catalogApi, webApp, policyApi);

// Wire up the callback urls (self referencing)
webApp.WithEnvironment("CallBackUrl", webApp.GetEndpoint(launchProfileName));
webhooksClient.WithEnvironment("CallBackUrl", webhooksClient.GetEndpoint(launchProfileName));

// Identity has a reference to all of the apps for callback urls (and to eShop.Mcp's URL as a resource name),
// this is a cyclic reference
identityApi.WithEnvironment("BasketApiClient", basketApi.GetEndpoint("http"))
           .WithEnvironment("OrderingApiClient", orderingApi.GetEndpoint("http"))
           .WithEnvironment("WebhooksApiClient", webHooksApi.GetEndpoint("http"))
           .WithEnvironment("WebhooksWebClient", webhooksClient.GetEndpoint(launchProfileName))
           .WithEnvironment("WebAppClient", webApp.GetEndpoint(launchProfileName))
           .WithEnvironment("EshopMcpUrl", eshopMcpUrl);

builder.Build().Run();

// For test use only.
// Looks for an environment variable that forces the use of HTTP for all the endpoints. We
// are doing this for ease of running the Playwright tests in CI.
static bool ShouldUseHttpForEndpoints()
{
    const string EnvVarName = "ESHOP_USE_HTTP_ENDPOINTS";
    var envValue = Environment.GetEnvironmentVariable(EnvVarName);

    // Attempt to parse the environment variable value; return true if it's exactly "1".
    return int.TryParse(envValue, out int result) && result == 1;
}
