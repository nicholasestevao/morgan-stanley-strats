using MatchingEngine.Core.Book;
using MatchingEngine.Core.Domain;

namespace MatchingEngine.Core.Tests.Matching;

public sealed class LimitOrderTests
{
    [Theory]
    [InlineData(Side.Buy)]
    [InlineData(Side.Sell)]
    public void Submit_PassiveLimitRestsEntireQuantity(Side side)
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var price = PriceOf(1_000);

        var result = Success(engine.Submit(new LimitRequest(side, price, 100)));
        var restingOrder = Assert.Single(side == Side.Buy ? engine.GetSnapshot().Bids : engine.GetSnapshot().Asks);

        Assert.Empty(result.Trades);
        Assert.Equal(0, result.ExecutedQuantity);
        Assert.Equal(100, result.RestingQuantity);
        Assert.Equal(0, result.CancelledQuantity);
        Assert.Equal(new BookOrderSnapshot(result.OrderId, side, OrderType.Limit, price, 100), restingOrder);
    }

    [Fact]
    public void Submit_CrossingBuyMatchesAsksUpToItsLimitAndRestsRemainder()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var firstAskId = engine.AddRestingLimit(Side.Sell, PriceOf(1_000), 30);
        var secondAskId = engine.AddRestingLimit(Side.Sell, PriceOf(1_100), 40);
        var expensiveAskId = engine.AddRestingLimit(Side.Sell, PriceOf(1_200), 50);

        var result = Success(engine.Submit(new LimitRequest(Side.Buy, PriceOf(1_100), 100)));

        Assert.Equal(70, result.ExecutedQuantity);
        Assert.Equal(30, result.RestingQuantity);
        Assert.Equal(0, result.CancelledQuantity);
        Assert.Collection(
            result.Trades,
            trade => AssertTrade(trade, result.OrderId, firstAskId, 1_000, 30),
            trade => AssertTrade(trade, result.OrderId, secondAskId, 1_100, 40));

        var restingBid = Assert.Single(engine.GetSnapshot().Bids);
        Assert.Equal(result.OrderId, restingBid.OrderId);
        Assert.Equal(PriceOf(1_100), restingBid.Price);
        Assert.Equal(30, restingBid.RemainingQuantity);
        Assert.Equal(expensiveAskId, Assert.Single(engine.GetSnapshot().Asks).OrderId);
    }

    [Fact]
    public void Submit_CrossingSellMatchesBidsUpToItsLimitAndRestsRemainder()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var firstBidId = engine.AddRestingLimit(Side.Buy, PriceOf(1_200), 30);
        var secondBidId = engine.AddRestingLimit(Side.Buy, PriceOf(1_100), 40);
        var cheapBidId = engine.AddRestingLimit(Side.Buy, PriceOf(1_000), 50);

        var result = Success(engine.Submit(new LimitRequest(Side.Sell, PriceOf(1_100), 100)));

        Assert.Equal(70, result.ExecutedQuantity);
        Assert.Equal(30, result.RestingQuantity);
        Assert.Collection(
            result.Trades,
            trade => AssertTrade(trade, firstBidId, result.OrderId, 1_200, 30),
            trade => AssertTrade(trade, secondBidId, result.OrderId, 1_100, 40));

        var restingAsk = Assert.Single(engine.GetSnapshot().Asks);
        Assert.Equal(result.OrderId, restingAsk.OrderId);
        Assert.Equal(PriceOf(1_100), restingAsk.Price);
        Assert.Equal(cheapBidId, Assert.Single(engine.GetSnapshot().Bids).OrderId);
    }

    [Fact]
    public void Submit_FullyMatchedLimitDoesNotRest()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        engine.AddRestingLimit(Side.Sell, PriceOf(900), 100);

        var result = Success(engine.Submit(new LimitRequest(Side.Buy, PriceOf(1_000), 100)));

        Assert.Equal(100, result.ExecutedQuantity);
        Assert.Equal(0, result.RestingQuantity);
        Assert.Empty(engine.GetSnapshot().Bids);
        Assert.Empty(engine.GetSnapshot().Asks);
    }

    [Fact]
    public void Submit_BetterPriceBeatsEarlierOrderAtWorsePrice()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var earlierAskId = engine.AddRestingLimit(Side.Sell, PriceOf(1_100), 10);
        var betterAskId = engine.AddRestingLimit(Side.Sell, PriceOf(1_000), 10);

        var result = Success(engine.Submit(new LimitRequest(Side.Buy, PriceOf(1_100), 10)));

        Assert.Equal(betterAskId, Assert.Single(result.Trades).SellOrderId);
        Assert.Equal(earlierAskId, Assert.Single(engine.GetSnapshot().Asks).OrderId);
    }

    [Fact]
    public void Submit_PassiveLimitsPreserveFifoForLaterMatching()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var first = Success(engine.Submit(new LimitRequest(Side.Sell, PriceOf(1_000), 10)));
        var second = Success(engine.Submit(new LimitRequest(Side.Sell, PriceOf(1_000), 10)));

        var market = Success(engine.Submit(new MarketRequest(Side.Buy, 15)));

        Assert.Equal(first.OrderId, market.Trades[0].SellOrderId);
        Assert.Equal(second.OrderId, market.Trades[1].SellOrderId);
        Assert.Equal(5, Assert.Single(engine.GetSnapshot().Asks).RemainingQuantity);
    }

    [Fact]
    public void Submit_RejectsInvalidPriceWithoutConsumingId()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();

        var failure = engine.Submit(new LimitRequest(Side.Buy, default, 10));
        var accepted = Success(engine.Submit(new LimitRequest(Side.Buy, PriceOf(1_000), 10)));

        AssertFailure(failure, EngineErrorCode.InvalidPrice, "price");
        Assert.Equal(new OrderId(1), accepted.OrderId);
    }

    [Fact]
    public void Submit_RejectsLimitWhenPriorityCapacityIsExhaustedWithoutConsumingId()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine(1, ulong.MaxValue);
        Success(engine.Submit(new LimitRequest(Side.Sell, PriceOf(1_000), 10)));

        var failure = engine.Submit(new LimitRequest(Side.Sell, PriceOf(1_000), 10));
        var market = Success(engine.Submit(new MarketRequest(Side.Buy, 10)));

        AssertFailure(failure, EngineErrorCode.PrioritySequenceExhausted, "prioritySequence");
        Assert.Equal(new OrderId(2), market.OrderId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Submit_RejectsNonPositiveQuantityWithoutChangingBook(long quantity)
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var resting = Success(engine.Submit(new LimitRequest(Side.Buy, PriceOf(1_000), 10)));

        var failure = engine.Submit(new LimitRequest(Side.Sell, PriceOf(1_000), quantity));

        AssertFailure(failure, EngineErrorCode.NonPositiveQuantity, "quantity");
        Assert.Equal(resting.OrderId, Assert.Single(engine.GetSnapshot().Bids).OrderId);
    }

    [Fact]
    public void Submit_RejectsInvalidSideWithoutChangingBook()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var resting = Success(engine.Submit(new LimitRequest(Side.Buy, PriceOf(1_000), 10)));

        var failure = engine.Submit(new LimitRequest((Side)99, PriceOf(1_000), 10));

        AssertFailure(failure, EngineErrorCode.InvalidSide, "side");
        Assert.Equal(resting.OrderId, Assert.Single(engine.GetSnapshot().Bids).OrderId);
    }

    private static SubmitResult Success(OperationResult<SubmitResult> result)
    {
        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        return Assert.IsType<SubmitResult>(result.Value);
    }

    private static void AssertFailure(
        OperationResult<SubmitResult> result,
        EngineErrorCode expectedCode,
        string expectedField)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Equal(new EngineError(expectedCode, expectedField), result.Error);
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
