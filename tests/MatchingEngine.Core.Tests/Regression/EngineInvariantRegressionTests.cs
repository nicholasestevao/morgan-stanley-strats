using MatchingEngine.Core.Book;
using MatchingEngine.Core.Domain;

namespace MatchingEngine.Core.Tests.Regression;

public sealed class EngineInvariantRegressionTests
{
    [Fact]
    public void MixedOperations_PreserveEngineInvariantsAfterEveryStep()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var fixedBid = Submit(engine, new LimitRequest(Side.Buy, PriceOf(1_000), 20));
        var fixedAsk = Submit(engine, new LimitRequest(Side.Sell, PriceOf(1_200), 20));
        var peggedBid = Submit(engine, new PeggedRequest(PegReference.Bid, Side.Buy, 8));
        var peggedOffer = Submit(engine, new PeggedRequest(PegReference.Offer, Side.Sell, 7));

        Amend(engine, new AmendRequest(fixedBid.OrderId, Price: PriceOf(1_100)), 20);
        var market = Submit(engine, new MarketRequest(Side.Sell, 5));
        Assert.Equal(peggedBid.OrderId, Assert.Single(market.Trades).BuyOrderId);

        Amend(engine, new AmendRequest(peggedOffer.OrderId, Quantity: 10), 10);
        Cancel(engine, fixedAsk.OrderId);
        Assert.Equal(peggedOffer.OrderId, Assert.Single(engine.GetSnapshot().SuspendedOrders).OrderId);

        var beforeFailure = engine.GetSnapshot();
        var rejected = engine.Amend(new AmendRequest(peggedOffer.OrderId, Price: PriceOf(1_300)));
        Assert.False(rejected.IsSuccess);
        Assert.Equal(EngineErrorCode.PriceAmendmentNotAllowed, rejected.Error?.Code);
        AssertSnapshotEqual(beforeFailure, engine.GetSnapshot());
        engine.ValidateInvariants();

        var crossingLimit = Submit(engine, new LimitRequest(Side.Sell, PriceOf(1_100), 30));
        Assert.Equal(23, crossingLimit.ExecutedQuantity);
        Assert.Equal(7, crossingLimit.RestingQuantity);
        Assert.Equal(
            [peggedOffer.OrderId, crossingLimit.OrderId],
            engine.GetSnapshot().Asks.Select(order => order.OrderId));

        Cancel(engine, crossingLimit.OrderId);
        Cancel(engine, peggedOffer.OrderId);
        Assert.Empty(engine.GetSnapshot().SuspendedOrders);
        engine.ValidateInvariants();
    }

    private static SubmitResult Submit(
        global::MatchingEngine.Core.MatchingEngine engine,
        SubmitRequest request)
    {
        var result = engine.Submit(request);
        Assert.True(result.IsSuccess);
        var submission = Assert.IsType<SubmitResult>(result.Value);
        var requestedQuantity = request switch
        {
            LimitRequest limit => limit.Quantity,
            MarketRequest market => market.Quantity,
            PeggedRequest pegged => pegged.Quantity,
            _ => throw new InvalidOperationException("Unsupported test request."),
        };
        Assert.Equal(
            requestedQuantity,
            submission.ExecutedQuantity + submission.RestingQuantity + submission.CancelledQuantity);
        AssertPositiveTrades(submission.Trades);
        engine.ValidateInvariants();
        return submission;
    }

    private static AmendResult Amend(
        global::MatchingEngine.Core.MatchingEngine engine,
        AmendRequest request,
        long expectedQuantity)
    {
        var result = engine.Amend(request);
        Assert.True(result.IsSuccess);
        var amendment = Assert.IsType<AmendResult>(result.Value);
        Assert.Equal(expectedQuantity, amendment.ExecutedQuantity + amendment.RestingQuantity);
        AssertPositiveTrades(amendment.Trades);
        engine.ValidateInvariants();
        return amendment;
    }

    private static CancelResult Cancel(
        global::MatchingEngine.Core.MatchingEngine engine,
        OrderId orderId)
    {
        var result = engine.Cancel(new CancelRequest(orderId));
        Assert.True(result.IsSuccess);
        var cancellation = Assert.IsType<CancelResult>(result.Value);
        Assert.True(cancellation.CancelledQuantity > 0);
        AssertPositiveTrades(cancellation.Trades);
        engine.ValidateInvariants();
        return cancellation;
    }

    private static void AssertPositiveTrades(IEnumerable<Trade> trades) =>
        Assert.All(trades, trade =>
        {
            Assert.True(trade.Quantity > 0);
            Assert.True(trade.Price.MinorUnits > 0);
            Assert.NotEqual(trade.BuyOrderId, trade.SellOrderId);
        });

    private static void AssertSnapshotEqual(BookSnapshot expected, BookSnapshot actual)
    {
        Assert.Equal(expected.Bids.AsEnumerable(), actual.Bids);
        Assert.Equal(expected.Asks.AsEnumerable(), actual.Asks);
        Assert.Equal(expected.SuspendedOrders.AsEnumerable(), actual.SuspendedOrders);
    }

    private static Price PriceOf(long minorUnits)
    {
        Assert.True(Price.TryCreate(minorUnits, out var price));
        return price;
    }
}
