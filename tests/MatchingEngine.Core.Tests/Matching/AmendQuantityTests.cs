using MatchingEngine.Core.Domain;

namespace MatchingEngine.Core.Tests.Matching;

public sealed class AmendQuantityTests
{
    [Fact]
    public void Amend_ReducesRemainingQuantityWithoutLosingPriority()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var first = Submit(engine, new LimitRequest(Side.Sell, PriceOf(1_000), 100));
        var second = Submit(engine, new LimitRequest(Side.Sell, PriceOf(1_000), 100));

        var amended = Success(engine.Amend(new AmendRequest(first.OrderId, Quantity: 50)));
        var market = Submit(engine, new MarketRequest(Side.Buy, 75));

        Assert.Equal(first.OrderId, amended.OrderId);
        Assert.Equal(50, amended.RestingQuantity);
        Assert.Empty(amended.Trades);
        Assert.Collection(
            market.Trades,
            trade =>
            {
                Assert.Equal(first.OrderId, trade.SellOrderId);
                Assert.Equal(50, trade.Quantity);
            },
            trade =>
            {
                Assert.Equal(second.OrderId, trade.SellOrderId);
                Assert.Equal(25, trade.Quantity);
            });
        Assert.Equal(75, Assert.Single(engine.GetSnapshot().Asks).RemainingQuantity);
    }

    [Fact]
    public void Amend_IncreasesRemainingQuantityAndMovesOrderBehindItsPriceLevel()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var first = Submit(engine, new LimitRequest(Side.Sell, PriceOf(1_000), 10));
        var second = Submit(engine, new LimitRequest(Side.Sell, PriceOf(1_000), 10));

        Success(engine.Amend(new AmendRequest(first.OrderId, Quantity: 20)));
        var market = Submit(engine, new MarketRequest(Side.Buy, 10));

        Assert.Equal(second.OrderId, Assert.Single(market.Trades).SellOrderId);
        Assert.Equal(first.OrderId, Assert.Single(engine.GetSnapshot().Asks).OrderId);
    }

    [Fact]
    public void Amend_SameQuantityPreservesPriority()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var first = Submit(engine, new LimitRequest(Side.Buy, PriceOf(1_000), 10));
        Submit(engine, new LimitRequest(Side.Buy, PriceOf(1_000), 10));

        Success(engine.Amend(new AmendRequest(first.OrderId, Quantity: 10)));
        var market = Submit(engine, new MarketRequest(Side.Sell, 10));

        Assert.Equal(first.OrderId, Assert.Single(market.Trades).BuyOrderId);
    }

    [Fact]
    public void Amend_ReplacesRemainingQuantityAfterPartialExecution()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var resting = Submit(engine, new LimitRequest(Side.Sell, PriceOf(1_000), 100));
        Submit(engine, new MarketRequest(Side.Buy, 40));

        var amended = Success(engine.Amend(new AmendRequest(resting.OrderId, Quantity: 80)));
        var snapshot = Assert.Single(engine.GetSnapshot().Asks);

        Assert.Equal(resting.OrderId, amended.OrderId);
        Assert.Equal(0, amended.ExecutedQuantity);
        Assert.Equal(80, amended.RestingQuantity);
        Assert.Equal(80, snapshot.RemainingQuantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Amend_RejectsNonPositiveQuantityWithoutChangingOrder(long quantity)
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var resting = Submit(engine, new LimitRequest(Side.Buy, PriceOf(1_000), 100));

        var result = engine.Amend(new AmendRequest(resting.OrderId, Quantity: quantity));

        AssertFailure(result, EngineErrorCode.NonPositiveQuantity, "quantity");
        var unchanged = Assert.Single(engine.GetSnapshot().Bids);
        Assert.Equal(resting.OrderId, unchanged.OrderId);
        Assert.Equal(100, unchanged.RemainingQuantity);
    }

    [Fact]
    public void Amend_RejectsMissingCancelledOrExecutedOrder()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var cancelled = Submit(engine, new LimitRequest(Side.Buy, PriceOf(1_000), 10));
        var executed = Submit(engine, new LimitRequest(Side.Buy, PriceOf(900), 10));
        Assert.True(engine.Cancel(new CancelRequest(cancelled.OrderId)).IsSuccess);
        Submit(engine, new MarketRequest(Side.Sell, 10));

        AssertNotFound(engine.Amend(new AmendRequest(new OrderId(999), Quantity: 20)));
        AssertNotFound(engine.Amend(new AmendRequest(cancelled.OrderId, Quantity: 20)));
        AssertNotFound(engine.Amend(new AmendRequest(executed.OrderId, Quantity: 20)));
    }

    [Fact]
    public void Amend_RejectsNullOrEmptyRequestWithoutChangingOrder()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var resting = Submit(engine, new LimitRequest(Side.Buy, PriceOf(1_000), 10));

        AssertFailure(engine.Amend(null), EngineErrorCode.RequestRequired, "request");
        AssertFailure(
            engine.Amend(new AmendRequest(resting.OrderId)),
            EngineErrorCode.UnsupportedRequest,
            "request");
        Assert.Equal(resting.OrderId, Assert.Single(engine.GetSnapshot().Bids).OrderId);
    }

    [Fact]
    public void Amend_IncreaseFailsWithoutMutationWhenPriorityCapacityIsExhausted()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine(1, ulong.MaxValue);
        var resting = Submit(engine, new LimitRequest(Side.Buy, PriceOf(1_000), 10));

        var failure = engine.Amend(new AmendRequest(resting.OrderId, Quantity: 20));
        var reduction = Success(engine.Amend(new AmendRequest(resting.OrderId, Quantity: 5)));

        AssertFailure(failure, EngineErrorCode.PrioritySequenceExhausted, "prioritySequence");
        Assert.Equal(5, reduction.RestingQuantity);
        Assert.Equal(5, Assert.Single(engine.GetSnapshot().Bids).RemainingQuantity);
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

    private static void AssertNotFound(OperationResult<AmendResult> result) =>
        AssertFailure(result, EngineErrorCode.OrderNotFound, "orderId");

    private static void AssertFailure(
        OperationResult<AmendResult> result,
        EngineErrorCode expectedCode,
        string expectedField)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Equal(new EngineError(expectedCode, expectedField), result.Error);
    }

    private static Price PriceOf(long minorUnits)
    {
        Assert.True(Price.TryCreate(minorUnits, out var price));
        return price;
    }
}
