using Microsoft.Extensions.AI.Evaluation;
using ShoppingAssistant.Evals.Cases;

namespace ShoppingAssistant.Evals.Checks;

/// <summary>Every <c>mustCite</c> string appears, exactly (case and punctuation included), in the final answer.</summary>
public sealed class CitationsPresentEvaluator : DeterministicCheck
{
    public override string MetricName => "Citations present";

    protected override EvaluationMetric Check(EvalConversation conversation, EvalCase evalCase)
    {
        var missing = evalCase.MustCite.Where(c => !conversation.FinalAnswer.Contains(c, StringComparison.Ordinal)).ToList();

        return missing.Count == 0
            ? Verdict(true, evalCase.MustCite.Count == 0 ? "No Citation required." : $"Cited: {Quoted(evalCase.MustCite)}.")
            : Verdict(false, $"Not cited: {Quoted(missing)}.");
    }
}
