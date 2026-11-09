using System.Globalization;
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

    /// <summary>
    /// Wraps <paramref name="inner"/> with function invocation and request defaults: the model id and output limit
    /// (<c>claude-haiku-4-5</c> and 1024, overridable under <c>Claude:ModelId</c> and <c>Claude:MaxOutputTokens</c>)
    /// and thinking disabled. Options a caller sets take precedence.
    /// </summary>
    /// <param name="services">Resolves the logger for function invocation; optional outside dependency injection.</param>
    /// <exception cref="InvalidOperationException"><c>Claude:MaxOutputTokens</c> is not a positive whole number.</exception>
    public static IChatClient Build(IChatClient inner, IConfiguration configuration, IServiceProvider? services = null)
    {
        var modelId = GetModelId(configuration);
        var maxOutputTokens = GetMaxOutputTokens(configuration);

        // The Anthropic adapter has no "off" thinking mode; ReasoningEffort.None is what makes it send thinking.type=disabled.
        return new ChatClientBuilder(inner)
            .ConfigureOptions(options =>
            {
                options.ModelId ??= modelId;
                options.MaxOutputTokens ??= maxOutputTokens;
                options.Reasoning ??= new ReasoningOptions { Effort = ReasoningEffort.None };
            })
            .UseFunctionInvocation()
            .Build(services);
    }

    internal static string GetModelId(IConfiguration configuration)
        => configuration["Claude:ModelId"] is { Length: > 0 } modelId ? modelId : DefaultModelId;

    internal static int GetMaxOutputTokens(IConfiguration configuration)
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
