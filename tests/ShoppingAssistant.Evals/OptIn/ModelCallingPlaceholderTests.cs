namespace ShoppingAssistant.Evals.OptIn;

/// <summary>
/// Stands in for the model-calling evals until the first one lands: skipped without <c>ESHOP_EVALS=1</c>, and with
/// it, failing unless an Anthropic API key is configured. Delete it when the first real eval replaces it.
/// </summary>
public sealed class ModelCallingPlaceholderTests
{
    [EvalFact]
    public void AModelCallingTestRunsOnlyWithAnAnthropicApiKey()
    {
        var key = EvalSettings.AnthropicApiKey(EvalSettings.Configuration);

        Assert.NotEmpty(key);
    }
}
