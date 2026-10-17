using MatchingEngine.Core.Domain;

namespace MatchingEngine.Core.Tests.Matching;

public sealed class PeggedOrderStabilizationTests
{
    [Fact]
    public void Submit_PeggedRepricesBetweenTradesAndSuspendsRemainder()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var firstAsk = Submit(engine, new LimitRequest(Side.Sell, PriceOf(1_000), 10));
        var secondAsk = Submit(engine, new LimitRequest(Side.Sell, PriceOf(1_100), 10));

        var pegged = Submit(engine, new PeggedRequest(PegReference.Offer, Side.Buy, 25));

        Assert.Equal(20, pegged.ExecutedQuantity);
        Assert.Equal(5, pegged.RestingQuantity);
        Assert.Equal(0, pegged.CancelledQuantity);
        Assert.Collection(
            pegged.Trades,
            trade => AssertTrade(trade, pegged.OrderId, firstAsk.OrderId, 1_000, 10),
            trade => AssertTrade(trade, pegged.OrderId, secondAsk.OrderId, 1_100, 10));
        var snapshot = engine.GetSnapshot();
        Assert.Empty(snapshot.Bids);
        Assert.Empty(snapshot.Asks);
        Assert.Equal(pegged.OrderId, Assert.Single(snapshot.SuspendedOrders).OrderId);
        Assert.Equal(5, snapshot.SuspendedOrders[0].RemainingQuantity);
    }

    [Fact]
    public void Submit_MarketRepricesPeggedBeforeChoosingNextCounterparty()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var firstAsk = Submit(engine, new LimitRequest(Side.Sell, PriceOf(1_000), 10));
        var secondAsk = Submit(engine, new LimitRequest(Side.Sell, PriceOf(1_100), 10));
        var pegged = Submit(engine, new PeggedRequest(PegReference.Offer, Side.Sell, 10));

        var market = Submit(engine, new MarketRequest(Side.Buy, 15));

        Assert.Collection(
            market.Trades,
            trade => AssertTrade(trade, market.OrderId, firstAsk.OrderId, 1_000, 10),
            trade => AssertTrade(trade, market.OrderId, secondAsk.OrderId, 1_100, 5));
        var asks = engine.GetSnapshot().Asks;
        Assert.Equal([secondAsk.OrderId, pegged.OrderId], asks.Select(order => order.OrderId));
        Assert.All(asks, order => Assert.Equal(PriceOf(1_100), order.Price));
    }

    [Fact]
    public void Submit_FixedLimitReportsTradesCreatedByPeggedActivation()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var pegged = Submit(engine, new PeggedRequest(PegReference.Bid, Side.Sell, 6));

        var fixedBid = Submit(engine, new LimitRequest(Side.Buy, PriceOf(1_000), 10));

        Assert.Equal(6, fixedBid.ExecutedQuantity);
        Assert.Equal(4, fixedBid.RestingQuantity);
        AssertTrade(Assert.Single(fixedBid.Trades), fixedBid.OrderId, pegged.OrderId, 1_000, 6);
        var resting = Assert.Single(engine.GetSnapshot().Bids);
        Assert.Equal(fixedBid.OrderId, resting.OrderId);
        Assert.Equal(4, resting.RemainingQuantity);
        Assert.Empty(engine.GetSnapshot().SuspendedOrders);
    }

    [Fact]
    public void Submit_ProcessesExecutablePeggedOrdersByPrioritySequence()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var first = Submit(engine, new PeggedRequest(PegReference.Bid, Side.Sell, 4));
        var second = Submit(engine, new PeggedRequest(PegReference.Bid, Side.Sell, 4));

        var fixedBid = Submit(engine, new LimitRequest(Side.Buy, PriceOf(1_000), 10));

        Assert.Collection(
            fixedBid.Trades,
            trade => AssertTrade(trade, fixedBid.OrderId, first.OrderId, 1_000, 4),
            trade => AssertTrade(trade, fixedBid.OrderId, second.OrderId, 1_000, 4));
        Assert.Equal(8, fixedBid.ExecutedQuantity);
        Assert.Equal(2, fixedBid.RestingQuantity);
        Assert.Empty(engine.GetSnapshot().SuspendedOrders);
    }

    [Fact]
    public void Submit_MarketSuspendsPeggedBeforeNextTradeWhenReferenceDisappears()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var fixedAsk = Submit(engine, new LimitRequest(Side.Sell, PriceOf(1_000), 10));
        var pegged = Submit(engine, new PeggedRequest(PegReference.Offer, Side.Sell, 5));

        var amended = engine.Amend(new AmendRequest(pegged.OrderId, Quantity: 15));
        Assert.True(amended.IsSuccess);

        var market = Submit(engine, new MarketRequest(Side.Buy, 20));

        Assert.Equal(10, market.ExecutedQuantity);
        Assert.Equal(10, market.CancelledQuantity);
        AssertTrade(Assert.Single(market.Trades), market.OrderId, fixedAsk.OrderId, 1_000, 10);
        var suspended = Assert.Single(engine.GetSnapshot().SuspendedOrders);
        Assert.Equal(pegged.OrderId, suspended.OrderId);
        Assert.Equal(15, suspended.RemainingQuantity);
    }

    [Fact]
    public void Amend_ActivePeggedQuantityAppliesPriorityPoliciesBeforeStabilizing()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var fixedAsk = Submit(engine, new LimitRequest(Side.Sell, PriceOf(1_000), 10));
        var first = Submit(engine, new PeggedRequest(PegReference.Offer, Side.Sell, 5));
        var second = Submit(engine, new PeggedRequest(PegReference.Offer, Side.Sell, 5));

        Assert.True(engine.Amend(new AmendRequest(first.OrderId, Quantity: 3)).IsSuccess);
        Assert.Equal(
            [fixedAsk.OrderId, first.OrderId, second.OrderId],
            engine.GetSnapshot().Asks.Select(order => order.OrderId));

        Assert.True(engine.Amend(new AmendRequest(first.OrderId, Quantity: 10)).IsSuccess);
        Assert.Equal(
            [fixedAsk.OrderId, second.OrderId, first.OrderId],
            engine.GetSnapshot().Asks.Select(order => order.OrderId));
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
