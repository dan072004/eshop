using System.Globalization;
using Anthropic.Models.Messages;
using Microsoft.Extensions.AI;

namespace eShop.WebApp.Chatbot;

/// <summary>
/// The Shopping Assistant's chat pipeline for <c>ChatProvider=Claude</c>, built around any inner <see cref="IChatClient"/>.
/// WebApp passes the Anthropic client; the eval passes a caching client wrapped around it, so it scores what ships.
/// </summary>
public static class ClaudeChatPipeline
{
    private const string DefaultModelId = "claude-haiku-4-5";
    private const int DefaultMaxOutputTokens = 1024;
    private static readonly TimeSpan DefaultTurnDeadline = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Wraps <paramref name="inner"/> with a turn deadline, function invocation, telemetry and request defaults: the model
    /// id and output limit (<c>claude-haiku-4-5</c> and 1024, overridable under <c>Claude:ModelId</c> and
    /// <c>Claude:MaxOutputTokens</c>), thinking disabled, and automatic prompt caching (a top-level <c>cache_control</c> of
    /// type <c>ephemeral</c>, through the raw representation factory). Options a caller sets take precedence. Each model
    /// call is a <c>chat</c> span with its tokens, cache tokens and, for a listed model, estimated cost, under the Turn's
    /// <c>orchestrate_tools</c> span with the Turn's totals.
    /// </summary>
    /// <param name="services">
    /// Resolves the loggers, and the host environment: spans carry message content only in Development. Optional outside
    /// dependency injection, which means no message content.
    /// </param>
    /// <param name="turnDeadline">
    /// How long a whole turn, every model request and tool call, may take before it is cancelled; 60 seconds by default.
    /// </param>
    /// <exception cref="InvalidOperationException"><c>Claude:MaxOutputTokens</c> is not a positive whole number.</exception>
    public static IChatClient Build(
        IChatClient inner, IConfiguration configuration, IServiceProvider? services = null, TimeSpan? turnDeadline = null)
    {
        var modelId = GetModelId(configuration);
        var maxOutputTokens = GetMaxOutputTokens(configuration);
        var deadline = turnDeadline ?? DefaultTurnDeadline;

        // The Anthropic adapter has no "off" thinking mode; ReasoningEffort.None is what makes it send thinking.type=disabled.
        return new ChatClientBuilder(inner)
            .Use(turn => new TurnDeadlineChatClient(turn, deadline))
            .ConfigureOptions(options =>
            {
                options.ModelId ??= modelId;
                options.MaxOutputTokens ??= maxOutputTokens;
                options.Reasoning ??= new ReasoningOptions { Effort = ReasoningEffort.None };
                options.RawRepresentationFactory ??= _ => AskForPromptCaching(options);
            })
            .UseFunctionInvocation()
            .UseChatTelemetry()
            .Use(inner => new ClaudeUsageTelemetryChatClient(inner))
            .Build(services);
    }

    // Automatic caching: Anthropic caches up to the last cacheable block, so the cached prefix grows through the Turn.
    // The adapter sends these parameters' model and output limit over the options' own, so they are copied in.
    private static MessageCreateParams AskForPromptCaching(ChatOptions options) => new()
    {
        Model = options.ModelId!,
        MaxTokens = options.MaxOutputTokens!.Value,
        Messages = [],
        CacheControl = new CacheControlEphemeral(),
    };

    /// <summary>The model id: <c>Claude:ModelId</c>, or <c>claude-haiku-4-5</c>.</summary>
    public static string GetModelId(IConfiguration configuration)
        => configuration["Claude:ModelId"] is { Length: > 0 } modelId ? modelId : DefaultModelId;

    /// <summary>The output limit: <c>Claude:MaxOutputTokens</c>, or 1024.</summary>
    /// <exception cref="InvalidOperationException">The setting is not a positive whole number.</exception>
    public static int GetMaxOutputTokens(IConfiguration configuration)
    {
        var value = configuration["Claude:MaxOutputTokens"];
        if (string.IsNullOrWhiteSpace(value))
        {
            return DefaultMaxOutputTokens;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxOutputTokens) && maxOutputTokens > 0
            ? maxOutputTokens
            : throw new InvalidOperationException(
                $"The Claude:MaxOutputTokens setting has the invalid value '{value}'. It must be a positive whole number.");
    }
}
