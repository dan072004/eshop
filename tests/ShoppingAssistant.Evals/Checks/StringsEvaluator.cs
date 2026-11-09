using System.Text.RegularExpressions;
using Microsoft.Extensions.AI.Evaluation;
using ShoppingAssistant.Evals.Cases;

namespace ShoppingAssistant.Evals.Checks;

/// <summary>
/// Every <c>mustContain</c> literal appears in the assistant's answers, and no <c>mustNotContain</c> literal does, in
/// any turn: a leak in an earlier answer counts. Literals match as whole tokens, so order 202 is not found in the year
/// 2026, and the match is case-sensitive.
/// </summary>
public sealed class StringsEvaluator : DeterministicCheck
{
    public override string MetricName => "Strings";

    protected override EvaluationMetric Check(EvalConversation conversation, EvalCase evalCase)
    {
        var answers = string.Join("\n", conversation.Answers);
        var missing = evalCase.MustContain.Where(s => !ContainsToken(answers, s)).ToList();
        var forbidden = evalCase.MustNotContain.Where(s => ContainsToken(answers, s)).ToList();

        List<string> problems = [];
        if (missing.Count > 0)
        {
            problems.Add($"Missing: {Quoted(missing)}.");
        }

        if (forbidden.Count > 0)
        {
            problems.Add($"Contains forbidden: {Quoted(forbidden)}.");
        }

        return problems.Count == 0
            ? Verdict(true, $"{evalCase.MustContain.Count} required present, {evalCase.MustNotContain.Count} forbidden absent.")
            : Verdict(false, string.Join(" ", problems));
    }

    // Not preceded or followed by a letter or digit.
    private static bool ContainsToken(string text, string literal) =>
        Regex.IsMatch(text, $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(literal)}(?![\p{{L}}\p{{N}}])");
}
