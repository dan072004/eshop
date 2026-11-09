using Microsoft.Extensions.AI.Evaluation;
using ShoppingAssistant.Evals.Cases;
using ShoppingAssistant.Evals.Checks;

namespace ShoppingAssistant.Evals.Judge;

/// <summary>
/// The rubric evaluator at the evaluator seam, with the judge's real Anthropic client and only the Anthropic API
/// faked: a conversation goes in, one verdict per rubric item comes out.
/// </summary>
public sealed class RubricEvaluatorTests
{
    private const string WindowPerItem = "Gives a separate Return Window per item: 30 days for the GPS watch and 60 days for the jacket";
    private const string DatesFromOrderDate = "End dates are counted from the Order Date (2026-10-31): November 30, 2026 for the watch and December 30, 2026 for the jacket";

    private static readonly EvalCase AcceptanceCase = new(
        Id: "returns-gps-watch-last-order",
        Category: CaseCategory.Order,
        SignedInAs: "alice",
        Turns: ["what's the return window for my last order?"],
        CatalogResults: null,
        MustCallTools: [],
        MustNotCallTools: [],
        MustCite: [],
        MustContain: [],
        MustNotContain: [],
        Reference: "The GPS watch has 30 days, until November 30, 2026; the jacket has 60 days, until December 30, 2026.",
        Rubric: [WindowPerItem, DatesFromOrderDate]);

    [Fact]
    public async Task TheJudgeGivesEachRubricItemAVerdictWithItsReason()
    {
        var anthropic = new FakeAnthropic(FakeAnthropic.Verdicts(
            (1, "30 days for the watch and 60 for the jacket.", true),
            (2, "The dates are counted from October 20, not the Order Date.", false)));

        var result = await Answer("The watch: 30 days, until November 19. The jacket: 60 days, until December 19.")
            .EvaluateAllAsync(new RubricEvaluator(), AcceptanceCase, JudgeClient.Configuration(anthropic.Client()));

        Assert.Equivalent(
            new object[]
            {
                new { Name = $"Rubric: {WindowPerItem}", Value = true, Reason = "30 days for the watch and 60 for the jacket.", Interpretation = new { Failed = false } },
                new { Name = $"Rubric: {DatesFromOrderDate}", Value = false, Reason = "The dates are counted from October 20, not the Order Date.", Interpretation = new { Failed = true } },
            },
            result.Metrics.Values.ToArray());
    }

    [Fact]
    public async Task TheJudgeIsSonnet55AtLowEffortWithAdaptiveThinkingNotThinkingDisabled()
    {
        var anthropic = new FakeAnthropic(FakeAnthropic.Verdicts((1, "Yes.", true), (2, "Yes.", true)));

        await Answer("The watch: 30 days. The jacket: 60 days.")
            .EvaluateAllAsync(new RubricEvaluator(), AcceptanceCase, JudgeClient.Configuration(anthropic.Client()));

        var request = Assert.Single(anthropic.Requests);
        Assert.Equivalent(
            new { Model = "claude-sonnet-5-5", Thinking = """{"type":"adaptive"}""", Effort = "low", Format = "json_schema", Sampling = false },
            new
            {
                Model = request.GetProperty("model").GetString(),
                Thinking = request.GetProperty("thinking").GetRawText(),
                Effort = request.GetProperty("output_config").GetProperty("effort").GetString(),
                Format = request.GetProperty("output_config").GetProperty("format").GetProperty("type").GetString(),
                Sampling = request.TryGetProperty("temperature", out _) || request.TryGetProperty("top_p", out _) || request.TryGetProperty("top_k", out _),
            });
    }

    [Fact]
    public async Task TheJudgeSeesTheQuestionThePolicySectionsTheReferenceTheAnswerAndTheNumberedRubric()
    {
        var anthropic = new FakeAnthropic(FakeAnthropic.Verdicts((1, "Yes.", true), (2, "Yes.", true)));

        await Answer("The watch: 30 days. The jacket: 60 days.")
            .EvaluateAllAsync(new RubricEvaluator(), AcceptanceCase, JudgeClient.Configuration(anthropic.Client()));

        var prompt = Assert.Single(Assert.Single(anthropic.Requests).GetProperty("messages").EnumerateArray())
            .GetProperty("content").EnumerateArray().Single().GetProperty("text").GetString();
        Assert.Equal(
            """
            <question>
            what's the return window for my last order?
            </question>
            <policy_sections>
            <section citation="Returns Exceptions › GPS watches, navigators and bike computers">
            Navigation electronics have a 30-day return window from the order date, not 60 days.
            </section>
            <section citation="Returns Policy › Return window">
            You can return most items within 60 days of the order date for a full refund.
            </section>
            </policy_sections>
            <reference>
            The GPS watch has 30 days, until November 30, 2026; the jacket has 60 days, until December 30, 2026.
            </reference>
            <answer>
            The watch: 30 days. The jacket: 60 days.
            </answer>
            <rubric>
            1. Gives a separate Return Window per item: 30 days for the GPS watch and 60 days for the jacket
            2. End dates are counted from the Order Date (2026-10-31): November 30, 2026 for the watch and December 30, 2026 for the jacket
            </rubric>

            """.ReplaceLineEndings(Environment.NewLine),
            prompt);
    }

    [Fact]
    public async Task AJudgeReplyMissingARubricItemIsAJudgeFailureNotAVerdict()
    {
        var anthropic = new FakeAnthropic(FakeAnthropic.Verdicts((1, "30 and 60 days.", true)));

        // Composed as Reporting composes evaluators, which records an evaluator's exception on its metric.
        var result = await Answer("The watch: 30 days. The jacket: 60 days.")
            .EvaluateAllAsync(new CompositeEvaluator(new RubricEvaluator()), AcceptanceCase, JudgeClient.Configuration(anthropic.Client()));

        var failure = Assert.Throws<JudgeFailedException>(() => JudgeFailedException.ThrowIfAny(AcceptanceCase.Id, result));
        Assert.Contains("did not return one verdict for each of the 2 rubric items", failure.Message);
    }

    private static HandBuiltConversation Answer(string answer) => new HandBuiltConversation()
        .User("what's the return window for my last order?")
        .Tool("search_policies", """
            { "mode": "keyword", "results": [
              { "citation": "Returns Exceptions › GPS watches, navigators and bike computers", "text": "Navigation electronics have a 30-day return window from the order date, not 60 days." },
              { "citation": "Returns Policy › Return window", "text": "You can return most items within 60 days of the order date for a full refund." } ] }
            """)
        .Answer(answer);
}
