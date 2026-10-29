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

        // Migrate, then index the Policy files, before the app starts serving.
        builder.Services.AddScoped<PolicyIngestion>();
        builder.Services.AddMigration<PolicyContext>((_, services) => services.GetRequiredService<PolicyIngestion>().IngestAsync());

        builder.Services.AddScoped<PolicySearch>();
    }
}
