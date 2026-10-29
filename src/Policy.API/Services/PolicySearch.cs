using eShop.Policy.API.Infrastructure;

namespace eShop.Policy.API.Services;

/// <summary>Finds the Policy Sections that best answer a question.</summary>
/// <remarks>
/// With no embedding provider configured, it runs in keyword mode: Postgres full-text search with
/// the English configuration over each section's indexed text, ordered by rank.
/// </remarks>
public sealed class PolicySearch(PolicyContext context)
{
    public const int MaxTop = 10;

    public const string KeywordMode = "keyword";

    public async Task<PolicySearchResponse> SearchAsync(string question, int top, CancellationToken cancellationToken = default)
    {
        var limit = Math.Min(top, MaxTop);
        var results = await KeywordSearchAsync(question, limit, cancellationToken);
        return new PolicySearchResponse(KeywordMode, results);
    }

    private async Task<List<PolicySearchResult>> KeywordSearchAsync(string question, int limit, CancellationToken cancellationToken)
    {
        // plainto_tsquery joins the question's words with AND; a question rarely repeats every word of
        // the rule it is about, so the words are joined with OR instead and rank decides the order.
        var rows = await context.Database.SqlQuery<KeywordRow>($"""
            WITH question AS (
                SELECT replace(plainto_tsquery('english', {question})::text, ' & ', ' | ')::tsquery AS query
            )
            SELECT p."Slug" AS "PolicySlug", p."Title" AS "PolicyTitle", s."Heading", s."Anchor", s."Body",
                   ts_rank(s."SearchVector", question.query) AS "Score"
            FROM "PolicySection" s
            JOIN "Policy" p ON p."Slug" = s."PolicySlug"
            CROSS JOIN question
            WHERE s."SearchVector" @@ question.query
            ORDER BY "Score" DESC, p."Slug", s."Position"
            LIMIT {limit}
            """).ToListAsync(cancellationToken);

        return rows.Select(row => new PolicySearchResult(
            PolicySplitter.Citation(row.PolicyTitle, row.Heading),
            row.PolicySlug,
            row.PolicyTitle,
            row.Heading,
            row.Anchor,
            row.Body,
            row.Score)).ToList();
    }

    private sealed record KeywordRow(string PolicySlug, string PolicyTitle, string Heading, string Anchor, string Body, float Score);
}

/// <param name="Mode"><c>"keyword"</c> or, once an embedding provider is configured, <c>"semantic"</c>.</param>
public sealed record PolicySearchResponse(string Mode, IReadOnlyList<PolicySearchResult> Results);

/// <param name="Citation"><c>"{Policy title} › {heading}"</c>, or just the Policy title for an intro section.</param>
/// <param name="Section">The Policy Section's heading.</param>
/// <param name="Text">The Policy Section's body.</param>
public sealed record PolicySearchResult(
    string Citation,
    string PolicySlug,
    string PolicyTitle,
    string Section,
    string Anchor,
    string Text,
    double Score);
