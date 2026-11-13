using Anthropic;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

namespace ShoppingAssistant.Evals.Judge;

/// <summary>
/// The judge: <c>claude-sonnet-5-5</c> at low effort, a stronger model than the Haiku 4.5 it grades, on a fresh
/// Anthropic client. It deliberately does not go through WebApp's chat pipeline, whose <c>ReasoningEffort.None</c> is
/// sent as <c>thinking: disabled</c> and gets a 400 from Sonnet 5.5. <c>ReasoningEffort.Low</c> is sent as adaptive
/// thinking with <c>output_config.effort: low</c>.
/// </summary>
/// <remarks>
/// The Anthropic SDK sends no temperature, top_p or top_k, so the judge's sampling can't be pinned; the calibration
/// set (<c>Judge/calibration.json</c>) is what shows it grades correctly. Raise <see cref="Effort"/> only when the
/// calibration set catches a misgrade the rubric wording can't fix.
/// </remarks>
public static class JudgeClient
{
    public const string ModelId = "claude-sonnet-5-5";

    public const ReasoningEffort Effort = ReasoningEffort.Low;

    // Room for adaptive thinking plus a few short reasons; the verdicts themselves are small.
    public const int MaxOutputTokens = 8192;

    /// <summary>The judge's chat client over <paramref name="anthropic"/>, with the judge's model and effort.</summary>
    public static IChatClient Create(AnthropicClient anthropic) =>
        new ChatClientBuilder(anthropic.AsIChatClient(ModelId, MaxOutputTokens))
            .ConfigureOptions(options =>
            {
                options.ModelId ??= ModelId;
                options.MaxOutputTokens ??= MaxOutputTokens;
                options.Reasoning ??= new ReasoningOptions { Effort = Effort };
            })
            .Build();

    /// <summary>The chat configuration the evaluators get the judge from.</summary>
    public static ChatConfiguration Configuration(AnthropicClient anthropic) => new(Create(anthropic));
}
