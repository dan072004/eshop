using eShop.Policy.API.Infrastructure;

namespace eShop.Policy.API.Services;

/// <summary>Finds the Policy Sections that best answer a question.</summary>
/// <remarks>
/// With an embedding provider configured, it runs in semantic mode: it embeds the question and orders the
/// Policy Sections by cosine distance, scanning every row (there are about a hundred, so there is no vector index).
/// With none, it runs in keyword mode: Postgres full-text search with the English configuration over each
/// section's indexed text, ordered by rank.
/// </remarks>
public sealed class PolicySearch(PolicyContext context, PolicyEmbeddings embeddings)
{
    public const int MaxTop = 10;

    public const string KeywordMode = "keyword";

    public const string SemanticMode = "semantic";

    public async Task<PolicySearchResponse> SearchAsync(string question, int top, CancellationToken cancellationToken = default)
    {
        var limit = Math.Min(top, MaxTop);
        if (embeddings.IsEnabled)
        {
            return new PolicySearchResponse(SemanticMode, await SemanticSearchAsync(question, limit, cancellationToken));
        }

        return new PolicySearchResponse(KeywordMode, await KeywordSearchAsync(question, limit, cancellationToken));
    }

    private async Task<List<PolicySearchResult>> SemanticSearchAsync(string question, int limit, CancellationToken cancellationToken)
    {
        var vector = await embeddings.EmbedAsync(question, cancellationToken);

        // <=> is cosine distance; the score is cosine similarity, so higher is closer, as in keyword mode.
        var rows = await context.Database.SqlQuery<SectionRow>($"""
            SELECT p."Slug" AS "PolicySlug", p."Title" AS "PolicyTitle", s."Heading", s."Anchor", s."Body",
                   1 - (s."Embedding" <=> {vector}) AS "Score"
            FROM "PolicySection" s
            JOIN "Policy" p ON p."Slug" = s."PolicySlug"
            WHERE s."Embedding" IS NOT NULL
            ORDER BY s."Embedding" <=> {vector}, p."Slug", s."Position"
            LIMIT {limit}
            """).ToListAsync(cancellationToken);

        return rows.Select(ToResult).ToList();
    }

    private async Task<List<PolicySearchResult>> KeywordSearchAsync(string question, int limit, CancellationToken cancellationToken)
    {
        // plainto_tsquery joins the question's words with AND; a question rarely repeats every word of
        // the rule it is about, so the words are joined with OR instead and rank decides the order.
        var rows = await context.Database.SqlQuery<SectionRow>($"""
            WITH question AS (
                SELECT replace(plainto_tsquery('english', {question})::text, ' & ', ' | ')::tsquery AS query
            )
            SELECT p."Slug" AS "PolicySlug", p."Title" AS "PolicyTitle", s."Heading", s."Anchor", s."Body",
                   ts_rank(s."SearchVector", question.query)::float8 AS "Score"
            FROM "PolicySection" s
            JOIN "Policy" p ON p."Slug" = s."PolicySlug"
            CROSS JOIN question
            WHERE s."SearchVector" @@ question.query
            ORDER BY "Score" DESC, p."Slug", s."Position"
            LIMIT {limit}
            """).ToListAsync(cancellationToken);

        return rows.Select(ToResult).ToList();
    }

    private static PolicySearchResult ToResult(SectionRow row) => new(
        PolicySplitter.Citation(row.PolicyTitle, row.Heading),
        row.PolicySlug,
        row.PolicyTitle,
        row.Heading,
        row.Anchor,
        row.Body,
        row.Score);

    private sealed record SectionRow(string PolicySlug, string PolicyTitle, string Heading, string Anchor, string Body, double Score);
}

/// <param name="Mode"><c>"semantic"</c> when an embedding provider is configured, otherwise <c>"keyword"</c>.</param>
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
