namespace eShop.Policy.UnitTests;

// Reads the Policy files that ship with policy-api, so a rewritten Policy can't break Citations unnoticed.
[TestClass]
public class RealPoliciesTests
{
    private static readonly string PoliciesFolder = Path.Combine(AppContext.BaseDirectory, "policies");

    private static ParsedPolicy Load(string slug) =>
        PolicySplitter.Split($"{slug}.md", File.ReadAllText(Path.Combine(PoliciesFolder, $"{slug}.md")));

    [TestMethod]
    public void ReturnsExceptionsCitesTheThirtyDayNavigationElectronicsRule()
    {
        var policy = Load("returns-exceptions");

        var section = policy.Sections.Single(s => s.Heading == "GPS watches, navigators and bike computers");
        Assert.Contains("30-day", section.Body);
        Assert.AreEqual("Returns Exceptions › GPS watches, navigators and bike computers", section.Citation);
        Assert.AreEqual("gps-watches-navigators-and-bike-computers", section.Anchor);
        Assert.AreEqual("returns", policy.Category);
    }

    [TestMethod]
    public void ReturnsExceptionsStartsWithAnIntroThatPointsToTheStandardReturnsPolicy()
    {
        var intro = Load("returns-exceptions").Sections[0];

        Assert.AreEqual("Returns Exceptions", intro.Citation);
        Assert.Contains("60-day", intro.Body);
    }

    [TestMethod]
    public void SkiSizingKeepsItsLengthTableInsideTheSection()
    {
        var policy = Load("sizing-ski-snowboard");

        Assert.AreEqual("Sizing Guide: Skis and Snowboards", policy.Title);
        var section = policy.Sections.Single(s => s.Heading == "Ski length by height and ability");
        Assert.Contains("| 5'8\" (173 cm) | 155–165 cm | 165–170 cm | 170–175 cm |", section.Body);
    }

    [TestMethod]
    public void AllFifteenShippedPolicyFilesHaveATitleSoStartupDoesNotFail()
    {
        var files = Directory.GetFiles(PoliciesFolder, "*.md");

        Assert.HasCount(15, files);
        foreach (var file in files)
        {
            var policy = PolicySplitter.Split(Path.GetFileName(file), File.ReadAllText(file));
            Assert.IsFalse(string.IsNullOrWhiteSpace(policy.Title), file);
        }
    }
}
