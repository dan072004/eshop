using eShop.Assistant.Tools;
using eShop.ServiceDefaults;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Server;

namespace eShop.Mcp;

internal static class McpServerExtensions
{
    /// <summary>The audience a token must name to be accepted here (ADR 0005).</summary>
    public const string Audience = "eshop-mcp";

    public const string McpPath = "/mcp";

    private static readonly string[] ScopesSupported = ["openid", "profile", "offline_access", Audience, "orders"];

    /// <summary>
    /// Buyer sign-in (ADR 0005): JWT bearer against Identity.API, validating the <c>eshop-mcp</c> audience, with the
    /// SDK's MCP handler answering challenges with a pointer to the protected resource metadata (RFC 9728).
    /// Configured here rather than through the shared <c>AddDefaultAuthentication</c>, which turns audience validation off.
    /// </summary>
    public static IHostApplicationBuilder AddBuyerAuthentication(this IHostApplicationBuilder builder)
    {
        var identityUrl = builder.Configuration.GetSection("Identity").GetRequiredValue("Url");

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
                options.Audience = Audience;
                options.TokenValidationParameters.ValidIssuers = [identityUrl];
                options.TokenValidationParameters.ValidateAudience = true;
                // The token-forwarding handler (AddAuthToken) reads the validated token back from the request.
                options.SaveToken = true;
            })
            .AddMcp(options =>
            {
                options.ResourceMetadata = new()
                {
                    AuthorizationServers = [identityUrl],
                    ScopesSupported = [.. ScopesSupported],
                };
                // "resource" is the MCP endpoint exactly as the client reached it, so the client's check that the
                // metadata belongs to the server it is talking to passes for localhost and 127.0.0.1 alike.
                options.Events.OnResourceMetadataRequest = context =>
                {
                    var request = context.HttpContext.Request;
                    context.ResourceMetadata!.Resource = $"{request.Scheme}://{request.Host}{request.PathBase}{McpPath}";
                    return Task.CompletedTask;
                };
            });

        builder.Services.AddAuthorization();

        return builder;
    }

    /// <summary>
    /// The MCP server: Streamable HTTP in stateless mode, so every tool call is authorised by the token on that
    /// request (ADR 0005). The tools are the shared library's <see cref="AIFunction"/>s, wrapped unchanged.
    /// </summary>
    public static IHostApplicationBuilder AddMcpServer(this IHostApplicationBuilder builder)
    {
        builder.Services.AddAssistantTools();

        builder.Services.AddMcpServer()
            .WithHttpTransport(options =>
            {
                options.Stateless = true;
                // Built per request from the request's services, so the tools' typed HTTP clients are never
                // captured by a long-lived server instance.
                options.ConfigureSessionOptions = (httpContext, serverOptions, _) =>
                {
                    var tools = httpContext.RequestServices.GetRequiredService<AssistantTools>();
                    serverOptions.ToolCollection = [McpServerTool.Create(tools.SearchCatalog)];
                    return Task.CompletedTask;
                };
            });

        return builder;
    }
}
