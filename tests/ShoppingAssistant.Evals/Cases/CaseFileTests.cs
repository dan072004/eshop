using eShop.Policy.API.Model;
using ShoppingAssistant.Evals.World;

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
    public void TheCaseFileHasFortyCasesSplitFifteenTenFiveFiveFiveEachWithAReferenceAndARubric()
    {
        var cases = EvalCase.LoadAll();

        Assert.Equal(
            [(CaseCategory.Policy, 15), (CaseCategory.Order, 10), (CaseCategory.Catalog, 5), (CaseCategory.OutOfScope, 5), (CaseCategory.Adversarial, 5)],
            cases.GroupBy(c => c.Category).OrderBy(g => g.Key).Select(g => (g.Key, g.Count())));
        Assert.Empty(cases.Where(c => string.IsNullOrWhiteSpace(c.Reference) || c.Rubric.Count == 0).Select(c => c.Id));
    }

    [Fact]
    public void EveryPolicyFileHasExactlyOnePolicyCaseAndThatCaseCitesOnlyItsPolicy()
    {
        var titles = Policies().Select(p => p.Title).Order(StringComparer.Ordinal).ToList();

        var citedByPolicyCases = EvalCase.LoadAll()
            .Where(c => c.Category == CaseCategory.Policy)
            .Select(c => (c.Id, Titles: c.MustCite.Select(TitleOf).Distinct().ToList()))
            .ToList();

        Assert.Empty(citedByPolicyCases.Where(c => c.Titles.Count != 1).Select(c => c.Id));
        Assert.Equal(titles, citedByPolicyCases.Select(c => c.Titles.Single()).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void EveryMustCiteIsTheCitationOfARealPolicySection()
    {
        var citations = Policies().SelectMany(p => p.Sections).Select(s => s.Citation).ToHashSet(StringComparer.Ordinal);

        var unknown = EvalCase.LoadAll()
            .SelectMany(c => c.MustCite.Where(cite => !citations.Contains(cite)).Select(cite => $"{c.Id}: {cite}"));

        Assert.Empty(unknown);
    }

    [Fact]
    public void EveryAdversarialCaseForbidsTheOtherBuyersOrderNumbersAndProductsItsQuestionDoesNotName()
    {
        var world = EvalWorld.Load();

        var gaps = EvalCase.LoadAll()
            .Where(c => c.Category == CaseCategory.Adversarial)
            .SelectMany(c => world.Orders
                .Where(o => o.Buyer != c.SignedInAs)
                .SelectMany(o => o.Items.Select(i => world.Product(i.ProductId).Name).Prepend($"{o.OrderNumber}"))
                .Where(secret => !c.Turns.Any(t => t.Contains(secret, StringComparison.Ordinal)) && !c.MustNotContain.Contains(secret))
                .Select(secret => $"{c.Id}: {secret}"));

        Assert.Empty(gaps);
    }

    [Fact]
    public void EveryOutOfScopeCaseIsGradedOnDecliningPolitelyAndOfferingAdventureWorksHelp()
    {
        var withoutIt = EvalCase.LoadAll()
            .Where(c => c.Category == CaseCategory.OutOfScope && !c.Rubric.Contains("Declines politely and offers AdventureWorks help"))
            .Select(c => c.Id);

        Assert.Empty(withoutIt);
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

    private static string TitleOf(string citation) => citation.Split(" › ")[0];

    // The Policy files as policy-api indexes them: the copy of policies/ in the build output.
    private static List<ParsedPolicy> Policies() =>
        [.. Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "policies"), "*.md")
            .Select(path => PolicySplitter.Split(Path.GetFileName(path), File.ReadAllText(path)))];
}
