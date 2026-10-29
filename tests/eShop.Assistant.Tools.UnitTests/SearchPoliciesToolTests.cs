using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace eShop.Assistant.Tools.UnitTests;

[TestClass]
public class SearchPoliciesToolTests
{
    // What policy-api returns from GET /api/policies/search (PolicySearchResponse, web JSON casing).
    private const string PolicySearchResponse = """
        {
          "mode": "keyword",
          "results": [
            {
              "citation": "Returns Exceptions › GPS watches, navigators and bike computers",
              "policySlug": "returns-exceptions",
              "policyTitle": "Returns Exceptions",
              "section": "GPS watches, navigators and bike computers",
              "anchor": "gps-watches-navigators-and-bike-computers",
              "text": "These items have a 30-day return window.",
              "score": 0.42
            },
            {
              "citation": "Returns Policy",
              "policySlug": "returns-policy",
              "policyTitle": "Returns Policy",
              "section": "Returns Policy",
              "anchor": "returns-policy",
              "text": "We want you to love your gear.",
              "score": 0.1
            }
          ]
        }
        """;

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

        Assert.AreEqual("keyword", result.GetProperty("mode").GetString());
        var results = result.GetProperty("results").EnumerateArray().ToArray();
        Assert.HasCount(2, results);
        var first = results[0];
        Assert.AreEqual("Returns Exceptions › GPS watches, navigators and bike computers", first.GetProperty("citation").GetString());
        Assert.AreEqual("returns-exceptions", first.GetProperty("policySlug").GetString());
        Assert.AreEqual("Returns Exceptions", first.GetProperty("policyTitle").GetString());
        Assert.AreEqual("GPS watches, navigators and bike computers", first.GetProperty("section").GetString());
        Assert.AreEqual("gps-watches-navigators-and-bike-computers", first.GetProperty("anchor").GetString());
        Assert.AreEqual("These items have a 30-day return window.", first.GetProperty("text").GetString());
        Assert.AreEqual(0.42, first.GetProperty("score").GetDouble());
        Assert.AreEqual("Returns Policy", results[1].GetProperty("citation").GetString());
    }

    [TestMethod]
    public async Task SearchPoliciesPassesTheSemanticModeThrough()
    {
        var policy = new FakeHandler(_ => Json(HttpStatusCode.OK, """{ "mode": "semantic", "results": [] }"""));
        var tool = BuildTools(policy).SearchPolicies;

        var result = await InvokeAsync(tool, new() { ["question"] = "how much is shipping to Canada?" });

        Assert.AreEqual("semantic", result.GetProperty("mode").GetString());
        Assert.AreEqual(0, result.GetProperty("results").GetArrayLength());
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
    public void SearchPoliciesHasTheContractNameParameterAndCitationRule()
    {
        var tools = BuildTools(new FakeHandler(_ => Json(HttpStatusCode.OK, PolicySearchResponse)));
        var tool = tools.SearchPolicies;

        Assert.AreEqual("search_policies", tool.Name);
        var properties = tool.JsonSchema.GetProperty("properties");
        Assert.AreEqual("question", properties.EnumerateObject().Single().Name);
        Assert.AreEqual("question", tool.JsonSchema.GetProperty("required").EnumerateArray().Single().GetString());
        Assert.AreEqual(
            "AdventureWorks Policies on returns, exchanges, shipping, warranty, sizing, payments and orders. " +
            "State Policy rules only from these results; cite each rule with its citation value; " +
            "if no result answers the question, say so.",
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

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }
}
