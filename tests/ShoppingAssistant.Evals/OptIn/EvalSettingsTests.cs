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

    [Fact]
    public void TheCaseFilterIsTheCommaSeparatedIdsAndNoFilterRunsEveryCase()
    {
        Assert.Equal(["returns-window", "bob-order-asked-directly"], EvalSettings.CaseFilter(" returns-window, bob-order-asked-directly ,"));
        Assert.Null(EvalSettings.CaseFilter(""));
    }

    [Fact]
    public void ParallelismDefaultsToFourAndRejectsANonPositiveNumber()
    {
        Assert.Equal(4, EvalSettings.Parallelism(null));
        Assert.Equal(2, EvalSettings.Parallelism("2"));
        var error = Assert.Throws<InvalidOperationException>(() => EvalSettings.Parallelism("0"));
        Assert.Equal("ESHOP_EVALS_PARALLELISM is '0'; it must be a positive whole number.", error.Message);
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
