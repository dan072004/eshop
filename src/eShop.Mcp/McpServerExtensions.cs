using eShop.Assistant.Tools;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Server;

namespace eShop.Mcp;

internal static class McpServerExtensions
{
    /// <summary>
    /// The setting holding eShop.Mcp's own <c>/mcp</c> URL, from the AppHost. Identity.API names eShop.Mcp's API
    /// resource with the same value (ADR 0005).
    /// </summary>
    public const string McpUrlSetting = "EshopMcpUrl";

    public const string McpPath = "/mcp";

    /// <summary>The scope MCP clients request. Identity.API's eShop.Mcp resource lists it.</summary>
    private const string Scope = "eshop-mcp";

    private static readonly string[] ScopesSupported = ["openid", "profile", "offline_access", Scope];

    /// <summary>
    /// Buyer sign-in (ADR 0005): JWT bearer against Identity.API, validating eShop.Mcp's URL as the audience, with the
    /// SDK's MCP handler answering challenges with a pointer to the protected resource metadata (RFC 9728).
    /// Configured here rather than through the shared <c>AddDefaultAuthentication</c>, which turns audience validation off.
    /// </summary>
    public static IHostApplicationBuilder AddBuyerAuthentication(this IHostApplicationBuilder builder)
    {
        var identityUrl = builder.Configuration.GetSection("Identity").GetRequiredValue("Url");
        var mcpUrl = builder.Configuration.GetRequiredValue(McpUrlSetting);

        // Keep "sub" as "sub", as the other services do.
        JsonWebTokenHandler.DefaultInboundClaimTypeMap.Remove("sub");

        builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.Authority = identityUrl;
                options.RequireHttpsMetadata = false;
                // A client names eShop.Mcp by this URL as its resource indicator (RFC 8707), and Identity.API then
                // issues the token for that resource alone.
                options.Audience = mcpUrl;
                options.TokenValidationParameters.ValidIssuers = [identityUrl];
                options.TokenValidationParameters.ValidateAudience = true;
                // The token-exchange handler reads the validated token back from the request.
                options.SaveToken = true;
            })
            .AddMcp(options =>
            {
                // "resource" is exactly the configured URL, not derived from the request: it must be the name the
                // token's audience carries. A client that connected on another host form (127.0.0.1 instead of
                // localhost) sees the mismatch and refuses before sign-in.
                options.ResourceMetadata = new()
                {
                    Resource = mcpUrl,
                    AuthorizationServers = [identityUrl],
                    ScopesSupported = [.. ScopesSupported],
                };
            });

        builder.Services.AddAuthorization();

        return builder;
    }

    /// <summary>
    /// The MCP server: Streamable HTTP in stateless mode, so every tool call is authorised by the token on that
    /// request (ADR 0005). The tools are the shared library's <see cref="AIFunction"/>s, wrapped unchanged.
    /// </summary>
    public static IHostApplicationBuilder AddEshopMcpServer(this IHostApplicationBuilder builder)
    {
        // Only Ordering needs the Buyer: on every request the caller's validated token is exchanged at Identity.API
        // for an Ordering token, and only that is sent (no passthrough, ADR 0005). The policy and catalog clients send
        // no token. Ordering.API keeps the ownership check.
        var identityUrl = builder.Configuration.GetSection("Identity").GetRequiredValue("Url");
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddHttpClient<TokenExchangeClient>(client => client.BaseAddress = new($"{identityUrl.TrimEnd('/')}/"));
        builder.Services.AddTransient<OrdersTokenExchangeHandler>();
        builder.Services.AddAssistantTools().OrderingClient
            .AddHttpMessageHandler<OrdersTokenExchangeHandler>();

        builder.Services.AddMcpServer()
            .WithHttpTransport(options =>
            {
                options.Stateless = true;
                // Built per request from the request's services, so the tools' typed HTTP clients are never
                // captured by a long-lived server instance.
                options.ConfigureSessionOptions = (httpContext, serverOptions, _) =>
                {
                    var tools = httpContext.RequestServices.GetRequiredService<AssistantTools>();
                    serverOptions.ToolCollection = [.. tools.All.Select(tool => McpServerTool.Create(tool))];
                    return Task.CompletedTask;
                };
            });

        return builder;
    }
}
