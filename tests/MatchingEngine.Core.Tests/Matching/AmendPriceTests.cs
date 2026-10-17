using MatchingEngine.Core.Book;
using MatchingEngine.Core.Domain;

namespace MatchingEngine.Core.Tests.Matching;

public sealed class AmendPriceTests
{
    [Fact]
    public void Amend_ChangesPriceAndMovesOrderBehindExistingTargetLevel()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var existing = Submit(engine, new LimitRequest(Side.Buy, PriceOf(1_000), 10));
        var amended = Submit(engine, new LimitRequest(Side.Buy, PriceOf(900), 10));

        Success(engine.Amend(new AmendRequest(amended.OrderId, PriceOf(1_000))));
        var market = Submit(engine, new MarketRequest(Side.Sell, 10));

        Assert.Equal(existing.OrderId, Assert.Single(market.Trades).BuyOrderId);
        var resting = Assert.Single(engine.GetSnapshot().Bids);
        Assert.Equal(amended.OrderId, resting.OrderId);
        Assert.Equal(PriceOf(1_000), resting.Price);
    }

    [Fact]
    public void Amend_SamePricePreservesPriority()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var first = Submit(engine, new LimitRequest(Side.Buy, PriceOf(1_000), 10));
        Submit(engine, new LimitRequest(Side.Buy, PriceOf(1_000), 10));

        Success(engine.Amend(new AmendRequest(first.OrderId, PriceOf(1_000))));
        var market = Submit(engine, new MarketRequest(Side.Sell, 10));

        Assert.Equal(first.OrderId, Assert.Single(market.Trades).BuyOrderId);
    }

    [Fact]
    public void Amend_CrossingBuyMatchesAtRestingPricesAndRestsRemainderWithSameId()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var firstAsk = Submit(engine, new LimitRequest(Side.Sell, PriceOf(1_000), 30));
        var secondAsk = Submit(engine, new LimitRequest(Side.Sell, PriceOf(1_100), 40));
        var expensiveAsk = Submit(engine, new LimitRequest(Side.Sell, PriceOf(1_200), 50));
        var bid = Submit(engine, new LimitRequest(Side.Buy, PriceOf(900), 100));

        var result = Success(engine.Amend(new AmendRequest(bid.OrderId, PriceOf(1_100))));

        Assert.Equal(bid.OrderId, result.OrderId);
        Assert.Equal(70, result.ExecutedQuantity);
        Assert.Equal(30, result.RestingQuantity);
        Assert.Collection(
            result.Trades,
            trade => AssertTrade(trade, bid.OrderId, firstAsk.OrderId, 1_000, 30),
            trade => AssertTrade(trade, bid.OrderId, secondAsk.OrderId, 1_100, 40));
        Assert.Equal(
            new BookOrderSnapshot(bid.OrderId, Side.Buy, OrderType.Limit, PriceOf(1_100), 30),
            Assert.Single(engine.GetSnapshot().Bids));
        Assert.Equal(expensiveAsk.OrderId, Assert.Single(engine.GetSnapshot().Asks).OrderId);
    }

    [Fact]
    public void Amend_CrossingSellCanExecuteEntireOrder()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var bid = Submit(engine, new LimitRequest(Side.Buy, PriceOf(1_200), 25));
        var ask = Submit(engine, new LimitRequest(Side.Sell, PriceOf(1_300), 25));

        var result = Success(engine.Amend(new AmendRequest(ask.OrderId, PriceOf(1_100))));

        Assert.Equal(25, result.ExecutedQuantity);
        Assert.Equal(0, result.RestingQuantity);
        AssertTrade(Assert.Single(result.Trades), bid.OrderId, ask.OrderId, 1_200, 25);
        Assert.Empty(engine.GetSnapshot().Bids);
        Assert.Empty(engine.GetSnapshot().Asks);
        AssertFailure(
            engine.Amend(new AmendRequest(ask.OrderId, PriceOf(1_000))),
            EngineErrorCode.OrderNotFound,
            "orderId");
    }

    [Fact]
    public void Amend_CombinedPriceAndQuantityUsesNewRemainingQuantity()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        Submit(engine, new LimitRequest(Side.Sell, PriceOf(1_000), 30));
        var bid = Submit(engine, new LimitRequest(Side.Buy, PriceOf(900), 100));

        var result = Success(engine.Amend(new AmendRequest(bid.OrderId, PriceOf(1_000), 50)));

        Assert.Equal(30, result.ExecutedQuantity);
        Assert.Equal(20, result.RestingQuantity);
        Assert.Equal(20, Assert.Single(engine.GetSnapshot().Bids).RemainingQuantity);
    }

    [Fact]
    public void Amend_RejectsInvalidPriceWithoutChangingOrder()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var resting = Submit(engine, new LimitRequest(Side.Sell, PriceOf(1_000), 10));

        var result = engine.Amend(new AmendRequest(resting.OrderId, default(Price)));

        AssertFailure(result, EngineErrorCode.InvalidPrice, "price");
        Assert.Equal(
            new BookOrderSnapshot(resting.OrderId, Side.Sell, OrderType.Limit, PriceOf(1_000), 10),
            Assert.Single(engine.GetSnapshot().Asks));
    }

    [Fact]
    public void Amend_InvalidCombinedFieldPreservesPriceQuantityAndPriority()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var first = Submit(engine, new LimitRequest(Side.Buy, PriceOf(1_000), 10));
        Submit(engine, new LimitRequest(Side.Buy, PriceOf(1_000), 10));

        var result = engine.Amend(new AmendRequest(first.OrderId, PriceOf(1_100), 0));
        var market = Submit(engine, new MarketRequest(Side.Sell, 10));

        AssertFailure(result, EngineErrorCode.NonPositiveQuantity, "quantity");
        Assert.Equal(first.OrderId, Assert.Single(market.Trades).BuyOrderId);
        var unchanged = Assert.Single(engine.GetSnapshot().Bids);
        Assert.Equal(PriceOf(1_000), unchanged.Price);
        Assert.Equal(10, unchanged.RemainingQuantity);
    }

    [Fact]
    public void Amend_RejectsMissingOrderWithoutChangingBook()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var resting = Submit(engine, new LimitRequest(Side.Buy, PriceOf(1_000), 10));

        var result = engine.Amend(new AmendRequest(new OrderId(999), PriceOf(1_100)));

        AssertFailure(result, EngineErrorCode.OrderNotFound, "orderId");
        Assert.Equal(resting.OrderId, Assert.Single(engine.GetSnapshot().Bids).OrderId);
    }

    [Fact]
    public void Amend_PriceChangeFailsWithoutMutationWhenPriorityCapacityIsExhausted()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine(1, ulong.MaxValue);
        var resting = Submit(engine, new LimitRequest(Side.Buy, PriceOf(1_000), 10));

        var result = engine.Amend(new AmendRequest(resting.OrderId, PriceOf(1_100)));

        AssertFailure(result, EngineErrorCode.PrioritySequenceExhausted, "prioritySequence");
        var unchanged = Assert.Single(engine.GetSnapshot().Bids);
        Assert.Equal(PriceOf(1_000), unchanged.Price);
        Assert.Equal(10, unchanged.RemainingQuantity);
    }

    private static SubmitResult Submit(
        global::MatchingEngine.Core.MatchingEngine engine,
        SubmitRequest request)
    {
        var result = engine.Submit(request);
        Assert.True(result.IsSuccess);
        return Assert.IsType<SubmitResult>(result.Value);
    }

    private static AmendResult Success(OperationResult<AmendResult> result)
    {
        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        return Assert.IsType<AmendResult>(result.Value);
    }

    private static void AssertFailure(
        OperationResult<AmendResult> result,
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
