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
    [DataRow("OpenAI")]
    [DataRow("1")]
    public void UnknownChatProviderFailsAtStartupNamingTheSetting(string chatProvider)
    {
        var builder = CreateBuilder(new() { ["ChatProvider"] = chatProvider });

        var exception = Assert.ThrowsExactly<InvalidOperationException>(builder.AddChatProvider);

        Assert.Contains("ChatProvider", exception.Message);
        Assert.Contains($"'{chatProvider}'", exception.Message);
        Assert.Contains("None, Ollama, Foundry", exception.Message);
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
