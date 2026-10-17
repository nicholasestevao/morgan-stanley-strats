using MatchingEngine.Core.Book;
using MatchingEngine.Core.Domain;

namespace MatchingEngine.Core.Tests.Matching;

public sealed class PeggedOrderReferenceTests
{
    [Theory]
    [InlineData(PegReference.Bid, Side.Buy, 1_100)]
    [InlineData(PegReference.Offer, Side.Sell, 1_200)]
    public void Submit_PeggedUsesBestFixedLimitReference(
        PegReference reference,
        Side side,
        long expectedMinorUnits)
    {
        var engine = EngineWithFixedBook();

        var result = Success(engine.Submit(new PeggedRequest(reference, side, 25)));
        var snapshot = engine.GetSnapshot();
        var orders = side == Side.Buy ? snapshot.Bids : snapshot.Asks;
        var pegged = Assert.Single(orders, order => order.OrderId == result.OrderId);

        Assert.Equal(OrderType.Pegged, pegged.Type);
        Assert.Equal(reference, pegged.PegReference);
        Assert.Equal(PriceOf(expectedMinorUnits), pegged.Price);
        Assert.Equal(25, result.RestingQuantity);
        Assert.Empty(snapshot.SuspendedOrders);
    }

    [Fact]
    public void Submit_PeggedCanTradeAtInitialReference()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var ask = Success(engine.Submit(new LimitRequest(Side.Sell, PriceOf(1_000), 30)));

        var pegged = Success(engine.Submit(new PeggedRequest(PegReference.Offer, Side.Buy, 20)));

        var trade = Assert.Single(pegged.Trades);
        Assert.Equal(pegged.OrderId, trade.BuyOrderId);
        Assert.Equal(ask.OrderId, trade.SellOrderId);
        Assert.Equal(PriceOf(1_000), trade.Price);
        Assert.Equal(20, trade.Quantity);
        Assert.Equal(20, pegged.ExecutedQuantity);
        Assert.Equal(0, pegged.RestingQuantity);
    }

    [Theory]
    [InlineData(PegReference.Bid, Side.Buy)]
    [InlineData(PegReference.Bid, Side.Sell)]
    [InlineData(PegReference.Offer, Side.Buy)]
    [InlineData(PegReference.Offer, Side.Sell)]
    public void Submit_WithoutReferenceCreatesSuspendedOrder(PegReference reference, Side side)
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();

        var result = Success(engine.Submit(new PeggedRequest(reference, side, 100)));
        var snapshot = engine.GetSnapshot();

        Assert.Empty(result.Trades);
        Assert.Equal(100, result.RestingQuantity);
        Assert.Empty(snapshot.Bids);
        Assert.Empty(snapshot.Asks);
        Assert.Equal(
            new SuspendedOrderSnapshot(result.OrderId, side, reference, 100),
            Assert.Single(snapshot.SuspendedOrders));
    }

    [Fact]
    public void Submit_PeggedOrdersDoNotProvideReferences()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var fixedBid = Success(engine.Submit(new LimitRequest(Side.Buy, PriceOf(1_000), 10)));
        var firstPegged = Success(engine.Submit(new PeggedRequest(PegReference.Bid, Side.Buy, 10)));
        Assert.True(engine.Cancel(new CancelRequest(fixedBid.OrderId)).IsSuccess);

        var secondPegged = Success(engine.Submit(new PeggedRequest(PegReference.Bid, Side.Buy, 10)));
        var snapshot = engine.GetSnapshot();

        Assert.Equal(firstPegged.OrderId, Assert.Single(snapshot.Bids).OrderId);
        Assert.Equal(secondPegged.OrderId, Assert.Single(snapshot.SuspendedOrders).OrderId);
    }

    [Fact]
    public void Cancel_RemovesSuspendedPeggedOrder()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var pegged = Success(engine.Submit(new PeggedRequest(PegReference.Offer, Side.Sell, 50)));

        var cancelled = engine.Cancel(new CancelRequest(pegged.OrderId));

        Assert.True(cancelled.IsSuccess);
        Assert.Equal(50, cancelled.Value?.CancelledQuantity);
        Assert.Empty(engine.GetSnapshot().SuspendedOrders);
    }

    [Fact]
    public void Amend_QuantityPoliciesApplyToSuspendedPeggedOrders()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var first = Success(engine.Submit(new PeggedRequest(PegReference.Bid, Side.Buy, 10)));
        var second = Success(engine.Submit(new PeggedRequest(PegReference.Bid, Side.Buy, 10)));

        Assert.True(engine.Amend(new AmendRequest(first.OrderId, Quantity: 20)).IsSuccess);
        Assert.True(engine.Amend(new AmendRequest(second.OrderId, Quantity: 5)).IsSuccess);

        Assert.Equal(
            [second.OrderId, first.OrderId],
            engine.GetSnapshot().SuspendedOrders.Select(order => order.OrderId));
        Assert.Equal(
            [5L, 20L],
            engine.GetSnapshot().SuspendedOrders.Select(order => order.RemainingQuantity));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Amend_RejectsManualPriceForPeggedOrder(bool suspended)
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        if (!suspended)
            Success(engine.Submit(new LimitRequest(Side.Buy, PriceOf(1_000), 10)));
        var pegged = Success(engine.Submit(new PeggedRequest(PegReference.Bid, Side.Buy, 10)));

        var result = engine.Amend(new AmendRequest(pegged.OrderId, PriceOf(1_100)));

        Assert.False(result.IsSuccess);
        Assert.Equal(EngineErrorCode.PriceAmendmentNotAllowed, result.Error?.Code);
        var snapshot = engine.GetSnapshot();
        if (suspended) Assert.Equal(pegged.OrderId, Assert.Single(snapshot.SuspendedOrders).OrderId);
        else Assert.Equal(PriceOf(1_000), snapshot.Bids.Single(order => order.OrderId == pegged.OrderId).Price);
    }

    [Fact]
    public void Submit_RejectsInvalidFieldsWithoutConsumingId()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();

        AssertFailure(
            engine.Submit(new PeggedRequest((PegReference)99, Side.Buy, 10)),
            EngineErrorCode.InvalidPegReference,
            "reference");
        AssertFailure(
            engine.Submit(new PeggedRequest(PegReference.Bid, (Side)99, 10)),
            EngineErrorCode.InvalidSide,
            "side");
        AssertFailure(
            engine.Submit(new PeggedRequest(PegReference.Bid, Side.Buy, 0)),
            EngineErrorCode.NonPositiveQuantity,
            "quantity");

        var accepted = Success(engine.Submit(new PeggedRequest(PegReference.Bid, Side.Buy, 10)));
        Assert.Equal(new OrderId(1), accepted.OrderId);
    }

    [Fact]
    public void Submit_SuspendedPeggedConsumesPriorityCapacity()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine(1, ulong.MaxValue);

        var accepted = engine.Submit(new PeggedRequest(PegReference.Bid, Side.Buy, 10));
        var rejected = engine.Submit(new PeggedRequest(PegReference.Bid, Side.Buy, 10));

        Assert.True(accepted.IsSuccess);
        AssertFailure(rejected, EngineErrorCode.PrioritySequenceExhausted, "prioritySequence");
    }

    private static global::MatchingEngine.Core.MatchingEngine EngineWithFixedBook()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        Success(engine.Submit(new LimitRequest(Side.Buy, PriceOf(1_000), 10)));
        Success(engine.Submit(new LimitRequest(Side.Buy, PriceOf(1_100), 10)));
        Success(engine.Submit(new LimitRequest(Side.Sell, PriceOf(1_200), 10)));
        Success(engine.Submit(new LimitRequest(Side.Sell, PriceOf(1_300), 10)));
        return engine;
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

    private static Price PriceOf(long minorUnits)
    {
        Assert.True(Price.TryCreate(minorUnits, out var price));
        return price;
    }
}
