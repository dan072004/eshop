namespace eShop.Policy.UnitTests;

[TestClass]
public class PolicySplitterTests
{
    [TestMethod]
    public void FrontMatterSuppliesTitleCategoryAndLastUpdated()
    {
        var policy = PolicySplitter.Split("sizing-ski-snowboard.md", """
            ---
            title: Sizing Guide: Skis and Snowboards
            category: sizing
            last_updated: 2026-10-28
            ---

            # Sizing Guide: Skis and Snowboards

            ## Ski length
            Pick by height.
            """);

        Assert.AreEqual("sizing-ski-snowboard", policy.Slug);
        Assert.AreEqual("Sizing Guide: Skis and Snowboards", policy.Title);
        Assert.AreEqual("sizing", policy.Category);
        Assert.AreEqual("2026-10-28", policy.LastUpdated);
    }

    [TestMethod]
    public void MissingTitleFailsNamingTheFile()
    {
        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => PolicySplitter.Split("exchanges.md", """
            ---
            category: returns
            ---

            # Exchanges

            ## Sizes
            Swap for another size.
            """));

        Assert.Contains("'exchanges.md'", exception.Message);
        Assert.Contains("title", exception.Message);
    }

    [TestMethod]
    public void EachLevelTwoHeadingStartsAPolicySection()
    {
        var policy = PolicySplitter.Split("returns-exceptions.md", """
            ---
            title: Returns Exceptions
            ---

            # Returns Exceptions

            ## Helmets
            Unworn helmets can be returned.

            ## Goggles
            Goggles can be returned if the lens film is intact.
            """);

        Assert.HasCount(2, policy.Sections);
        var goggles = policy.Sections[1];
        Assert.AreEqual(1, goggles.Position);
        Assert.AreEqual("Goggles", goggles.Heading);
        Assert.AreEqual("Goggles can be returned if the lens film is intact.", goggles.Body);
        Assert.AreEqual("Returns Exceptions › Goggles", goggles.Citation);
        Assert.AreEqual("Returns Exceptions › Goggles\n\nGoggles can be returned if the lens film is intact.", goggles.IndexedText);
    }

    [TestMethod]
    public void TextBeforeTheFirstLevelTwoHeadingIsAnIntroSectionHeadedAndCitedByThePolicyTitle()
    {
        var policy = PolicySplitter.Split("returns-exceptions.md", """
            ---
            title: Returns Exceptions
            ---

            # Returns Exceptions

            Some items have stricter return rules.

            ## Helmets
            Unworn helmets can be returned.
            """);

        CollectionAssert.AreEqual(new[] { "Returns Exceptions", "Helmets" }, policy.Sections.Select(s => s.Heading).ToArray());
        var intro = policy.Sections[0];
        Assert.AreEqual(0, intro.Position);
        Assert.AreEqual("Some items have stricter return rules.", intro.Body);
        Assert.AreEqual("Returns Exceptions", intro.Citation);
        Assert.AreEqual(1, policy.Sections[1].Position);
    }

    [TestMethod]
    public void BlankTextBeforeTheFirstLevelTwoHeadingMakesNoIntroSection()
    {
        var policy = PolicySplitter.Split("exchanges.md", """
            ---
            title: Exchanges
            ---

            # Exchanges


            ## Sizes
            Swap for another size.
            """);

        Assert.AreEqual("Sizes", policy.Sections.Single().Heading);
        Assert.AreEqual(0, policy.Sections.Single().Position);
    }

    [TestMethod]
    public void LevelThreeHeadingsAndTablesStayInsideTheirSection()
    {
        var policy = PolicySplitter.Split("sizing-ski-snowboard.md", """
            ---
            title: Sizing Guide: Skis and Snowboards
            ---

            # Sizing Guide: Skis and Snowboards

            ## Snowboard length by weight
            Weight matters more than height.

            ### Wide boards
            | Boot size | Board |
            |---|---|
            | 11+ | Wide |

            ## Bindings
            Match bindings to boot size.
            """);

        CollectionAssert.AreEqual(new[] { "Snowboard length by weight", "Bindings" }, policy.Sections.Select(s => s.Heading).ToArray());
        Assert.AreEqual(
            "Weight matters more than height.\n\n### Wide boards\n| Boot size | Board |\n|---|---|\n| 11+ | Wide |",
            policy.Sections[0].Body);
    }

    [TestMethod]
    public void APolicyWithNoLevelTwoHeadingsIsOneSectionCitedByItsTitle()
    {
        var policy = PolicySplitter.Split("order-status.md", """
            ---
            title: Order Status and Tracking
            ---

            # Order Status and Tracking

            Orders move from submitted to shipped.

            ### Tracking
            A tracking link is emailed when it ships.
            """);

        var section = policy.Sections.Single();
        Assert.AreEqual("Order Status and Tracking", section.Heading);
        Assert.AreEqual("Order Status and Tracking", section.Citation);
        Assert.AreEqual("order-status-and-tracking", section.Anchor);
        Assert.AreEqual("Orders move from submitted to shipped.\n\n### Tracking\nA tracking link is emailed when it ships.", section.Body);
    }

    [TestMethod]
    [DataRow("GPS watches, navigators and bike computers", "gps-watches-navigators-and-bike-computers")]
    [DataRow("Sizing Guide: Skis and Snowboards", "sizing-guide-skis-and-snowboards")]
    [DataRow("Return window", "return-window")]
    [DataRow("Crash Replacement (helmets) & 30-day returns", "crash-replacement-helmets-30-day-returns")]
    public void AnchorIsASlugOfTheHeading(string heading, string expectedAnchor)
    {
        var policy = PolicySplitter.Split("p.md", $"""
            ---
            title: P
            ---

            # P

            ## {heading}
            Body.
            """);

        Assert.AreEqual(expectedAnchor, policy.Sections.Single().Anchor);
    }
}
