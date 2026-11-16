using System.Runtime.CompilerServices;

namespace ShoppingAssistant.Evals.OptIn;

/// <summary>
/// A test that calls a real model. It is reported as skipped unless <c>ESHOP_EVALS=1</c>, so the normal
/// <c>dotnet test</c> stays free and needs no key. Its body gets the key from
/// <see cref="EvalSettings.AnthropicApiKey"/>, which fails the test when none is configured.
/// </summary>
public sealed class EvalFactAttribute : FactAttribute
{
    public EvalFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        SkipType = typeof(EvalSettings);
        SkipUnless = nameof(EvalSettings.IsEnabled);
        Skip = $"Set {EvalSettings.EnvironmentVariable}=1 and {EvalSettings.AnthropicApiKeyName} to run the evals against a real Claude model.";
    }
}
