using Anthropic;
using Anthropic.Core;
using eShop.Basket.API.Grpc;
using eShop.WebApp.Services.OrderStatus.IntegrationEvents;
using eShop.WebAppComponents.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.Extensions.AI;
using Microsoft.IdentityModel.JsonWebTokens;

public static class Extensions
{
    public static void AddApplicationServices(this IHostApplicationBuilder builder)
    {
        builder.AddAuthenticationServices();

        builder.AddRabbitMqEventBus("EventBus")
               .AddEventBusSubscriptions();

        builder.Services.AddHttpForwarderWithServiceDiscovery();

        // Application services
        builder.Services.AddScoped<BasketState>();
        builder.Services.AddScoped<LogOutService>();
        builder.Services.AddSingleton<BasketService>();
        builder.Services.AddSingleton<OrderStatusNotificationService>();
        builder.Services.AddSingleton<IProductImageUrlProvider, ProductImageUrlProvider>();
        builder.AddChatProvider();

        // HTTP and GRPC client registrations
        builder.Services.AddGrpcClient<Basket.BasketClient>(o => o.Address = new("http://basket-api"))
            .AddAuthToken();

        builder.Services.AddHttpClient<CatalogService>(o => o.BaseAddress = new("https+http://catalog-api"))
            .AddApiVersion(2.0)
            .AddAuthToken();

        builder.Services.AddHttpClient<OrderingService>(o => o.BaseAddress = new("https+http://ordering-api"))
            .AddApiVersion(1.0)
            .AddAuthToken();
    }

    public static void AddEventBusSubscriptions(this IEventBusBuilder eventBus)
    {
        eventBus.AddSubscription<OrderStatusChangedToAwaitingValidationIntegrationEvent, OrderStatusChangedToAwaitingValidationIntegrationEventHandler>();
        eventBus.AddSubscription<OrderStatusChangedToPaidIntegrationEvent, OrderStatusChangedToPaidIntegrationEventHandler>();
        eventBus.AddSubscription<OrderStatusChangedToStockConfirmedIntegrationEvent, OrderStatusChangedToStockConfirmedIntegrationEventHandler>();
        eventBus.AddSubscription<OrderStatusChangedToShippedIntegrationEvent, OrderStatusChangedToShippedIntegrationEventHandler>();
        eventBus.AddSubscription<OrderStatusChangedToCancelledIntegrationEvent, OrderStatusChangedToCancelledIntegrationEventHandler>();
        eventBus.AddSubscription<OrderStatusChangedToSubmittedIntegrationEvent, OrderStatusChangedToSubmittedIntegrationEventHandler>();
    }

    public static void AddAuthenticationServices(this IHostApplicationBuilder builder)
    {
        var configuration = builder.Configuration;
        var services = builder.Services;

        JsonWebTokenHandler.DefaultInboundClaimTypeMap.Remove("sub");

        var identityUrl = configuration.GetRequiredValue("IdentityUrl");
        var callBackUrl = configuration.GetRequiredValue("CallBackUrl");
        var sessionCookieLifetime = configuration.GetValue("SessionCookieLifetimeMinutes", 60);

        // Add Authentication services
        services.AddAuthorization();
        services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        })
        .AddCookie(options => options.ExpireTimeSpan = TimeSpan.FromMinutes(sessionCookieLifetime))
        .AddOpenIdConnect(options =>
        {
            options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.Authority = identityUrl;
            options.SignedOutRedirectUri = callBackUrl;
            options.ClientId = "webapp";
            options.ClientSecret = "secret";
            options.ResponseType = "code";
            options.SaveTokens = true;
            options.GetClaimsFromUserInfoEndpoint = true;
            options.RequireHttpsMetadata = false;
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.Scope.Add("orders");
            options.Scope.Add("basket");
        });

        // Blazor auth services
        services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();
        services.AddCascadingAuthenticationState();
    }

    /// <summary>
    /// Registers the Shopping Assistant's <see cref="IChatClient"/> for the Chat Provider named by the
    /// <c>ChatProvider</c> setting (<c>None</c>, <c>Ollama</c>, <c>Foundry</c> or <c>Claude</c>, case-insensitive).
    /// Missing or <c>None</c> registers no client, which switches the Shopping Assistant off.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The setting has an unknown value, or it is <c>Claude</c> and <c>ANTHROPIC_API_KEY</c> is not set.
    /// </exception>
    public static void AddChatProvider(this IHostApplicationBuilder builder)
    {
        const string Setting = "ChatProvider";
        string[] allowed = ["None", "Ollama", "Foundry", "Claude"];

        var value = builder.Configuration[Setting];
        var chatProvider = string.IsNullOrWhiteSpace(value)
            ? "None"
            : allowed.FirstOrDefault(name => string.Equals(name, value.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException(
                    $"The {Setting} setting has the unknown value '{value}'. Allowed values: {string.Join(", ", allowed)}.");

        var chatClientBuilder = chatProvider switch
        {
            "Ollama" => builder.AddOllamaApiClient("chat").AddChatClient(),
            "Foundry" => builder.AddOpenAIClientFromConfiguration("chatModel").AddChatClient(),
            "Claude" => builder.AddClaudeChatClient(),
            _ => null
        };

        chatClientBuilder?.UseFunctionInvocation();
    }

    // There is no Aspire client integration for Anthropic, so the client is built from the official SDK.
    private static ChatClientBuilder AddClaudeChatClient(this IHostApplicationBuilder builder)
    {
        const string KeySetting = "ANTHROPIC_API_KEY";

        // The SDK would read the key itself, but only fail on the first request; checking here fails at startup instead.
        var apiKey = builder.Configuration[KeySetting];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                $"ChatProvider is Claude, but the {KeySetting} setting is missing. Set the AppHost's anthropic-api-key parameter.");
        }

        // Defaults suit a quick shopping helper; override them under Claude:ModelId and Claude:MaxOutputTokens.
        var claude = builder.Configuration.GetSection("Claude");
        var modelId = claude["ModelId"] is { Length: > 0 } configuredModelId ? configuredModelId : "claude-haiku-4-5";
        var maxOutputTokens = claude.GetValue("MaxOutputTokens", 1024);

        var chatClient = new AnthropicClient(new ClientOptions { ApiKey = apiKey })
            .AsIChatClient(modelId, maxOutputTokens);

        // The adapter has no "off" thinking mode; ReasoningEffort.None is what makes it send thinking.type=disabled.
        return builder.Services.AddChatClient(chatClient)
            .ConfigureOptions(options => options.Reasoning ??= new ReasoningOptions { Effort = ReasoningEffort.None });
    }

    public static async Task<string?> GetBuyerIdAsync(this AuthenticationStateProvider authenticationStateProvider)
    {
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        return user.FindFirst("sub")?.Value;
    }

    public static async Task<string?> GetUserNameAsync(this AuthenticationStateProvider authenticationStateProvider)
    {
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        return user.FindFirst("name")?.Value;
    }
}
