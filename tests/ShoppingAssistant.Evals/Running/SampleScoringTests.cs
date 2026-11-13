using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using ShoppingAssistant.Evals.Cases;
using ShoppingAssistant.Evals.Checks;
using ShoppingAssistant.Evals.Gate;

namespace ShoppingAssistant.Evals.Running;

/// <summary>
/// A sample's stored result, as Reporting writes it to the results store, turned into the <see cref="SampleResult"/>
/// the gate reads: what failed, why there is no verdict, and what the sample cost.
/// </summary>
public sealed class SampleScoringTests
{
    private static readonly EvalCase ShippingCase = new(
        Id: "shipping-cost",
        Category: CaseCategory.Policy,
        SignedInAs: null,
        Turns: ["how much is shipping?"],
        CatalogResults: null,
        MustCallTools: [],
        MustNotCallTools: [],
        MustCite: [],
        MustContain: [],
        MustNotContain: [],
        Reference: "Shipping is free over $50.",
        Rubric: ["Says shipping is free over $50"]);

    private static readonly HandBuiltConversation ShippingAnswer = new HandBuiltConversation()
        .User("how much is shipping?")
        .Answer("Shipping is free over $50.");

    [Fact]
    public async Task ACheckThatThrowsMakesTheSampleAnInfrastructureErrorNotAPass()
    {
        // Composed as Reporting composes evaluators, which records an evaluator's exception on its metric.
        var evaluation = await ShippingAnswer.EvaluateAllAsync(new CompositeEvaluator(new ToolsEvaluator(), new CrashingCheck()), ShippingCase);

        var sample = SampleScoring.Score(ShippingCase, Transcript(), Stored(evaluation));

        Assert.Equivalent(
            new
            {
                Passed = false,
                Failures = Array.Empty<CheckFailure>(),
                InfrastructureError = "Case 'shipping-cost': the Crashing check errored, so the sample has no verdict: System.InvalidOperationException: The check has a bug.",
            },
            sample);
    }

    [Fact]
    public async Task AJudgeCallThatReachedTheModelIsCountedEvenWhenTheChatTurnsWereReplayedAndAReplayedOneIsNot()
    {
        var evaluation = await ShippingAnswer.EvaluateAllAsync(new ToolsEvaluator(), ShippingCase);

        // The chat turns were replayed (no model calls), as after a change to the judge's settings.
        var rejudged = SampleScoring.Score(ShippingCase, Transcript(), Stored(evaluation, JudgeCall(cacheHit: false)));
        var replayed = SampleScoring.Score(ShippingCase, Transcript(), Stored(evaluation, JudgeCall(cacheHit: true)));

        Assert.Equal([("claude-sonnet-5-5", 1_000L, 100L)], rejudged.Usage.Select(u => (u.Model, u.InputTokens, u.OutputTokens)));
        Assert.Empty(replayed.Usage);
    }

    private static ChatTurnDetails JudgeCall(bool cacheHit) => new(
        TimeSpan.FromSeconds(2),
        "claude-sonnet-5-5",
        "anthropic",
        new UsageDetails { InputTokenCount = 1_000, OutputTokenCount = 100 },
        cacheKey: "judge-key",
        cacheHit);

    private static CaseTranscript Transcript() => new([]);

    private static ScenarioRunResult Stored(EvaluationResult evaluation, params ChatTurnDetails[] judgeCalls) => new(
        ShippingCase.Id,
        "1",
        "20261110T120000Z",
        DateTime.UtcNow,
        [new ChatMessage(ChatRole.User, "how much is shipping?")],
        new ChatResponse(new ChatMessage(ChatRole.Assistant, "Shipping is free over $50.")),
        evaluation,
        new ChatDetails(judgeCalls));

    // A check with a bug: it throws instead of returning a verdict.
    private sealed class CrashingCheck : DeterministicCheck
    {
        public override string MetricName => "Crashing";

        protected override EvaluationMetric Check(EvalConversation conversation, EvalCase evalCase) =>
            throw new InvalidOperationException("The check has a bug.");
    }
}
