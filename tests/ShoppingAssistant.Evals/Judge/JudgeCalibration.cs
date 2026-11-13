using eShop.Assistant.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using ShoppingAssistant.Evals.Cases;
using ShoppingAssistant.Evals.Checks;

namespace ShoppingAssistant.Evals.Judge;

/// <summary>
/// The judge's calibration set (<c>Judge/calibration.json</c>): fixed answers, each labelled with the verdict every
/// rubric item should get. Some answers are correct; others are subtly wrong. The judge grades them as it grades a
/// sample, and any verdict that differs from its label is a <see cref="JudgeError"/>: the judge is broken, not the
/// assistant.
/// </summary>
public static class JudgeCalibration
{
    /// <summary>Calibration scenarios are named with this prefix, so the report and the gate keep them apart from cases.</summary>
    public const string ScenarioPrefix = "judge-calibration.";

    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "Judge", "calibration.json");

    public static IReadOnlyList<CalibrationAnswer> LoadAll() =>
        JsonSerializer.Deserialize<CalibrationFile>(File.ReadAllText(FilePath), EvalCase.JsonOptions)?.Answers
            ?? throw new InvalidDataException("The calibration file is empty.");

    /// <summary>
    /// The judge errors in <paramref name="judged"/>: every rubric verdict that differs from its label. Each rubric
    /// metric is re-interpreted for the report: failed when the judge disagreed with the label, passed when it agreed,
    /// whichever way the label goes.
    /// </summary>
    /// <exception cref="JudgeFailedException">The judge call failed, so there is no verdict to compare.</exception>
    public static IReadOnlyList<JudgeError> Compare(CalibrationAnswer answer, EvaluationResult judged)
    {
        JudgeFailedException.ThrowIfAny(answer.Id, judged);

        List<JudgeError> errors = [];
        foreach (var item in answer.Rubric)
        {
            var metric = (BooleanMetric)judged.Metrics[RubricEvaluator.ItemPrefix + item.Item];
            var verdict = metric.Value ?? throw new JudgeFailedException(answer.Id, $"no verdict for \"{item.Item}\"");
            InterpretAgainstLabel(metric, item.Passes, verdict);
            if (verdict != item.Passes)
            {
                errors.Add(new JudgeError(answer.Id, item.Item, item.Passes, verdict, metric.Reason ?? ""));
            }
        }

        return errors;
    }

    private static void InterpretAgainstLabel(BooleanMetric metric, bool labelled, bool verdict)
    {
        var asLabelled = verdict == labelled;
        var reason = asLabelled
            ? $"As labelled ({PassOrFail(labelled)}): {metric.Reason}"
            : $"Judge error: labelled {PassOrFail(labelled)}, judged {PassOrFail(verdict)}: {metric.Reason}";
        metric.Interpretation = new EvaluationMetricInterpretation(
            asLabelled ? EvaluationRating.Good : EvaluationRating.Unacceptable, failed: !asLabelled, reason);
    }

    internal static string PassOrFail(bool passes) => passes ? "pass" : "fail";

    private sealed record CalibrationFile(IReadOnlyList<CalibrationAnswer> Answers);
}

/// <summary>A fixed answer with the rubric verdicts it should get.</summary>
/// <param name="Id">Stable kebab-case id; the scenario is <c>judge-calibration.{Id}</c>.</param>
/// <param name="Note">What is right or wrong about the answer, for whoever reads a judge error.</param>
/// <param name="PolicySections">What <c>search_policies</c> returned in the answer's conversation.</param>
/// <param name="Rubric">The rubric items, each with the verdict the judge should give.</param>
public sealed record CalibrationAnswer(
    string Id,
    CaseCategory Category,
    string Note,
    IReadOnlyList<string> Turns,
    IReadOnlyList<CalibrationPolicySection> PolicySections,
    string Reference,
    string Answer,
    IReadOnlyList<LabelledRubricItem> Rubric)
{
    /// <summary>The answer as the case the judge grades: the question, reference and rubric, and no deterministic checks.</summary>
    public EvalCase Case => new(
        Id, Category, SignedInAs: null, Turns, CatalogResults: null,
        MustCallTools: [], MustNotCallTools: [], MustCite: [], MustContain: [], MustNotContain: [],
        Reference, [.. Rubric.Select(r => r.Item)]);

    /// <summary>The conversation up to the last user turn: the turns.</summary>
    public IReadOnlyList<ChatMessage> Prompt =>
    [
        .. Turns.Select(turn => new ChatMessage(ChatRole.User, turn)),
    ];

    /// <summary>The reply to the last turn: the <c>search_policies</c> call, its result, and the answer.</summary>
    public ChatResponse Reply
    {
        get
        {
            const string CallId = "calibration-search";
            var result = JsonSerializer.SerializeToElement(
                new { Mode = "keyword", Results = PolicySections.Select(s => new { s.Citation, s.Text }) }, JsonSerializerOptions.Web);
            return new ChatResponse(
            [
                new(ChatRole.Assistant, [new FunctionCallContent(CallId, AssistantToolNames.SearchPolicies, new Dictionary<string, object?>())]),
                new(ChatRole.Tool, [new FunctionResultContent(CallId, result)]),
                new(ChatRole.Assistant, Answer),
            ]);
        }
    }

    /// <summary>Has <paramref name="evaluator"/> grade the answer, as a sample is graded.</summary>
    public async Task<EvaluationResult> GradeAsync(IEvaluator evaluator, ChatConfiguration judge, CancellationToken cancellationToken) =>
        await evaluator.EvaluateAsync(Prompt, Reply, judge, [new EvalCaseContext(Case)], cancellationToken);
}

public sealed record CalibrationPolicySection(string Citation, string Text);

/// <param name="Passes">The verdict the judge should give this item for this answer.</param>
public sealed record LabelledRubricItem(string Item, bool Passes);

/// <summary>
/// The judge gave a calibration answer's rubric item a verdict that differs from its label. The judge is wrong, so
/// this fails the run as a judge error, never as an eval miss.
/// </summary>
public sealed record JudgeError(string CalibrationId, string RubricItem, bool Labelled, bool Judged, string JudgeReason)
{
    public override string ToString() =>
        $"{CalibrationId}: \"{RubricItem}\" is labelled {JudgeCalibration.PassOrFail(Labelled)} but the judge said {JudgeCalibration.PassOrFail(Judged)}: {JudgeReason}";
}
