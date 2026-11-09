using Microsoft.Extensions.Configuration;

namespace ShoppingAssistant.Evals.OptIn;

/// <summary>
/// The evals' opt-in. Model-calling tests (<see cref="EvalFactAttribute"/>) run only with <c>ESHOP_EVALS=1</c>, and
/// then need an Anthropic API key from configuration: the <c>ANTHROPIC_API_KEY</c> environment variable or this
/// project's user-secrets. There is deliberately no fallback to an SDK login profile, so a run never spends from
/// whichever profile happens to be active.
/// </summary>
public static class EvalSettings
{
    public const string EnvironmentVariable = "ESHOP_EVALS";

    public const string AnthropicApiKeyName = "ANTHROPIC_API_KEY";

    public static bool IsEnabled => Environment.GetEnvironmentVariable(EnvironmentVariable) == "1";

    /// <summary>The configuration a run reads: user-secrets, then environment variables (which win).</summary>
    public static IConfiguration Configuration { get; } = new ConfigurationBuilder()
        .AddUserSecrets(typeof(EvalSettings).Assembly, optional: true)
        .AddEnvironmentVariables()
        .Build();

    /// <exception cref="InvalidOperationException">No key is configured; the message says how to set one.</exception>
    public static string AnthropicApiKey(IConfiguration configuration) =>
        configuration[AnthropicApiKeyName] is { Length: > 0 } key
            ? key
            : throw new InvalidOperationException(
                $"{EnvironmentVariable}=1 runs the evals against a real Claude model, which needs an Anthropic API key. " +
                $"Set the {AnthropicApiKeyName} environment variable, or run " +
                $"'dotnet user-secrets set {AnthropicApiKeyName} <key> --project tests/ShoppingAssistant.Evals'.");
}
