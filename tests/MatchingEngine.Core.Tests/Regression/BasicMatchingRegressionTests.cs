using MatchingEngine.Core.Domain;

namespace MatchingEngine.Core.Tests.Regression;

public sealed class BasicMatchingRegressionTests
{
    [Fact]
    public void ChallengeSequence_ProducesExpectedTradesAndEmptyBook()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var bid = Submit(engine, new LimitRequest(Side.Buy, PriceOf(1_000), 100));
        var firstAsk = Submit(engine, new LimitRequest(Side.Sell, PriceOf(2_000), 100));
        var secondAsk = Submit(engine, new LimitRequest(Side.Sell, PriceOf(2_000), 200));

        var firstMarket = Submit(engine, new MarketRequest(Side.Buy, 150));
        var secondMarket = Submit(engine, new MarketRequest(Side.Buy, 200));
        var thirdMarket = Submit(engine, new MarketRequest(Side.Sell, 200));

        Assert.Collection(
            firstMarket.Trades,
            trade => AssertTrade(trade, firstMarket.OrderId, firstAsk.OrderId, 2_000, 100),
            trade => AssertTrade(trade, firstMarket.OrderId, secondAsk.OrderId, 2_000, 50));
        AssertTrade(Assert.Single(secondMarket.Trades), secondMarket.OrderId, secondAsk.OrderId, 2_000, 150);
        AssertTrade(Assert.Single(thirdMarket.Trades), bid.OrderId, thirdMarket.OrderId, 1_000, 100);
        Assert.Equal(50, secondMarket.CancelledQuantity);
        Assert.Equal(100, thirdMarket.CancelledQuantity);
        Assert.Empty(engine.GetSnapshot().Bids);
        Assert.Empty(engine.GetSnapshot().Asks);
    }

    private static SubmitResult Submit(
        global::MatchingEngine.Core.MatchingEngine engine,
        SubmitRequest request)
    {
        var result = engine.Submit(request);
        Assert.True(result.IsSuccess);
        return Assert.IsType<SubmitResult>(result.Value);
    }

    private static void AssertTrade(
        Trade trade,
        OrderId expectedBuyOrderId,
        OrderId expectedSellOrderId,
        long expectedMinorUnits,
        long expectedQuantity)
    {
        Assert.Equal(expectedBuyOrderId, trade.BuyOrderId);
        Assert.Equal(expectedSellOrderId, trade.SellOrderId);
        Assert.Equal(PriceOf(expectedMinorUnits), trade.Price);
        Assert.Equal(expectedQuantity, trade.Quantity);
    }

    private static Price PriceOf(long minorUnits)
    {
        Assert.True(Price.TryCreate(minorUnits, out var price));
        return price;
    }
}
