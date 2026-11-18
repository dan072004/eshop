using eShop.WebApp.Chatbot;

namespace eShop.WebApp.UnitTests;

[TestClass]
public class ClaudePricesTests
{
    private const string DatedHaiku = "claude-haiku-4-5-20251001";

    [TestMethod]
    public void PricesInputOutputCacheReadAndCacheWriteTokensEachAtTheirOwnRate()
    {
        Assert.AreEqual(2m, ClaudePrices.EstimateCost(DatedHaiku, new ClaudeTokenCounts(Input: 2_000_000)));
        Assert.AreEqual(1.5m, ClaudePrices.EstimateCost(DatedHaiku, new ClaudeTokenCounts(Output: 300_000)));
        Assert.AreEqual(0.2m, ClaudePrices.EstimateCost(DatedHaiku, new ClaudeTokenCounts(CacheRead: 2_000_000)));
        Assert.AreEqual(0.5m, ClaudePrices.EstimateCost(DatedHaiku, new ClaudeTokenCounts(CacheWrite: 400_000)));
    }

    [TestMethod]
    public void AddsUpEveryKindOfTokenOnTheJudgeModel()
    {
        var tokens = new ClaudeTokenCounts(Input: 1_000_000, Output: 100_000, CacheRead: 1_000_000, CacheWrite: 1_000_000);

        Assert.AreEqual(5.7m, ClaudePrices.EstimateCost("claude-sonnet-5-5", tokens));
    }

    [TestMethod]
    public void AModelWithNoListedPriceHasNoCost()
    {
        Assert.IsNull(ClaudePrices.EstimateCost("llama3.2", new ClaudeTokenCounts(Input: 1_000, Output: 1_000)));
    }
}
