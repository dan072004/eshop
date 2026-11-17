global using Xunit;

// Each test class starts its own eShop.Mcp host, and each host's startup edits the static
// JsonWebTokenHandler.DefaultInboundClaimTypeMap; two at once race (IndexOutOfRangeException in JwtBearerOptions).
[assembly: CollectionBehavior(DisableTestParallelization = true)]
