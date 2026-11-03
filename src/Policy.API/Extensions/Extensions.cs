using eShop.Policy.API;
using eShop.Policy.API.Infrastructure;
using eShop.Policy.API.Services;

public static class Extensions
{
    public static void AddApplicationServices(this IHostApplicationBuilder builder)
    {
        builder.AddNpgsqlDbContext<PolicyContext>("policydb", configureDbContextOptions: dbContextOptionsBuilder =>
        {
            dbContextOptionsBuilder.UseNpgsql(npgsql => npgsql.UseVector());
        });

        builder.Services.AddOptions<PolicyOptions>()
            .BindConfiguration(PolicyOptions.SectionName);

        builder.Services.TryAddSingleton(TimeProvider.System);

        // The embedding source is detected as Catalog.API does it, so the AppHost wires both the same way:
        // OllamaEnabled with an "embedding" connection, or a "textEmbeddingModel" (Foundry) connection.
        // With neither, there is no generator and Policy search runs in keyword mode.
        if (builder.Configuration["OllamaEnabled"] is string ollamaEnabled && bool.Parse(ollamaEnabled))
        {
            builder.AddOllamaApiClient("embedding")
                .AddEmbeddingGenerator();
        }
        else if (!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("textEmbeddingModel")))
        {
            builder.AddOpenAIClientFromConfiguration("textEmbeddingModel")
                .AddEmbeddingGenerator();
        }

        builder.Services.AddScoped<PolicyEmbeddings>();

        // Migrate, then index the Policy files, before the app starts serving.
        builder.Services.AddScoped<PolicyIngestion>();
        builder.Services.AddMigration<PolicyContext>((_, services) => services.GetRequiredService<PolicyIngestion>().IngestAsync());

        builder.Services.AddScoped<PolicySearch>();
    }
}
