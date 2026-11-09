using System.Text.Json.Serialization;
using ShoppingAssistant.Evals.World;

namespace ShoppingAssistant.Evals.Cases;

/// <summary>
/// One eval case from <c>Cases/cases.json</c>: a Buyer asks the Shopping Assistant one or more questions, and the
/// deterministic checks and the judge score the answer. Cases refer to the fixed world's orders by number.
/// </summary>
/// <param name="Id">Stable kebab-case id, used in reports (as the scenario name) and to filter cases.</param>
/// <param name="SignedInAs">The world's Buyer (<c>alice</c>, <c>bob</c>), or null for a signed-out Buyer.</param>
/// <param name="Turns">The user messages, in order.</param>
/// <param name="CatalogResults">What <c>search_catalog</c> returns in this case; null finds nothing.</param>
/// <param name="MustCallTools">Tool names that must appear among the calls.</param>
/// <param name="MustNotCallTools">Tool names that must not appear among the calls.</param>
/// <param name="MustCite">Citation strings that must appear, exactly, in the final answer.</param>
/// <param name="MustContain">Literals every answer must contain, matched as whole tokens.</param>
/// <param name="MustNotContain">Literals no answer may contain, matched as whole tokens (other Buyers' order numbers).</param>
/// <param name="Reference">A reference answer for the judge.</param>
/// <param name="Rubric">Short yes/no criteria for the judge.</param>
/// <param name="KnownMiss">Why a case known to fail still fails; it stays failing until the prompt or tools are fixed.</param>
public sealed record EvalCase(
    string Id,
    CaseCategory Category,
    string? SignedInAs,
    IReadOnlyList<string> Turns,
    IReadOnlyList<WorldProduct>? CatalogResults,
    IReadOnlyList<string> MustCallTools,
    IReadOnlyList<string> MustNotCallTools,
    IReadOnlyList<string> MustCite,
    IReadOnlyList<string> MustContain,
    IReadOnlyList<string> MustNotContain,
    string Reference,
    IReadOnlyList<string> Rubric,
    string? KnownMiss = null)
{
    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "Cases", "cases.json");

    // Every field but knownMiss must be present, so a case can't silently lose a check.
    private static readonly JsonSerializerOptions Options = new(JsonSerializerOptions.Web)
    {
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        Converters = { new JsonStringEnumConverter<CaseCategory>(JsonNamingPolicy.KebabCaseLower, allowIntegerValues: false) },
    };

    public static IReadOnlyList<EvalCase> LoadAll() => Parse(File.ReadAllText(FilePath));

    public static IReadOnlyList<EvalCase> Parse(string json) =>
        JsonSerializer.Deserialize<CaseFile>(json, Options)?.Cases
            ?? throw new InvalidDataException("The case file is empty.");

    private sealed record CaseFile(IReadOnlyList<EvalCase> Cases);
}

public enum CaseCategory
{
    Policy,
    Order,
    Catalog,
    OutOfScope,
    Adversarial,
}
