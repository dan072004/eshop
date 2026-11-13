using System.Globalization;
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

    public const string CaseVariable = "ESHOP_EVALS_CASE";

    public const string ParallelismVariable = "ESHOP_EVALS_PARALLELISM";

    private const int DefaultParallelism = 4;

    /// <summary>
    /// The case ids in <c>ESHOP_EVALS_CASE</c> (comma-separated), or null to run every case. A filtered run writes the
    /// report and the summary, and skips the gate.
    /// </summary>
    public static IReadOnlyList<string>? CaseFilter(string? value) =>
        value?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) is { Length: > 0 } ids ? ids : null;

    /// <summary>How many cases run at once: <c>ESHOP_EVALS_PARALLELISM</c>, or 4.</summary>
    /// <exception cref="InvalidOperationException">The value is not a positive whole number.</exception>
    public static int Parallelism(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DefaultParallelism
        : int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parallelism) && parallelism > 0 ? parallelism
        : throw new InvalidOperationException($"{ParallelismVariable} is '{value}'; it must be a positive whole number.");

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
