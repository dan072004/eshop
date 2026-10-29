namespace eShop.Assistant.Tools.Policy;

/// <summary>
/// The result of <c>search_policies</c>: policy-api's search response passed through unchanged.
/// <paramref name="Mode"/> is <c>"semantic"</c> or <c>"keyword"</c>.
/// </summary>
public sealed record PolicySearchResult(string Mode, IReadOnlyList<PolicySectionMatch> Results);

/// <summary>A matching Policy Section.</summary>
/// <param name="Citation"><c>"{Policy title} › {heading}"</c>, or just the Policy title for an intro section.</param>
/// <param name="Section">The Policy Section's heading.</param>
/// <param name="Text">The Policy Section's body.</param>
public sealed record PolicySectionMatch(
    string Citation,
    string PolicySlug,
    string PolicyTitle,
    string Section,
    string Anchor,
    string Text,
    double Score);
