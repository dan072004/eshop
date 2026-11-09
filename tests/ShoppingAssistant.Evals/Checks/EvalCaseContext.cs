using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using ShoppingAssistant.Evals.Cases;

namespace ShoppingAssistant.Evals.Checks;

/// <summary>Hands every check the case it is scoring: its expectations and its Buyer.</summary>
public sealed class EvalCaseContext(EvalCase evalCase) : EvaluationContext(ContextName, new TextContent(evalCase.Id))
{
    public const string ContextName = "Eval case";

    public EvalCase Case { get; } = evalCase;

    public static EvalCase From(IEnumerable<EvaluationContext>? additionalContext) =>
        additionalContext?.OfType<EvalCaseContext>().SingleOrDefault()?.Case
            ?? throw new ArgumentException($"The checks need the case as an {nameof(EvalCaseContext)}.", nameof(additionalContext));
}
