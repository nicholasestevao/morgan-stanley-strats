using MatchingEngine.Core.Domain;

namespace MatchingEngine.Core.Tests.Matching;

public sealed class MarketOrderTests
{
    [Theory]
    [InlineData(Side.Buy)]
    [InlineData(Side.Sell)]
    public void Submit_MarketOnEmptyBookCancelsEntireQuantity(Side side)
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();

        var result = Success(engine.Submit(new MarketRequest(side, 100)));

        Assert.Empty(result.Trades);
        Assert.Equal(0, result.ExecutedQuantity);
        Assert.Equal(0, result.RestingQuantity);
        Assert.Equal(100, result.CancelledQuantity);
        Assert.Empty(engine.GetSnapshot().Bids);
        Assert.Empty(engine.GetSnapshot().Asks);
    }

    [Fact]
    public void Submit_MarketBuyConsumesLowestAsksInPriceTimeOrder()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var expensiveAskId = engine.AddRestingLimit(Side.Sell, PriceOf(1_200), 40);
        var firstBestAskId = engine.AddRestingLimit(Side.Sell, PriceOf(1_000), 30);
        var secondBestAskId = engine.AddRestingLimit(Side.Sell, PriceOf(1_000), 50);

        var result = Success(engine.Submit(new MarketRequest(Side.Buy, 100)));

        Assert.Equal(100, result.ExecutedQuantity);
        Assert.Equal(0, result.CancelledQuantity);
        Assert.Collection(
            result.Trades,
            trade => AssertTrade(trade, result.OrderId, firstBestAskId, 1_000, 30),
            trade => AssertTrade(trade, result.OrderId, secondBestAskId, 1_000, 50),
            trade => AssertTrade(trade, result.OrderId, expensiveAskId, 1_200, 20));
        Assert.Equal(20, Assert.Single(engine.GetSnapshot().Asks).RemainingQuantity);
    }

    [Fact]
    public void Submit_MarketSellConsumesHighestBidsInPriceTimeOrder()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var lowerBidId = engine.AddRestingLimit(Side.Buy, PriceOf(900), 40);
        var firstBestBidId = engine.AddRestingLimit(Side.Buy, PriceOf(1_100), 30);
        var secondBestBidId = engine.AddRestingLimit(Side.Buy, PriceOf(1_100), 50);

        var result = Success(engine.Submit(new MarketRequest(Side.Sell, 100)));

        Assert.Equal(100, result.ExecutedQuantity);
        Assert.Equal(0, result.CancelledQuantity);
        Assert.Collection(
            result.Trades,
            trade => AssertTrade(trade, firstBestBidId, result.OrderId, 1_100, 30),
            trade => AssertTrade(trade, secondBestBidId, result.OrderId, 1_100, 50),
            trade => AssertTrade(trade, lowerBidId, result.OrderId, 900, 20));
        Assert.Equal(20, Assert.Single(engine.GetSnapshot().Bids).RemainingQuantity);
    }

    [Fact]
    public void Submit_PartialFillPreservesRestingOrderPriority()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var firstAskId = engine.AddRestingLimit(Side.Sell, PriceOf(1_000), 100);
        engine.AddRestingLimit(Side.Sell, PriceOf(1_000), 100);

        Success(engine.Submit(new MarketRequest(Side.Buy, 40)));
        var secondResult = Success(engine.Submit(new MarketRequest(Side.Buy, 70)));

        Assert.Equal(firstAskId, secondResult.Trades[0].SellOrderId);
        Assert.Equal(60, secondResult.Trades[0].Quantity);
        Assert.Equal(2, secondResult.Trades.Length);
    }

    [Fact]
    public void Submit_CancelsQuantityBeyondAvailableLiquidity()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        engine.AddRestingLimit(Side.Sell, PriceOf(1_000), 60);

        var result = Success(engine.Submit(new MarketRequest(Side.Buy, 100)));

        Assert.Equal(60, result.ExecutedQuantity);
        Assert.Equal(40, result.CancelledQuantity);
        Assert.Equal(100, result.ExecutedQuantity + result.RestingQuantity + result.CancelledQuantity);
        Assert.Empty(engine.GetSnapshot().Asks);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Submit_RejectsNonPositiveQuantityWithoutConsumingId(long quantity)
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();

        var failure = engine.Submit(new MarketRequest(Side.Buy, quantity));
        var accepted = Success(engine.Submit(new MarketRequest(Side.Buy, 1)));

        AssertFailure(failure, EngineErrorCode.NonPositiveQuantity, "quantity");
        Assert.Equal(new OrderId(1), accepted.OrderId);
    }

    [Fact]
    public void Submit_RejectsInvalidSideWithoutChangingBook()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var restingId = engine.AddRestingLimit(Side.Sell, PriceOf(1_000), 10);

        var failure = engine.Submit(new MarketRequest((Side)99, 10));

        AssertFailure(failure, EngineErrorCode.InvalidSide, "side");
        Assert.Equal(restingId, Assert.Single(engine.GetSnapshot().Asks).OrderId);
    }

    [Fact]
    public void Submit_RejectsNullRequest()
    {
        var result = new global::MatchingEngine.Core.MatchingEngine().Submit(null);

        AssertFailure(result, EngineErrorCode.RequestRequired, "request");
    }

    [Fact]
    public void Submit_RejectsOrderAfterIdCapacityIsExhausted()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine(ulong.MaxValue, 1);

        var lastAccepted = Success(engine.Submit(new MarketRequest(Side.Buy, 1)));
        var failure = engine.Submit(new MarketRequest(Side.Buy, 1));

        Assert.Equal(new OrderId(ulong.MaxValue), lastAccepted.OrderId);
        AssertFailure(failure, EngineErrorCode.OrderIdExhausted, "orderId");
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
