using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace eShop.Policy.FunctionalTests;

/// <summary>
/// Retrieval quality on demand: does semantic search with a real embedding model put the right Policy Section first?
/// </summary>
/// <remarks>
/// Skipped unless <c>ESHOP_POLICY_GOLDEN_SET=1</c>. Run it with
/// <c>$env:ESHOP_POLICY_GOLDEN_SET=1; dotnet test --project tests/Policy.FunctionalTests -- --filter-class "*GoldenSetTests"</c>.
/// It fails listing every question whose expected section was not the first result, with the rank it got.
/// </remarks>
public sealed class GoldenSetTests(GoldenSetFixture fixture) : IClassFixture<GoldenSetFixture>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Written from the Policy files before the first run, and not tuned to the model's answers.
    private static readonly (string Question, string Expected)[] Questions =
    [
        ("can I return my GPS watch?", "Returns Exceptions › GPS watches, navigators and bike computers"),
        ("how much is shipping to Canada?", "Shipping Destinations › Canada"),
        ("how many days do I have to send something back?", "Returns Policy › Return window"),
        ("who pays for the postage when I return an item?", "Returns Policy › Return shipping cost"),
        ("can I return a bike helmet I have worn?", "Returns Exceptions › Helmets"),
        ("do you deliver to a PO box?", "Shipping Destinations › PO boxes and military addresses"),
        ("how much do I need to spend for free shipping?", "Shipping Options and Costs › Free shipping over $75"),
        ("is it too late to cancel my order?", "Order Cancellation and Changes › When you can cancel"),
        ("can I pay with PayPal?", "Payments and Billing › Accepted payment methods"),
        ("do gift cards expire?", "Promotions and Gift Cards › Gift cards"),
        ("how do I measure myself for a jacket?", "Sizing Guide: Jackets and Footwear › Jackets: how to measure"),
        ("what ski length should I get for my height?", "Sizing Guide: Skis and Snowboards › Ski length by height and ability"),
        ("my helmet was in a crash, can I get a replacement?", "Warranty Overview › Crash Replacement for helmets"),
        ("can I swap my boots for a bigger size?", "Exchanges › What you can exchange"),
        ("the sole of my boot is coming off, is that a defect?", "Defects vs Normal Wear › Footwear"),
        ("is there an extra charge to ship a snowboard?", "Oversized Shipping › Surcharge"),
        ("my parcel never arrived", "Order Status and Tracking › Late, lost or damaged parcels"),
        ("how long is the warranty for my brand?", "Warranty Overview › Warranty by brand"),
    ];

    public static bool IsEnabled => GoldenSetFixture.IsEnabled;

    [Fact(SkipUnless = nameof(IsEnabled), Skip = $"Set {GoldenSetFixture.EnvironmentVariable}=1 to run the golden set against a real Ollama embedding model.")]
    public async Task EachQuestionFindsItsExpectedPolicySectionFirst()
    {
        var client = fixture.CreateClient();
        var report = new StringBuilder();
        var misses = new List<string>();

        foreach (var (question, expected) in Questions)
        {
            var response = await client.GetFromJsonAsync<SearchResponse>(
                $"/api/policies/search?q={Uri.EscapeDataString(question)}&top=5", JsonOptions, TestContext.Current.CancellationToken);
            Assert.Equal("semantic", response.Mode);

            var citations = response.Results.Select(r => r.Citation).ToList();
            var rank = citations.IndexOf(expected) + 1;
            var outcome = rank == 1 ? "hit" : rank == 0 ? "MISS (not in top 5)" : $"MISS (rank {rank})";
            report.AppendLine($"{outcome,-20} {question} -> expected '{expected}', first '{citations.FirstOrDefault()}' ({response.Results.FirstOrDefault()?.Score:F3})");
            if (rank != 1)
            {
                misses.Add($"{question} -> expected '{expected}', {outcome}, first was '{citations.FirstOrDefault()}'");
            }
        }

        var hits = Questions.Length - misses.Count;
        report.Insert(0, $"Golden set with {GoldenSetFixture.EmbeddingModel}: {hits}/{Questions.Length} first, {misses.Count} missed{Environment.NewLine}");
        TestContext.Current.TestOutputHelper?.WriteLine(report.ToString());
        TestContext.Current.SendDiagnosticMessage(report.ToString());

        Assert.True(misses.Count == 0, $"{misses.Count} of {Questions.Length} questions missed:{Environment.NewLine}{string.Join(Environment.NewLine, misses)}");
    }

    private sealed record SearchResponse(string Mode, List<SearchResult> Results);

    private sealed record SearchResult(string Citation, double Score);
}
