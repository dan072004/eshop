using Microsoft.Extensions.AI.Evaluation;
using ShoppingAssistant.Evals.Cases;

namespace ShoppingAssistant.Evals.Checks;

/// <summary>
/// Every <c>mustCallTools</c> entry was called, and no <c>mustNotCallTools</c> entry was. Basket tools are out of the
/// evals' scope, so a call to one is forbidden too unless the case requires it.
/// </summary>
public sealed class ToolsEvaluator : DeterministicCheck
{
    // The conversation class's own basket functions, named after their methods.
    private static readonly string[] BasketTools = ["AddToCart", "GetCartContents"];

    public override string MetricName => "Tools";

    protected override EvaluationMetric Check(EvalConversation conversation, EvalCase evalCase)
    {
        var called = conversation.Calls.Select(c => c.Name).ToList();
        var missing = evalCase.MustCallTools.Where(t => !called.Contains(t)).ToList();
        var forbidden = evalCase.MustNotCallTools.Concat(BasketTools.Except(evalCase.MustCallTools))
            .Distinct().Where(called.Contains).ToList();

        List<string> problems = [];
        if (missing.Count > 0)
        {
            problems.Add($"Not called: {string.Join(", ", missing)}.");
        }

        if (forbidden.Count > 0)
        {
            problems.Add($"Called but forbidden: {string.Join(", ", forbidden)}.");
        }

        return problems.Count == 0
            ? Verdict(true, $"Called: {(called.Count == 0 ? "no tools" : string.Join(", ", called))}.")
            : Verdict(false, string.Join(" ", problems));
    }
}
