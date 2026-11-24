using System.Net;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace eShop.Assistant.Tools.UnitTests;

[TestClass]
public class SearchPoliciesToolTests
{
    // What policy-api returns from GET /api/policies/search (PolicySearchResponse; Json serialises it with web casing).
    private static readonly object PolicySearchResponse = new
    {
        Mode = "keyword",
        Results = new[]
        {
            new
            {
                Citation = "Returns Exceptions › GPS watches, navigators and bike computers",
                PolicySlug = "returns-exceptions",
                PolicyTitle = "Returns Exceptions",
                Section = "GPS watches, navigators and bike computers",
                Anchor = "gps-watches-navigators-and-bike-computers",
                Text = "These items have a 30-day return window.",
                Score = 0.42,
            },
            new
            {
                Citation = "Returns Policy",
                PolicySlug = "returns-policy",
                PolicyTitle = "Returns Policy",
                Section = "Returns Policy",
                Anchor = "returns-policy",
                Text = "We want you to love your gear.",
                Score = 0.1,
            },
        },
    };

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task SearchPoliciesAsksForTheTopFiveAndPassesModeAndCitationsThrough()
    {
        var policy = new FakeHandler(_ => Json(HttpStatusCode.OK, PolicySearchResponse));
        var tool = BuildTools(policy).SearchPolicies;

        var result = await InvokeAsync(tool, new() { ["question"] = "can I return a GPS watch after 45 days?" });

        var request = policy.Requests.Single();
        Assert.AreEqual("policy-api", request.RequestUri!.Host);
        Assert.AreEqual("/api/policies/search", request.RequestUri.AbsolutePath);
        var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);
        Assert.AreEqual("can I return a GPS watch after 45 days?", query["q"]);
        Assert.AreEqual("5", query["top"]);
        Assert.IsFalse(request.Headers.Contains("Authorization"));

        Assert.IsTrue(JsonElement.DeepEquals(WireJson(PolicySearchResponse), result), result.GetRawText());
    }

    [TestMethod]
    public async Task SearchPoliciesAddsTheItemTypesToTheSearch()
    {
        var policy = new FakeHandler(_ => Json(HttpStatusCode.OK, PolicySearchResponse));
        var tool = BuildTools(policy).SearchPolicies;

        await InvokeAsync(tool, new()
        {
            ["question"] = "What is the return window for my order?",
            ["itemTypes"] = new[] { "Navigation", "Jackets" },
        });

        var query = System.Web.HttpUtility.ParseQueryString(policy.Requests.Single().RequestUri!.Query);
        Assert.AreEqual("What is the return window for my order? Navigation Jackets", query["q"]);
    }

    [TestMethod]
    public async Task SearchPoliciesPassesTheSemanticModeThrough()
    {
        var semanticResponse = new { Mode = "semantic", Results = Array.Empty<object>() };
        var policy = new FakeHandler(_ => Json(HttpStatusCode.OK, semanticResponse));
        var tool = BuildTools(policy).SearchPolicies;

        var result = await InvokeAsync(tool, new() { ["question"] = "how much is shipping to Canada?" });

        Assert.IsTrue(JsonElement.DeepEquals(WireJson(semanticResponse), result), result.GetRawText());
    }

    [TestMethod]
    public async Task SearchPoliciesReturnsUnavailableWhenPolicyApiFails()
    {
        var policy = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var tool = BuildTools(policy).SearchPolicies;

        var result = await InvokeAsync(tool, new() { ["question"] = "return window" });

        Assert.AreEqual("unavailable", result.GetProperty("error").GetString());
        Assert.AreEqual("Error searching the Policies.", result.GetProperty("message").GetString());
    }

    [TestMethod]
    [DataRow("timeout")]
    [DataRow("invalid-json")]
    public async Task SearchPoliciesReturnsUnavailableForAnyOtherFailure(string failure)
    {
        var policy = new FakeHandler(_ => failure == "timeout"
            ? throw new TaskCanceledException("The request timed out.")
            : Json(HttpStatusCode.OK, "<html>not json</html>"));
        var tool = BuildTools(policy).SearchPolicies;

        var result = await InvokeAsync(tool, new() { ["question"] = "return window" });

        Assert.AreEqual("unavailable", result.GetProperty("error").GetString());
        Assert.AreEqual("Error searching the Policies.", result.GetProperty("message").GetString());
    }

    [TestMethod]
    public async Task SearchPoliciesRethrowsTheCallersOwnCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var policy = new FakeHandler(_ =>
        {
            cancellation.Cancel();
            throw new TaskCanceledException();
        });
        var tool = BuildTools(policy).SearchPolicies;

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await tool.InvokeAsync(new() { ["question"] = "return window" }, cancellation.Token));
    }

    [TestMethod]
    public void SearchPoliciesHasTheContractNameParametersAndCitationRule()
    {
        var tools = BuildTools(new FakeHandler(_ => Json(HttpStatusCode.OK, PolicySearchResponse)));
        var tool = tools.SearchPolicies;

        Assert.AreEqual("search_policies", tool.Name);
        var properties = tool.JsonSchema.GetProperty("properties");
        const string expectedProperties = """
            {
              "question": { "description": "The Buyer's question about an AdventureWorks Policy", "type": "string" },
              "itemTypes": {
                "description": "The catalog types of the order's items, as get_order returns them",
                "type": ["array", "null"],
                "items": { "type": ["string", "null"] },
                "default": null
              }
            }
            """;
        Assert.IsTrue(JsonElement.DeepEquals(JsonDocument.Parse(expectedProperties).RootElement, properties), properties.GetRawText());
        Assert.AreEqual("question", tool.JsonSchema.GetProperty("required").EnumerateArray().Single().GetString());
        Assert.AreEqual(
            "AdventureWorks Policies on returns, exchanges, shipping, warranty, sizing, payments and orders. " +
            "State Policy rules only from these results; cite each rule with its citation value; " +
            "if no result answers the question, say so. " +
            "For a question about a specific order, pass its items' catalog types as itemTypes.",
            tool.Description);
        CollectionAssert.Contains(tools.All.ToArray(), tool);
    }

    private static AssistantTools BuildTools(FakeHandler policy)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddAssistantTools().PolicyClient.ConfigurePrimaryHttpMessageHandler(() => policy);
        return services.BuildServiceProvider().GetRequiredService<AssistantTools>();
    }

    private async Task<JsonElement> InvokeAsync(AIFunction tool, AIFunctionArguments arguments)
    {
        var result = await tool.InvokeAsync(arguments, TestContext.CancellationToken);
        return result is JsonElement element ? element : JsonSerializer.SerializeToElement(result);
    }
}
