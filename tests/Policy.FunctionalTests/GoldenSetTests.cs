namespace eShop.Policy.FunctionalTests;

/// <summary>
/// Retrieval quality on demand: does semantic search with a real embedding model put the right Policy Section among
/// the results the assistant reads?
/// </summary>
/// <remarks>
/// Skipped unless <c>ESHOP_POLICY_GOLDEN_SET=1</c>. Run it with
/// <c>$env:ESHOP_POLICY_GOLDEN_SET=1; dotnet test --project tests/Policy.FunctionalTests -- --filter-class "*GoldenSetTests"</c>.
/// Each question is its own test case. It fails, listing the top 5 with scores, when the expected section is not in
/// them, and passes with a warning naming its rank when the section is there but not first.
/// </remarks>
public sealed class GoldenSetTests(GoldenSetFixture fixture) : IClassFixture<GoldenSetFixture>
{
    // Written from the Policy files before the first run, and not tuned to the model's answers.
    public static TheoryData<string, string> Questions => new()
    {
        { "can I return my GPS watch?", "Returns Exceptions › GPS watches, navigators and bike computers" },
        { "how much is shipping to Canada?", "Shipping Destinations › Canada" },
        { "how many days do I have to send something back?", "Returns Policy › Return window" },
        { "who pays for the postage when I return an item?", "Returns Policy › Return shipping cost" },
        { "can I return a bike helmet I have worn?", "Returns Exceptions › Helmets" },
        { "do you deliver to a PO box?", "Shipping Destinations › PO boxes and military addresses" },
        { "how much do I need to spend for free shipping?", "Shipping Options and Costs › Free shipping over $75" },
        { "is it too late to cancel my order?", "Order Cancellation and Changes › When you can cancel" },
        { "can I pay with PayPal?", "Payments and Billing › Accepted payment methods" },
        { "do gift cards expire?", "Promotions and Gift Cards › Gift cards" },
        { "how do I measure myself for a jacket?", "Sizing Guide: Jackets and Footwear › Jackets: how to measure" },
        { "what ski length should I get for my height?", "Sizing Guide: Skis and Snowboards › Ski length by height and ability" },
        { "my helmet was in a crash, can I get a replacement?", "Warranty Overview › Crash Replacement for helmets" },
        { "can I swap my boots for a bigger size?", "Exchanges › What you can exchange" },
        { "the sole of my boot is coming off, is that a defect?", "Defects vs Normal Wear › Footwear" },
        { "is there an extra charge to ship a snowboard?", "Oversized Shipping › Surcharge" },
        { "my parcel never arrived", "Order Status and Tracking › Late, lost or damaged parcels" },
        { "how long is the warranty for my brand?", "Warranty Overview › Warranty by brand" },
    };

    public static bool IsEnabled => GoldenSetFixture.IsEnabled;

    // What search_policies asks for (AssistantTools.PolicySearchTop): the model reads all of these before it answers,
    // so a section anywhere in them can be cited.
    private const int ResultsTheAssistantSees = 5;

    [Theory(SkipUnless = nameof(IsEnabled), Skip = $"Set {GoldenSetFixture.EnvironmentVariable}=1 to run the golden set against a real Ollama embedding model.")]
    [MemberData(nameof(Questions))]
    public async Task ExpectedPolicySectionIsAmongTheResultsTheAssistantSees(string question, string expected)
    {
        var response = await fixture.CreateClient().SearchPoliciesAsync(question, ResultsTheAssistantSees);
        Assert.Equal("semantic", response.Mode);

        var rank = response.Results.Select(r => r.Citation).ToList().IndexOf(expected) + 1;
        var ranking = string.Join(Environment.NewLine, response.Results.Select((r, i) => $"{i + 1}. {r.Citation} ({r.Score:F3})"));

        // Not first is still a pass, but worth seeing: it is how close the embedding model came.
        var outcome = rank switch { 0 => $"not in the top {ResultsTheAssistantSees}", 1 => "first", _ => $"rank {rank}, not first" };
        TestContext.Current.TestOutputHelper?.WriteLine($"{GoldenSetFixture.EmbeddingModel}: {outcome}{Environment.NewLine}{ranking}");

        Assert.True(rank > 0, $"Expected '{expected}' in the top {ResultsTheAssistantSees}, but it was not:{Environment.NewLine}{ranking}");
    }
}
