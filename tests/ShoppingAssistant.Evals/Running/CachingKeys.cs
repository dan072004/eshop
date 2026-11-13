using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using ShoppingAssistant.Evals.Judge;

namespace ShoppingAssistant.Evals.Running;

/// <summary>
/// What the eval adds to Reporting's response-cache key, which already covers the messages, the options, the scenario
/// and iteration names, the provider and the model id. Each added key is a hash of an input Reporting can't see, so
/// changing it makes the next run call the model again instead of replaying a stale answer.
/// </summary>
public static partial class CachingKeys
{
    /// <summary>
    /// The keys for one case's chat turns and its judge call:
    /// <list type="bullet">
    /// <item>every tool's name, description and parameter schema, in the order they are sent (Reporting's key leaves
    /// out <c>ChatOptions.Tools</c>);</item>
    /// <item>this case's entry in the case file, as written but without its <c>knownMiss</c> note, so editing one case
    /// resamples only that case, and noting a miss resamples nothing;</item>
    /// <item>the world file.</item>
    /// </list>
    /// </summary>
    /// <exception cref="InvalidDataException">The case file has no case with this id.</exception>
    public static IReadOnlyList<string> ForCase(IEnumerable<AITool> tools, string caseFile, string caseId, string worldFile) =>
    [
        "tools:" + Hash(JsonSerializer.Serialize(tools.Select(t => new
        {
            t.Name,
            Description = Lf(t.Description),
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
            Instructions = Lf(RubricEvaluator.Instructions),
        })),
    ];

    private static string CaseEntry(string caseFile, string caseId)
    {
        using var document = JsonDocument.Parse(caseFile);
        return document.RootElement.GetProperty("cases").EnumerateArray()
            .FirstOrDefault(c => c.TryGetProperty("id", out var id) && id.ValueEquals(caseId)) is { ValueKind: JsonValueKind.Object } entry
                ? KnownMissNote().Replace(entry.GetRawText(), "")
                : throw new InvalidDataException($"The case file has no case '{caseId}'.");
    }

    // A knownMiss note, with the comma that joins it to the entry. It changes neither the conversation nor the grading,
    // so noting a miss doesn't resample it; cutting it from the text keeps the key of an entry without one unchanged.
    [GeneratedRegex("""\s*,\s*"knownMiss"\s*:\s*"(?:[^"\\]|\\.)*"|"knownMiss"\s*:\s*"(?:[^"\\]|\\.)*"\s*,\s*""")]
    private static partial Regex KnownMissNote();

    // Hashed with LF line endings, so a CRLF checkout (git's autocrlf on Windows) gets the same keys as CI's LF one:
    // the case and world files and the judge's instructions, a raw string literal, follow the checkout's line endings.
    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Lf(value))));

    // Strings inside a serialized value are escaped (a CR becomes \r), so those are normalised before serializing.
    [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(text))]
    private static string? Lf(string? text) => text?.ReplaceLineEndings("\n");
}
