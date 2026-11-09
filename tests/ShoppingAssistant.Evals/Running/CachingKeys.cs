using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.AI;
using ShoppingAssistant.Evals.Judge;

namespace ShoppingAssistant.Evals.Running;

/// <summary>
/// What the eval adds to Reporting's response-cache key, which already covers the messages, the options, the scenario
/// and iteration names, the provider and the model id. Each added key is a hash of an input Reporting can't see, so
/// changing it makes the next run call the model again instead of replaying a stale answer.
/// </summary>
public static class CachingKeys
{
    /// <summary>
    /// The keys for one case's chat turns and its judge call:
    /// <list type="bullet">
    /// <item>every tool's name, description and parameter schema, in the order they are sent (Reporting's key leaves
    /// out <c>ChatOptions.Tools</c>);</item>
    /// <item>this case's entry in the case file, as written, so editing one case resamples only that case;</item>
    /// <item>the world file.</item>
    /// </list>
    /// </summary>
    /// <exception cref="InvalidDataException">The case file has no case with this id.</exception>
    public static IReadOnlyList<string> ForCase(IEnumerable<AITool> tools, string caseFile, string caseId, string worldFile) =>
    [
        "tools:" + Hash(JsonSerializer.Serialize(tools.Select(t => new
        {
            t.Name,
            t.Description,
            Schema = (t as AIFunctionDeclaration)?.JsonSchema,
        }))),
        "case:" + Hash(CaseEntry(caseFile, caseId)),
        "world:" + Hash(worldFile),
    ];

    /// <summary>
    /// The judge's settings that are applied below Reporting's cache (<see cref="JudgeClient"/>'s effort and output
    /// limit) or that a change should resample on its own (its model and <see cref="RubricEvaluator.Instructions"/>).
    /// </summary>
    public static IReadOnlyList<string> Judge { get; } =
    [
        "judge:" + Hash(JsonSerializer.Serialize(new
        {
            JudgeClient.ModelId,
            Effort = JudgeClient.Effort.ToString(),
            JudgeClient.MaxOutputTokens,
            RubricEvaluator.Instructions,
        })),
    ];

    private static string CaseEntry(string caseFile, string caseId)
    {
        using var document = JsonDocument.Parse(caseFile);
        return document.RootElement.GetProperty("cases").EnumerateArray()
            .FirstOrDefault(c => c.TryGetProperty("id", out var id) && id.ValueEquals(caseId)) is { ValueKind: JsonValueKind.Object } entry
                ? entry.GetRawText()
                : throw new InvalidDataException($"The case file has no case '{caseId}'.");
    }

    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
