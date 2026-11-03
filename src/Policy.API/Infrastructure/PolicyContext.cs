namespace eShop.Policy.API.Infrastructure;

/// <remarks>
/// Add migrations using the following command inside the 'Policy.API' project directory:
///
/// dotnet ef migrations add --context PolicyContext [migration-name]
/// </remarks>
public class PolicyContext(DbContextOptions<PolicyContext> options) : DbContext(options)
{
    public DbSet<IndexedPolicy> Policies => Set<IndexedPolicy>();

    public DbSet<IndexedPolicySection> PolicySections => Set<IndexedPolicySection>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasPostgresExtension("vector");

        builder.Entity<IndexedPolicy>(policy =>
        {
            policy.ToTable("Policy");
            policy.HasKey(p => p.Slug);
            policy.HasMany(p => p.Sections)
                .WithOne()
                .HasForeignKey(s => s.PolicySlug)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<IndexedPolicySection>(section =>
        {
            section.ToTable("PolicySection");
            section.HasIndex(s => new { s.PolicySlug, s.Position }).IsUnique();
            section.Property(s => s.Embedding).HasColumnType("vector(384)");
            section.HasGeneratedTsVectorColumn(s => s.SearchVector, "english", s => new { s.IndexedText })
                .HasIndex(s => s.SearchVector)
                .HasMethod("GIN");
        });
    }
}
