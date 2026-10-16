using MatchingEngine.Core.Domain;

namespace MatchingEngine.Core.Tests.Matching;

public sealed class CancelOrderTests
{
    [Theory]
    [InlineData(Side.Buy)]
    [InlineData(Side.Sell)]
    public void Cancel_RemovesRestingOrderAndReturnsItsQuantity(Side side)
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var submitted = Submit(engine, new LimitRequest(side, PriceOf(1_000), 100));

        var cancelled = Success(engine.Cancel(new CancelRequest(submitted.OrderId)));

        Assert.Equal(submitted.OrderId, cancelled.OrderId);
        Assert.Equal(100, cancelled.CancelledQuantity);
        Assert.Empty(engine.GetSnapshot().Bids);
        Assert.Empty(engine.GetSnapshot().Asks);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Cancel_RemovesFirstMiddleOrLastOrderAtPriceLevel(int cancelledIndex)
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var orders = Enumerable.Range(0, 3)
            .Select(_ => Submit(engine, new LimitRequest(Side.Buy, PriceOf(1_000), 10)).OrderId)
            .ToArray();

        Success(engine.Cancel(new CancelRequest(orders[cancelledIndex])));

        var expected = orders.Where((_, index) => index != cancelledIndex);
        Assert.Equal(expected, engine.GetSnapshot().Bids.Select(order => order.OrderId));
    }

    [Fact]
    public void Cancel_RemovesLastOrderAtBestPriceAndPromotesNextLevel()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var best = Submit(engine, new LimitRequest(Side.Buy, PriceOf(1_100), 10));
        var next = Submit(engine, new LimitRequest(Side.Buy, PriceOf(1_000), 10));

        Success(engine.Cancel(new CancelRequest(best.OrderId)));

        Assert.Equal(next.OrderId, Assert.Single(engine.GetSnapshot().Bids).OrderId);
    }

    [Fact]
    public void Cancel_ReturnsRemainingQuantityAfterPartialExecution()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var resting = Submit(engine, new LimitRequest(Side.Sell, PriceOf(1_000), 100));
        Submit(engine, new MarketRequest(Side.Buy, 40));

        var cancelled = Success(engine.Cancel(new CancelRequest(resting.OrderId)));

        Assert.Equal(60, cancelled.CancelledQuantity);
        Assert.Empty(engine.GetSnapshot().Asks);
    }

    [Fact]
    public void Cancel_RejectsUnknownAlreadyCancelledOrExecutedOrderWithoutMutation()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var first = Submit(engine, new LimitRequest(Side.Buy, PriceOf(1_000), 10));
        var second = Submit(engine, new LimitRequest(Side.Buy, PriceOf(900), 10));
        Success(engine.Cancel(new CancelRequest(first.OrderId)));

        AssertNotFound(engine.Cancel(new CancelRequest(first.OrderId)));
        AssertNotFound(engine.Cancel(new CancelRequest(new OrderId(999))));

        var snapshot = engine.GetSnapshot();
        Assert.Equal(second.OrderId, Assert.Single(snapshot.Bids).OrderId);

        Submit(engine, new MarketRequest(Side.Sell, 10));
        AssertNotFound(engine.Cancel(new CancelRequest(second.OrderId)));
    }

    [Fact]
    public void Cancel_RejectsNullRequestWithoutChangingBook()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var resting = Submit(engine, new LimitRequest(Side.Buy, PriceOf(1_000), 10));

        var result = engine.Cancel(null);

        Assert.False(result.IsSuccess);
        Assert.Equal(EngineErrorCode.RequestRequired, result.Error?.Code);
        Assert.Equal(resting.OrderId, Assert.Single(engine.GetSnapshot().Bids).OrderId);
    }

    [Fact]
    public void Cancel_DoesNotReuseOrderId()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var first = Submit(engine, new LimitRequest(Side.Buy, PriceOf(1_000), 10));
        Success(engine.Cancel(new CancelRequest(first.OrderId)));

        var second = Submit(engine, new LimitRequest(Side.Buy, PriceOf(1_000), 10));

        Assert.Equal(new OrderId(first.OrderId.Value + 1), second.OrderId);
    }

    private static SubmitResult Submit(
        global::MatchingEngine.Core.MatchingEngine engine,
        SubmitRequest request)
    {
        var result = engine.Submit(request);
        Assert.True(result.IsSuccess);
        return Assert.IsType<SubmitResult>(result.Value);
    }

    private static CancelResult Success(OperationResult<CancelResult> result)
    {
        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        return Assert.IsType<CancelResult>(result.Value);
    }

    private static void AssertNotFound(OperationResult<CancelResult> result)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Equal(new EngineError(EngineErrorCode.OrderNotFound, "orderId"), result.Error);
    }

    private static Price PriceOf(long minorUnits)
    {
        Assert.True(Price.TryCreate(minorUnits, out var price));
        return price;
    }
}
