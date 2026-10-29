namespace eShop.Policy.API.Model;

/// <summary>A Policy file split into its Policy Sections, before it is indexed.</summary>
public sealed record ParsedPolicy(
    string Slug,
    string Title,
    string Category,
    string? LastUpdated,
    IReadOnlyList<PolicySection> Sections);

/// <summary>One headed part of a Policy, in the order it appears in the file.</summary>
public sealed record PolicySection(int Position, string Heading, string Anchor, string Body, string Citation, string IndexedText);
