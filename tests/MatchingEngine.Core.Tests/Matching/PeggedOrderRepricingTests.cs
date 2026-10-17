using MatchingEngine.Core.Domain;

namespace MatchingEngine.Core.Tests.Matching;

public sealed class PeggedOrderRepricingTests
{
    [Fact]
    public void Submit_FixedLimitActivatesSuspendedPeggedWithOriginalPriority()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var pegged = Submit(engine, new PeggedRequest(PegReference.Bid, Side.Buy, 100));

        var fixedBid = Submit(engine, new LimitRequest(Side.Buy, PriceOf(1_000), 50));
        var snapshot = engine.GetSnapshot();

        Assert.Empty(snapshot.SuspendedOrders);
        Assert.Equal([pegged.OrderId, fixedBid.OrderId], snapshot.Bids.Select(order => order.OrderId));
        Assert.All(snapshot.Bids, order => Assert.Equal(PriceOf(1_000), order.Price));
    }

    [Fact]
    public void Submit_BetterFixedLimitRepricesPeggedAheadOfNewReferenceOrder()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var oldReference = Submit(engine, new LimitRequest(Side.Buy, PriceOf(1_000), 10));
        var pegged = Submit(engine, new PeggedRequest(PegReference.Bid, Side.Buy, 10));

        var newReference = Submit(engine, new LimitRequest(Side.Buy, PriceOf(1_010), 10));
        var bids = engine.GetSnapshot().Bids;

        Assert.Equal(
            [pegged.OrderId, newReference.OrderId, oldReference.OrderId],
            bids.Select(order => order.OrderId));
        Assert.Equal(PriceOf(1_010), bids[0].Price);
        Assert.Equal(PriceOf(1_010), bids[1].Price);
    }

    [Fact]
    public void Cancel_ReferenceMovesPeggedToFallbackThenSuspendsIt()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var best = Submit(engine, new LimitRequest(Side.Buy, PriceOf(1_000), 10));
        var fallback = Submit(engine, new LimitRequest(Side.Buy, PriceOf(900), 10));
        var pegged = Submit(engine, new PeggedRequest(PegReference.Bid, Side.Buy, 10));

        Assert.True(engine.Cancel(new CancelRequest(best.OrderId)).IsSuccess);
        var repriced = engine.GetSnapshot();
        Assert.Equal(PriceOf(900), repriced.Bids.Single(order => order.OrderId == pegged.OrderId).Price);

        Assert.True(engine.Cancel(new CancelRequest(fallback.OrderId)).IsSuccess);
        var suspended = engine.GetSnapshot();
        Assert.Empty(suspended.Bids);
        Assert.Equal(pegged.OrderId, Assert.Single(suspended.SuspendedOrders).OrderId);
    }

    [Fact]
    public void Submit_NewReferenceReactivatesPeggedWithItsOriginalPriority()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var pegged = Submit(engine, new PeggedRequest(PegReference.Bid, Side.Buy, 10));
        var firstReference = Submit(engine, new LimitRequest(Side.Buy, PriceOf(1_000), 10));
        Assert.True(engine.Cancel(new CancelRequest(firstReference.OrderId)).IsSuccess);

        var secondReference = Submit(engine, new LimitRequest(Side.Buy, PriceOf(1_100), 10));
        var snapshot = engine.GetSnapshot();

        Assert.Empty(snapshot.SuspendedOrders);
        Assert.Equal(
            [pegged.OrderId, secondReference.OrderId],
            snapshot.Bids.Select(order => order.OrderId));
    }

    [Fact]
    public void Amend_FixedReferenceRepricesPeggedWithoutChangingItsPriority()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var fixedBid = Submit(engine, new LimitRequest(Side.Buy, PriceOf(1_000), 10));
        var pegged = Submit(engine, new PeggedRequest(PegReference.Bid, Side.Buy, 10));

        Assert.True(engine.Amend(new AmendRequest(fixedBid.OrderId, PriceOf(1_100))).IsSuccess);
        var bids = engine.GetSnapshot().Bids;

        Assert.Equal([pegged.OrderId, fixedBid.OrderId], bids.Select(order => order.OrderId));
        Assert.All(bids, order => Assert.Equal(PriceOf(1_100), order.Price));
    }

    [Fact]
    public void Submit_ExecutionThatExhaustsReferenceRepricesPeggedToNextLevel()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var bestAsk = Submit(engine, new LimitRequest(Side.Sell, PriceOf(1_000), 10));
        var nextAsk = Submit(engine, new LimitRequest(Side.Sell, PriceOf(1_100), 10));
        var pegged = Submit(engine, new PeggedRequest(PegReference.Offer, Side.Sell, 10));

        var market = Submit(engine, new MarketRequest(Side.Buy, 10));
        var asks = engine.GetSnapshot().Asks;

        Assert.Equal(bestAsk.OrderId, Assert.Single(market.Trades).SellOrderId);
        Assert.Equal([nextAsk.OrderId, pegged.OrderId], asks.Select(order => order.OrderId));
        Assert.All(asks, order => Assert.Equal(PriceOf(1_100), order.Price));
    }

    [Fact]
    public void Submit_RepricesMultiplePeggedOrdersInOriginalSequence()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        Submit(engine, new LimitRequest(Side.Buy, PriceOf(1_000), 10));
        var first = Submit(engine, new PeggedRequest(PegReference.Bid, Side.Buy, 10));
        var second = Submit(engine, new PeggedRequest(PegReference.Bid, Side.Buy, 10));

        var newReference = Submit(engine, new LimitRequest(Side.Buy, PriceOf(1_100), 10));
        var topLevel = engine.GetSnapshot().Bids.Where(order => order.Price == PriceOf(1_100));

        Assert.Equal(
            [first.OrderId, second.OrderId, newReference.OrderId],
            topLevel.Select(order => order.OrderId));
    }

    [Fact]
    public void Submit_UpdatesOnlyPeggedOrdersUsingChangedReferenceSide()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        Submit(engine, new LimitRequest(Side.Buy, PriceOf(1_000), 10));
        Submit(engine, new LimitRequest(Side.Sell, PriceOf(2_000), 10));
        var bidPegged = Submit(engine, new PeggedRequest(PegReference.Bid, Side.Buy, 10));
        var offerPegged = Submit(engine, new PeggedRequest(PegReference.Offer, Side.Sell, 10));

        Submit(engine, new LimitRequest(Side.Buy, PriceOf(1_100), 10));
        var snapshot = engine.GetSnapshot();

        Assert.Equal(
            PriceOf(1_100),
            snapshot.Bids.Single(order => order.OrderId == bidPegged.OrderId).Price);
        Assert.Equal(
            PriceOf(2_000),
            snapshot.Asks.Single(order => order.OrderId == offerPegged.OrderId).Price);
    }

    private static SubmitResult Submit(
        global::MatchingEngine.Core.MatchingEngine engine,
        SubmitRequest request)
    {
        var result = engine.Submit(request);
        Assert.True(result.IsSuccess);
        return Assert.IsType<SubmitResult>(result.Value);
    }

    private static Price PriceOf(long minorUnits)
    {
        Assert.True(Price.TryCreate(minorUnits, out var price));
        return price;
    }
}
