using Microsoft.Extensions.AI;

namespace ShoppingAssistant.Evals.Running;

/// <summary>
/// The caching keys added to Reporting's key, which leaves out the tool definitions and the files a case is read
/// from. Each input that can change an answer must change the key, or a run is scored on a stale answer.
/// </summary>
public sealed class CachingKeysTests
{
    private const string CaseFile = """
        { "cases": [
          { "id": "returns-gps-watch-last-order", "turns": ["what's the return window for my last order?"] },
          { "id": "shipping-cost", "turns": ["how much is shipping?"] }
        ] }
        """;

    private const string WorldFile = """{ "now": "2026-11-01T12:00:00Z", "buyers": [] }""";

    private static IReadOnlyList<AITool> Tools(string searchPoliciesDescription = "Searches the AdventureWorks Policies.") =>
    [
        AIFunctionFactory.Create((string query) => query, "search_policies", searchPoliciesDescription),
        AIFunctionFactory.Create(() => "[]", "list_my_orders", "Lists the signed-in Buyer's orders, newest first."),
    ];

    private static IReadOnlyList<string> Keys(
        IReadOnlyList<AITool>? tools = null, string caseFile = CaseFile, string caseId = "returns-gps-watch-last-order", string worldFile = WorldFile) =>
        CachingKeys.ForCase(tools ?? Tools(), caseFile, caseId, worldFile);

    [Fact]
    public void TheSameToolsCaseAndWorldGiveTheSameKeys()
    {
        Assert.Equal(Keys(), Keys());
    }

    [Fact]
    public void ChangingOneToolsDescriptionChangesTheKeys()
    {
        Assert.NotEqual(Keys(), Keys(tools: Tools(searchPoliciesDescription: "Searches the Policies; cite each rule.")));
    }

    [Fact]
    public void ChangingOneToolsParametersChangesTheKeys()
    {
        IReadOnlyList<AITool> renamedParameter =
        [
            AIFunctionFactory.Create((string question) => question, "search_policies", "Searches the AdventureWorks Policies."),
            Tools()[1],
        ];

        Assert.NotEqual(Keys(), Keys(tools: renamedParameter));
    }

    [Fact]
    public void ChangingTheCaseChangesItsKeys()
    {
        var edited = CaseFile.Replace("my last order?", "my most recent order?", StringComparison.Ordinal);

        Assert.NotEqual(Keys(), Keys(caseFile: edited));
    }

    [Fact]
    public void ChangingAnotherCaseLeavesThisCasesKeysAlone()
    {
        var edited = CaseFile.Replace("how much is shipping?", "what does shipping cost?", StringComparison.Ordinal);

        Assert.Equal(Keys(), Keys(caseFile: edited));
    }

    [Fact]
    public void ChangingTheWorldChangesTheKeys()
    {
        var edited = WorldFile.Replace("2026-11-01", "2026-11-02", StringComparison.Ordinal);

        Assert.NotEqual(Keys(), Keys(worldFile: edited));
    }
}
