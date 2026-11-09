using Microsoft.Extensions.Configuration;

namespace ShoppingAssistant.Evals.OptIn;

public sealed class EvalSettingsTests
{
    [Fact]
    public void TheAnthropicApiKeyComesFromConfiguration()
    {
        var configuration = Configuration(new() { ["ANTHROPIC_API_KEY"] = "sk-ant-test" });

        Assert.Equal("sk-ant-test", EvalSettings.AnthropicApiKey(configuration));
    }

    [Fact]
    public void AMissingAnthropicApiKeyFailsWithAMessageSayingHowToSetIt()
    {
        var configuration = Configuration(new() { ["ANTHROPIC_API_KEY"] = "" });

        var error = Assert.Throws<InvalidOperationException>(() => EvalSettings.AnthropicApiKey(configuration));

        Assert.Equal(
            "ESHOP_EVALS=1 runs the evals against a real Claude model, which needs an Anthropic API key. " +
            "Set the ANTHROPIC_API_KEY environment variable, or run " +
            "'dotnet user-secrets set ANTHROPIC_API_KEY <key> --project tests/ShoppingAssistant.Evals'.",
            error.Message);
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
