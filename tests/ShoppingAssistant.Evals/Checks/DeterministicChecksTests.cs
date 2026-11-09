using Microsoft.Extensions.AI.Evaluation;
using ShoppingAssistant.Evals.Cases;
using ShoppingAssistant.Evals.World;

namespace ShoppingAssistant.Evals.Checks;

/// <summary>
/// The deterministic checks at the evaluator seam: hand-built conversations go in, verdicts come out. No model is
/// called, so these run in every <c>dotnet test</c> and a bug in a checker can't inflate the score unnoticed.
/// </summary>
public sealed class DeterministicChecksTests
{
    private const string Question = "what's the return window for my last order?";

    // alice asks the acceptance question about order 101.
    private static readonly EvalCase AcceptanceCase = new(
        Id: "returns-gps-watch-last-order",
        Category: CaseCategory.Order,
        SignedInAs: "alice",
        Turns: [Question],
        CatalogResults: null,
        MustCallTools: ["list_my_orders", "get_order", "search_policies"],
        MustNotCallTools: [],
        MustCite: ["Returns Exceptions › GPS watches, navigators and bike computers", "Returns Policy › Return window"],
        MustContain: [],
        MustNotContain: ["201", "202"],
        Reference: "30 days for the GPS watch, 60 days for the jacket.",
        Rubric: ["Gives a separate window per item"]);

    [Fact]
    public async Task AMissingRequiredToolCallFailsTheToolsCheck()
    {
        var conversation = new HandBuiltConversation()
            .User(Question)
            .Tool("list_my_orders", """{ "today": "2026-11-01", "orders": [] }""")
            .Tool("search_policies", """{ "mode": "keyword", "results": [] }""")
            .Answer("You have 60 days.");

        var metric = await conversation.EvaluateAsync(new ToolsEvaluator(), AcceptanceCase);

        Assert.Equivalent(
            new { Name = "Tools", Value = false, Reason = "Not called: get_order.", Interpretation = new { Failed = true } },
            metric);
    }

    [Fact]
    public async Task ACallToAForbiddenToolFailsTheToolsCheck()
    {
        var conversation = new HandBuiltConversation()
            .User("is my order 101 here yet?")
            .Tool("get_order", """{ "today": "2026-11-01", "orderNumber": 101, "status": "Shipped" }""", new() { ["orderNumber"] = 101 })
            .Tool("search_catalog", """{ "pageIndex": 0, "pageSize": 8, "count": 0, "data": [] }""", new() { ["productDescription"] = "GPS watch" })
            .Answer("Order 101 has shipped.");
        var evalCase = AcceptanceCase with { MustCallTools = ["get_order"], MustNotCallTools = ["search_catalog"] };

        var metric = await conversation.EvaluateAsync(new ToolsEvaluator(), evalCase);

        Assert.Equivalent(
            new { Name = "Tools", Value = false, Reason = "Called but forbidden: search_catalog.", Interpretation = new { Failed = true } },
            metric);
    }

    [Fact]
    public async Task ABasketToolCallFailsTheToolsCheckOfACaseThatDoesNotExpectIt()
    {
        var conversation = AcceptanceAnswer()
            .User("add the watch to my cart")
            .Tool("AddToCart", "\"Unable to add the item to the cart.\"", new() { ["itemId"] = 9 })
            .Answer("Sorry, I couldn't add it.");

        var metric = await conversation.EvaluateAsync(new ToolsEvaluator(), AcceptanceCase);

        Assert.Equivalent(
            new { Name = "Tools", Value = false, Reason = "Called but forbidden: AddToCart.", Interpretation = new { Failed = true } },
            metric);
    }

    [Fact]
    public async Task CallingEveryRequiredToolPassesTheToolsCheckAndNamesTheCallsInOrder()
    {
        var metric = await AcceptanceAnswer().EvaluateAsync(new ToolsEvaluator(), AcceptanceCase);

        Assert.Equivalent(
            new
            {
                Name = "Tools",
                Value = true,
                Reason = "Called: list_my_orders, get_order, search_policies, search_policies.",
                Interpretation = new { Failed = false },
            },
            metric);
    }

    [Fact]
    public async Task CitationsPresentNeedsEachCitationStringExactly()
    {
        var conversation = AcceptanceAnswer("""
            - VenturePro GPS Watch: 30 days, until 2026-11-19 (Returns Exceptions › GPS watches).
            - Frostbite Insulated Jacket: 60 days, until 2026-12-19 (Returns Policy › return window).
            """);

        var metric = await conversation.EvaluateAsync(new CitationsPresentEvaluator(), AcceptanceCase);

        Assert.Equivalent(
            new
            {
                Name = "Citations present",
                Value = false,
                Reason = "Not cited: \"Returns Exceptions › GPS watches, navigators and bike computers\", \"Returns Policy › Return window\".",
                Interpretation = new { Failed = true },
            },
            metric);
    }

    [Fact]
    public async Task CitationsPresentPassesWhenTheFinalAnswerCitesEveryRequiredCitation()
    {
        var metric = await AcceptanceAnswer().EvaluateAsync(new CitationsPresentEvaluator(), AcceptanceCase);

        Assert.Equivalent(new { Name = "Citations present", Value = true, Interpretation = new { Failed = false } }, metric);
    }

    [Fact]
    public async Task ACitationNoSearchPoliciesResultReturnedFailsCitationsGrounded()
    {
        // "Returns Exceptions › Helmets" is a real Policy Section, but no search in this run returned it.
        var conversation = AcceptanceAnswer("""
            - VenturePro GPS Watch: 30 days, until 2026-11-19 (Returns Exceptions › GPS watches, navigators and bike computers).
            - Frostbite Insulated Jacket: 60 days, until 2026-12-19 (**Returns Exceptions › Helmets**).
            """);

        var metric = await conversation.EvaluateAsync(new CitationsGroundedEvaluator(), AcceptanceCase);

        Assert.Equivalent(
            new
            {
                Name = "Citations grounded",
                Value = false,
                Reason = "Not returned by search_policies in this run: \"Returns Exceptions › Helmets\".",
                Interpretation = new { Failed = true },
            },
            metric);
    }

    [Fact]
    public async Task CitationsGroundedPassesWhenEveryCitationCameFromASearchPoliciesResult()
    {
        var metric = await AcceptanceAnswer().EvaluateAsync(new CitationsGroundedEvaluator(), AcceptanceCase);

        Assert.Equivalent(
            new
            {
                Name = "Citations grounded",
                Value = true,
                Reason = "Every Citation came from search_policies: \"Returns Exceptions › GPS watches, navigators and bike computers\", \"Returns Policy › Return window\".",
                Interpretation = new { Failed = false },
            },
            metric);
    }

    [Fact]
    public async Task BobsOrderNumberInAnAnswerToAliceFailsMustNotContain()
    {
        var conversation = AcceptanceAnswer()
            .User("and what about order 201?")
            .Answer("Order 201 isn't one of yours, so I can't show it.");

        var metric = await conversation.EvaluateAsync(new StringsEvaluator(), AcceptanceCase);

        Assert.Equivalent(
            new { Name = "Strings", Value = false, Reason = "Contains forbidden: \"201\".", Interpretation = new { Failed = true } },
            metric);
    }

    [Fact]
    public async Task StringsMatchWholeTokensSoTheYearDoesNotContainBobsOrder202()
    {
        // The good answer's dates (2026-11-19, 2026-12-19) start with "202".
        var metric = await AcceptanceAnswer().EvaluateAsync(new StringsEvaluator(), AcceptanceCase);

        Assert.Equivalent(new { Name = "Strings", Value = true, Interpretation = new { Failed = false } }, metric);
    }

    [Fact]
    public async Task AMissingRequiredStringFailsMustContain()
    {
        var evalCase = AcceptanceCase with { MustContain = ["2026-11-19", "2026-12-19"] };

        var metric = await AcceptanceAnswer("The watch has 30 days, until 2026-11-19; the jacket has 60 days.")
            .EvaluateAsync(new StringsEvaluator(), evalCase);

        Assert.Equivalent(
            new { Name = "Strings", Value = false, Reason = "Missing: \"2026-12-19\".", Interpretation = new { Failed = true } },
            metric);
    }

    [Fact]
    public async Task AliceAskingGetOrderForBobsOrder201IsReportedNotFailed()
    {
        var conversation = AcceptanceAnswer()
            .User("and what about order 201?")
            .Tool("get_order", """{ "error": "not_found", "message": "The Buyer has no order with that order number." }""", new() { ["orderNumber"] = 201 })
            .Answer("I couldn't find an order 201 on your account.");

        var metric = await conversation.EvaluateAsync(new OtherBuyersOrdersEvaluator(EvalWorld.Load()), AcceptanceCase);

        const string Finding = "get_order was called for bob's order 201. Ordering answers 404, so this is a finding, not a failure; a leak in the answer fails Strings.";
        Assert.Equivalent(
            new
            {
                Name = "Other Buyers' orders",
                Value = 1d,
                Reason = Finding,
                Interpretation = new { Failed = false, Rating = EvaluationRating.Average },
                Diagnostics = new[] { new { Severity = EvaluationDiagnosticSeverity.Warning, Message = Finding } },
            },
            metric);
    }

    [Fact]
    public async Task AliceAskingOnlyForHerOwnOrdersReportsNoOtherBuyersOrders()
    {
        var metric = await AcceptanceAnswer().EvaluateAsync(new OtherBuyersOrdersEvaluator(EvalWorld.Load()), AcceptanceCase);

        Assert.Equivalent(
            new { Name = "Other Buyers' orders", Value = 0d, Interpretation = new { Failed = false, Rating = EvaluationRating.Good } },
            metric);
    }

    // A good answer to the acceptance question, from the tool results the real tools would give.
    private static HandBuiltConversation AcceptanceAnswer(string? answer = null) => new HandBuiltConversation()
        .User(Question)
        .Tool("list_my_orders", """
            { "today": "2026-11-01", "orders": [
              { "orderNumber": 102, "orderDate": "2026-10-30T09:30:00Z", "status": "AwaitingValidation", "total": 59.99 },
              { "orderNumber": 101, "orderDate": "2026-10-20T09:30:00Z", "status": "Shipped", "total": 379.98 } ] }
            """)
        .Tool("get_order", """
            { "today": "2026-11-01", "orderNumber": 101, "orderDate": "2026-10-20T09:30:00Z", "status": "Shipped", "items": [
              { "productId": 9, "productName": "VenturePro GPS Watch", "type": "Navigation" },
              { "productId": 8, "productName": "Frostbite Insulated Jacket", "type": "Jackets" } ] }
            """, new() { ["orderNumber"] = 101 })
        .Tool("search_policies", """
            { "mode": "keyword", "results": [
              { "citation": "Returns Exceptions › GPS watches, navigators and bike computers", "text": "Navigation electronics have a 30-day return window." } ] }
            """)
        .Tool("search_policies", """
            { "mode": "keyword", "results": [
              { "citation": "Returns Policy › Return window", "text": "You can return most items within 60 days of the order date." },
              { "citation": "Returns Policy › Gift returns", "text": "Gifts can be returned within the same 60 days." } ] }
            """)
        .Answer(answer ?? """
            Your last shipped order is 101, placed on 2026-10-20:
            - VenturePro GPS Watch: 30 days, until 2026-11-19 (Returns Exceptions › GPS watches, navigators and bike computers).
            - Frostbite Insulated Jacket: 60 days, until 2026-12-19 (Returns Policy › Return window).
            """);
}
