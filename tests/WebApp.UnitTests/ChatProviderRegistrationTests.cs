using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace eShop.WebApp.UnitTests;

[TestClass]
public class ChatProviderRegistrationTests
{
    [TestMethod]
    [DataRow(null, DisplayName = "missing")]
    [DataRow("", DisplayName = "empty")]
    [DataRow("None")]
    [DataRow("none")]
    public void NoChatProviderRegistersNoChatClient(string? chatProvider)
    {
        using var services = BuildServices(new() { ["ChatProvider"] = chatProvider });

        Assert.IsNull(services.GetService<IChatClient>());
    }

    [TestMethod]
    [DataRow("Ollama")]
    [DataRow("ollama")]
    public void OllamaChatProviderResolvesAChatClientWithFunctionInvocation(string chatProvider)
    {
        using var services = BuildServices(new()
        {
            ["ChatProvider"] = chatProvider,
            ["ConnectionStrings:chat"] = "Endpoint=http://localhost:11434;Model=llama3.1"
        });

        var client = services.GetRequiredService<IChatClient>();

        Assert.IsNotNull(client.GetService<FunctionInvokingChatClient>());
        var metadata = client.GetService<ChatClientMetadata>();
        Assert.IsNotNull(metadata);
        Assert.AreEqual("llama3.1", metadata.DefaultModelId);
    }

    [TestMethod]
    [DataRow("Foundry")]
    [DataRow("FOUNDRY")]
    public void FoundryChatProviderResolvesAChatClientWithFunctionInvocation(string chatProvider)
    {
        using var services = BuildServices(new()
        {
            ["ChatProvider"] = chatProvider,
            ["ConnectionStrings:chatModel"] = "Endpoint=https://eshop-test.openai.azure.com/;Key=not-a-real-key;Deployment=gpt-4.1-mini"
        });

        var client = services.GetRequiredService<IChatClient>();

        Assert.IsNotNull(client.GetService<FunctionInvokingChatClient>());
        var metadata = client.GetService<ChatClientMetadata>();
        Assert.IsNotNull(metadata);
        Assert.AreEqual("gpt-4.1-mini", metadata.DefaultModelId);
    }

    [TestMethod]
    [DataRow("Claude")]
    [DataRow("claude")]
    public void ClaudeChatProviderResolvesAnAnthropicChatClientWithFunctionInvocationOnHaiku(string chatProvider)
    {
        using var services = BuildServices(new()
        {
            ["ChatProvider"] = chatProvider,
            ["ANTHROPIC_API_KEY"] = "not-a-real-key"
        });

        var client = services.GetRequiredService<IChatClient>();

        Assert.IsNotNull(client.GetService<FunctionInvokingChatClient>());
        var metadata = client.GetService<ChatClientMetadata>();
        Assert.IsNotNull(metadata);
        Assert.AreEqual("anthropic", metadata.ProviderName);
        Assert.AreEqual("claude-haiku-4-5", metadata.DefaultModelId);
    }

    [TestMethod]
    public void ClaudeChatProviderHonoursAnOverriddenModelId()
    {
        using var services = BuildServices(new()
        {
            ["ChatProvider"] = "Claude",
            ["ANTHROPIC_API_KEY"] = "not-a-real-key",
            ["Claude:ModelId"] = "claude-sonnet-4-5"
        });

        var metadata = services.GetRequiredService<IChatClient>().GetService<ChatClientMetadata>();

        Assert.IsNotNull(metadata);
        Assert.AreEqual("claude-sonnet-4-5", metadata.DefaultModelId);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "missing")]
    [DataRow("", DisplayName = "empty")]
    [DataRow(" ", DisplayName = "whitespace")]
    public void ClaudeChatProviderWithoutAnApiKeyFailsAtStartupNamingTheSetting(string? apiKey)
    {
        var builder = CreateBuilder(new() { ["ChatProvider"] = "Claude", ["ANTHROPIC_API_KEY"] = apiKey });

        var exception = Assert.ThrowsExactly<InvalidOperationException>(builder.AddChatProvider);

        Assert.Contains("ANTHROPIC_API_KEY", exception.Message);
    }

    [TestMethod]
    [DataRow("lots", DisplayName = "non-numeric")]
    [DataRow("0", DisplayName = "zero")]
    [DataRow("-1", DisplayName = "negative")]
    public void ClaudeChatProviderWithAnInvalidMaxOutputTokensFailsAtStartupNamingTheSetting(string maxOutputTokens)
    {
        var builder = CreateBuilder(new()
        {
            ["ChatProvider"] = "Claude",
            ["ANTHROPIC_API_KEY"] = "not-a-real-key",
            ["Claude:MaxOutputTokens"] = maxOutputTokens
        });

        var exception = Assert.ThrowsExactly<InvalidOperationException>(builder.AddChatProvider);

        Assert.Contains("Claude:MaxOutputTokens", exception.Message);
        Assert.Contains($"'{maxOutputTokens}'", exception.Message);
    }

    [TestMethod]
    [DataRow("OpenAI")]
    [DataRow("1")]
    public void UnknownChatProviderFailsAtStartupNamingTheSetting(string chatProvider)
    {
        var builder = CreateBuilder(new() { ["ChatProvider"] = chatProvider });

        var exception = Assert.ThrowsExactly<InvalidOperationException>(builder.AddChatProvider);

        Assert.Contains("ChatProvider", exception.Message);
        Assert.Contains($"'{chatProvider}'", exception.Message);
        Assert.EndsWith("Allowed values: None, Ollama, Foundry, Claude.", exception.Message);
    }

    private static ServiceProvider BuildServices(Dictionary<string, string?> configuration)
    {
        var builder = CreateBuilder(configuration);
        builder.AddChatProvider();
        return builder.Services.BuildServiceProvider();
    }

    private static HostApplicationBuilder CreateBuilder(Dictionary<string, string?> configuration)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(configuration);
        return builder;
    }
}
