namespace eShop.Identity.API.Configuration
{
    public class Config
    {
        // ApiResources define the apis in your system
        public static IEnumerable<ApiResource> GetApis(IConfiguration configuration)
        {
            // eShop.Mcp's /mcp URL, from the AppHost. MCP clients name eShop.Mcp by this URL as their resource
            // indicator (RFC 8707), and eShop.Mcp validates it as the audience (ADR 0005).
            var eshopMcpUrl = configuration.GetRequiredValue("EshopMcpUrl");

            return new List<ApiResource>
            {
                // Scopes ties each resource to its scope, so a token that requests "basket" names "basket" as an audience.
                // Each API accepts only tokens for its own audience (ServiceDefaults), so the others refuse a token for
                // one API, and every API refuses the MCP clients' token for eShop.Mcp.
                new ApiResource("orders", "Orders Service") { Scopes = { "orders" } },
                new ApiResource("basket", "Basket Service") { Scopes = { "basket" } },
                new ApiResource("webhooks", "Webhooks registration Service") { Scopes = { "webhooks" } },
                new ApiResource(eshopMcpUrl, "AdventureWorks MCP Server") { Scopes = { "eshop-mcp" } },
            };
        }

        // ApiScope is used to protect the API 
        //The effect is the same as that of API resources in IdentityServer 3.x
        public static IEnumerable<ApiScope> GetApiScopes()
        {
            return new List<ApiScope>
            {
                new ApiScope("orders", "Orders Service"),
                new ApiScope("basket", "Basket Service"),
                new ApiScope("webhooks", "Webhooks registration Service"),
                new ApiScope("eshop-mcp", "AdventureWorks MCP Server: search the store and read your orders"),
            };
        }

        // Identity resources are data like user ID, name, or email address of a user
        // see: http://docs.identityserver.io/en/release/configuration/resources.html
        public static IEnumerable<IdentityResource> GetResources()
        {
            return new List<IdentityResource>
            {
                new IdentityResources.OpenId(),
                new IdentityResources.Profile()
            };
        }

        // client want to access resources (aka scopes)
        public static IEnumerable<Client> GetClients(IConfiguration configuration)
        {
            var claudeCodeCallbackPort = configuration.GetRequiredValue("ClaudeCodeCallbackPort");

            return new List<Client>
            {
                new Client
                {
                    ClientId = "maui",
                    ClientName = "eShop MAUI OpenId Client",
                    AllowedGrantTypes = GrantTypes.Code,                    
                    //Used to retrieve the access token on the back channel.
                    ClientSecrets =
                    {
                        new Secret("secret".Sha256())
                    },
                    RedirectUris = { configuration["MauiCallback"] },
                    RequireConsent = false,
                    RequirePkce = true,
                    PostLogoutRedirectUris = { $"{configuration["MauiCallback"]}/Account/Redirecting" },
                    //AllowedCorsOrigins = { "http://eshopxamarin" },
                    AllowedScopes = new List<string>
                    {
                        IdentityServerConstants.StandardScopes.OpenId,
                        IdentityServerConstants.StandardScopes.Profile,
                        IdentityServerConstants.StandardScopes.OfflineAccess,
                        "orders",
                        "basket",
                        "mobileshoppingagg",
                        "webhooks"
                    },
                    //Allow requesting refresh tokens for long lived API access
                    AllowOfflineAccess = true,
                    AllowAccessTokensViaBrowser = true,
                    AlwaysIncludeUserClaimsInIdToken = true,
                    AccessTokenLifetime = 60*60*2, // 2 hours
                    IdentityTokenLifetime= 60*60*2 // 2 hours
                },
                new Client
                {
                    ClientId = "webapp",
                    ClientName = "WebApp Client",
                    ClientSecrets = new List<Secret>
                    {
                        new Secret("secret".Sha256())
                    },
                    ClientUri = $"{configuration["WebAppClient"]}",                             // public uri of the client
                    AllowedGrantTypes = GrantTypes.Code,
                    AllowAccessTokensViaBrowser = false,
                    RequireConsent = false,
                    AllowOfflineAccess = true,
                    AlwaysIncludeUserClaimsInIdToken = true,
                    RequirePkce = false,
                    RedirectUris = new List<string>
                    {
                        $"{configuration["WebAppClient"]}/signin-oidc"
                    },
                    PostLogoutRedirectUris = new List<string>
                    {
                        $"{configuration["WebAppClient"]}/signout-callback-oidc"
                    },
                    AllowedScopes = new List<string>
                    {
                        IdentityServerConstants.StandardScopes.OpenId,
                        IdentityServerConstants.StandardScopes.Profile,
                        IdentityServerConstants.StandardScopes.OfflineAccess,
                        "orders",
                        "basket",
                        "webshoppingagg",
                        "webhooks"
                    },
                    AccessTokenLifetime = 60*60*2, // 2 hours
                    IdentityTokenLifetime= 60*60*2 // 2 hours
                },
                new Client
                {
                    ClientId = "webhooksclient",
                    ClientName = "Webhooks Client",
                    ClientSecrets = new List<Secret>
                    {
                        new Secret("secret".Sha256())
                    },
                    ClientUri = $"{configuration["WebhooksWebClient"]}",                             // public uri of the client
                    AllowedGrantTypes = GrantTypes.Code,
                    AllowAccessTokensViaBrowser = false,
                    RequireConsent = false,
                    AllowOfflineAccess = true,
                    AlwaysIncludeUserClaimsInIdToken = true,
                    RedirectUris = new List<string>
                    {
                        $"{configuration["WebhooksWebClient"]}/signin-oidc"
                    },
                    PostLogoutRedirectUris = new List<string>
                    {
                        $"{configuration["WebhooksWebClient"]}/signout-callback-oidc"
                    },
                    AllowedScopes = new List<string>
                    {
                        IdentityServerConstants.StandardScopes.OpenId,
                        IdentityServerConstants.StandardScopes.Profile,
                        IdentityServerConstants.StandardScopes.OfflineAccess,
                        "webhooks"
                    },
                    AccessTokenLifetime = 60*60*2, // 2 hours
                    IdentityTokenLifetime= 60*60*2 // 2 hours
                },
                // Local MCP clients sign in as the Buyer (ADR 0005). Client setup: README.md, "Connect Claude Code or VS Code".
                McpClient(
                    "claude-code",
                    "Claude Code",
                    [
                        $"http://localhost:{claudeCodeCallbackPort}/callback",
                        // Claude Code v2.1.229 sent the 127.0.0.1 form instead.
                        $"http://127.0.0.1:{claudeCodeCallbackPort}/callback"
                    ]),
                McpClient(
                    "vscode",
                    "Visual Studio Code",
                    [
                        "http://127.0.0.1:33418",
                        // VS Code's own client registration uses the trailing-slash form, and Duende matches exactly.
                        // Loopback only. https://vscode.dev/redirect would let any website start a vscode sign-in
                        // whose code lands on a page this app doesn't control.
                        "http://127.0.0.1:33418/"
                    ]),
                // eShop.Mcp exchanges a Buyer's eShop.Mcp token for a short-lived Ordering token, and nothing else
                // (ADR 0005, TokenExchangeGrantValidator).
                new Client
                {
                    ClientId = "eshop-mcp-server",
                    ClientName = "AdventureWorks MCP Server",
                    ClientSecrets = { new Secret(configuration.GetRequiredValue("EshopMcpServerSecret").Sha256()) },
                    AllowedGrantTypes = { OidcConstants.GrantTypes.TokenExchange },
                    AllowedScopes = { "orders" },
                    AllowOfflineAccess = false,
                    RequireConsent = false,
                    AccessTokenLifetime = 5 * 60
                },
                new Client
                {
                    ClientId = "basketswaggerui",
                    ClientName = "Basket Swagger UI",
                    AllowedGrantTypes = GrantTypes.Implicit,
                    AllowAccessTokensViaBrowser = true,

                    RedirectUris = { $"{configuration["BasketApiClient"]}/swagger/oauth2-redirect.html" },
                    PostLogoutRedirectUris = { $"{configuration["BasketApiClient"]}/swagger/" },

                    AllowedScopes =
                    {
                        "basket"
                    }
                },
                new Client
                {
                    ClientId = "orderingswaggerui",
                    ClientName = "Ordering Swagger UI",
                    AllowedGrantTypes = GrantTypes.Implicit,
                    AllowAccessTokensViaBrowser = true,

                    RedirectUris = { $"{configuration["OrderingApiClient"]}/swagger/oauth2-redirect.html" },
                    PostLogoutRedirectUris = { $"{configuration["OrderingApiClient"]}/swagger/" },

                    AllowedScopes =
                    {
                        "orders"
                    }
                },
                new Client
                {
                    ClientId = "webhooksswaggerui",
                    ClientName = "WebHooks Service Swagger UI",
                    AllowedGrantTypes = GrantTypes.Implicit,
                    AllowAccessTokensViaBrowser = true,

                    RedirectUris = { $"{configuration["WebhooksApiClient"]}/swagger/oauth2-redirect.html" },
                    PostLogoutRedirectUris = { $"{configuration["WebhooksApiClient"]}/swagger/" },

                    AllowedScopes =
                    {
                        "webhooks"
                    }
                }
            };
        }

        // An MCP client (ADR 0005): public (no secret), authorization code with PKCE (S256), consent required and
        // remembered, offline access with refresh-token rotation. No "orders": the client names eShop.Mcp's URL as the
        // resource, so the token's only audience is that URL and "orders" would be dropped from it anyway. eShop.Mcp
        // gets Ordering's token by exchange instead.
        private static Client McpClient(string clientId, string clientName, ICollection<string> redirectUris) => new()
        {
            ClientId = clientId,
            ClientName = clientName,
            AllowedGrantTypes = GrantTypes.Code,
            RequireClientSecret = false,
            RequirePkce = true,
            AllowPlainTextPkce = false,
            RedirectUris = redirectUris,
            RequireConsent = true,
            AllowRememberConsent = true,
            AllowOfflineAccess = true,
            RefreshTokenUsage = TokenUsage.OneTimeOnly,
            AllowedScopes =
            {
                IdentityServerConstants.StandardScopes.OpenId,
                IdentityServerConstants.StandardScopes.Profile,
                IdentityServerConstants.StandardScopes.OfflineAccess,
                "eshop-mcp"
            },
            AccessTokenLifetime = 60 * 60 * 2, // 2 hours
            IdentityTokenLifetime = 60 * 60 * 2 // 2 hours
        };
    }
}
