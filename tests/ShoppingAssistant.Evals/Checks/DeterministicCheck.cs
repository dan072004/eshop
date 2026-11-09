using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using ShoppingAssistant.Evals.Cases;

namespace ShoppingAssistant.Evals.Checks;

/// <summary>
/// A check that needs no model: it reads the conversation and the case (<see cref="EvalCaseContext"/>) and returns one
/// metric, already interpreted, so the report shows its verdict and reason and a sample fails when any metric failed.
/// </summary>
public abstract class DeterministicCheck : IEvaluator
{
    public abstract string MetricName { get; }

    public IReadOnlyCollection<string> EvaluationMetricNames => [MetricName];

    public ValueTask<EvaluationResult> EvaluateAsync(
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration = null,
        IEnumerable<EvaluationContext>? additionalContext = null,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(new EvaluationResult(
            Check(new EvalConversation(messages, modelResponse), EvalCaseContext.From(additionalContext))));

    protected abstract EvaluationMetric Check(EvalConversation conversation, EvalCase evalCase);

    /// <summary>A pass or a fail, with the reason shown in the report.</summary>
    protected BooleanMetric Verdict(bool passed, string reason) => new(MetricName, passed, reason)
    {
        Interpretation = new EvaluationMetricInterpretation(
            passed ? EvaluationRating.Good : EvaluationRating.Unacceptable, failed: !passed, reason),
    };

    protected static string Quoted(IEnumerable<string> values) => string.Join(", ", values.Distinct().Select(v => $"\"{v}\""));
}
