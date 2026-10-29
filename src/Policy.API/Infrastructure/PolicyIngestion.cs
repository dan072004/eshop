using System.Security.Cryptography;
using eShop.Policy.API.Services;
using Microsoft.Extensions.Options;
using Npgsql;
using Pgvector;

namespace eShop.Policy.API.Infrastructure;

/// <summary>
/// Indexes the Policy files at startup: new or changed Policies get their Policy Sections replaced,
/// Policies whose file is gone are deleted, and unchanged Policies are left alone.
/// </summary>
/// <remarks>
/// It runs in one transaction holding a Postgres transaction-level advisory lock, so two replicas
/// starting together index one after the other. A Policy counts as changed when its file hash or the
/// current embedding model id differs from what is stored, so switching embedding provider indexes every Policy again.
/// </remarks>
public sealed class PolicyIngestion(
    PolicyContext context,
    PolicyEmbeddings embeddings,
    IOptions<PolicyOptions> options,
    TimeProvider timeProvider,
    ILogger<PolicyIngestion> logger)
{
    private const string AdvisoryLockKey = "policy-ingestion";

    public async Task IngestAsync(CancellationToken cancellationToken = default)
    {
        var folder = options.Value.Folder;
        var files = ReadPolicyFiles(folder);
        var embeddingModelId = embeddings.ModelId;

        // The migration may have just created the vector extension on this connection; Npgsql only knows
        // the vector type once it reloads types (workaround from https://github.com/npgsql/efcore.pg/issues/292).
        await context.Database.OpenConnectionAsync(cancellationToken);
        await ((NpgsqlConnection)context.Database.GetDbConnection()).ReloadTypesAsync(cancellationToken);

        var strategy = context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            context.ChangeTracker.Clear();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            await context.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtext({AdvisoryLockKey}))", cancellationToken);

            var stored = await context.Policies.AsNoTracking()
                .ToDictionaryAsync(p => p.Slug, p => (p.ContentHash, p.EmbeddingModelId), cancellationToken);

            var toIndex = files
                .Where(f => !stored.TryGetValue(f.Policy.Slug, out var s) || s.ContentHash != f.ContentHash || s.EmbeddingModelId != embeddingModelId)
                .ToList();
            var gone = stored.Keys.Except(files.Select(f => f.Policy.Slug)).ToList();

            // Sections go with their Policy (ON DELETE CASCADE), so a changed Policy is deleted and inserted again.
            var toDelete = gone.Concat(toIndex.Select(f => f.Policy.Slug)).ToList();
            await context.Policies.Where(p => toDelete.Contains(p.Slug)).ExecuteDeleteAsync(cancellationToken);

            var indexedAt = timeProvider.GetUtcNow();
            var vectors = await EmbedSectionsAsync(toIndex, cancellationToken);
            context.Policies.AddRange(toIndex.Select(f => ToIndexedPolicy(f, embeddingModelId, indexedAt, vectors)));
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Indexed Policies from {Folder} with embedding model {EmbeddingModelId}: {Indexed} new or changed, {Removed} removed, {Unchanged} unchanged",
                folder, embeddingModelId, toIndex.Count, gone.Count, files.Count - toIndex.Count);
        });
    }

    // Only new or changed Policies are embedded; without a provider, sections are stored with no vector.
    private async Task<IReadOnlyDictionary<PolicySection, Vector>> EmbedSectionsAsync(List<PolicyFile> toIndex, CancellationToken cancellationToken)
    {
        if (!embeddings.IsEnabled)
        {
            return new Dictionary<PolicySection, Vector>();
        }

        var sections = toIndex.SelectMany(f => f.Policy.Sections).ToList();
        var vectors = await embeddings.EmbedAsync(sections.Select(s => s.IndexedText).ToList(), cancellationToken);
        return sections.Zip(vectors).ToDictionary<(PolicySection First, Vector Second), PolicySection, Vector>(
            pair => pair.First, pair => pair.Second, ReferenceEqualityComparer.Instance);
    }

    private static IndexedPolicy ToIndexedPolicy(PolicyFile file, string embeddingModelId, DateTimeOffset indexedAt, IReadOnlyDictionary<PolicySection, Vector> vectors) => new()
    {
        Slug = file.Policy.Slug,
        Title = file.Policy.Title,
        Category = file.Policy.Category,
        LastUpdated = file.Policy.LastUpdated,
        ContentHash = file.ContentHash,
        EmbeddingModelId = embeddingModelId,
        IndexedAt = indexedAt,
        Sections = file.Policy.Sections.Select(section => new IndexedPolicySection
        {
            PolicySlug = file.Policy.Slug,
            Position = section.Position,
            Heading = section.Heading,
            Anchor = section.Anchor,
            Body = section.Body,
            IndexedText = section.IndexedText,
            Embedding = vectors.GetValueOrDefault(section),
        }).ToList(),
    };

    // Read and split every file before touching the database, so a broken Policy fails startup naming its file.
    private static List<PolicyFile> ReadPolicyFiles(string folder)
    {
        if (!Directory.Exists(folder))
        {
            throw new DirectoryNotFoundException($"The Policies folder '{folder}' does not exist. Set Policies:Folder.");
        }

        return Directory.GetFiles(folder, "*.md")
            .Order(StringComparer.Ordinal)
            .Select(path =>
            {
                var bytes = File.ReadAllBytes(path);
                var policy = PolicySplitter.Split(Path.GetFileName(path), new StreamReader(new MemoryStream(bytes)).ReadToEnd());
                return new PolicyFile(policy, Convert.ToHexStringLower(SHA256.HashData(bytes)));
            })
            .ToList();
    }

    private sealed record PolicyFile(ParsedPolicy Policy, string ContentHash);
}
