global using Xunit;

// Each test class starts its own Aspire host; starting two orchestrators at once can time out.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
