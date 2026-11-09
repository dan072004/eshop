namespace ShoppingAssistant.Evals.Cases;

public sealed class CaseFileTests
{
    [Fact]
    public void EveryCaseInTheCaseFileLoadsWithAUniqueKebabCaseId()
    {
        var ids = EvalCase.LoadAll().Select(c => c.Id).ToList();

        Assert.NotEmpty(ids);
        Assert.Equal(ids.Distinct(), ids);
        Assert.All(ids, id => Assert.Matches("^[a-z0-9]+(-[a-z0-9]+)*$", id));
    }

    [Fact]
    public void ACaseMissingAFieldFailsToLoadSoItCannotSilentlyLoseACheck()
    {
        const string WithoutMustNotContain = """
            { "cases": [ {
              "id": "returns-gps-watch-last-order", "category": "order", "signedInAs": "alice",
              "turns": ["what's the return window for my last order?"], "catalogResults": null,
              "mustCallTools": [], "mustNotCallTools": [], "mustCite": [], "mustContain": [],
              "reference": "", "rubric": []
            } ] }
            """;

        var error = Assert.Throws<JsonException>(() => EvalCase.Parse(WithoutMustNotContain));

        Assert.Contains("mustNotContain", error.Message);
    }
}
