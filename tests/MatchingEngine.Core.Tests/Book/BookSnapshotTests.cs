using MatchingEngine.Core.Book;
using MatchingEngine.Core.Domain;

namespace MatchingEngine.Core.Tests.Book;

public sealed class BookSnapshotTests
{
    [Fact]
    public void GetSnapshot_ReturnsEmptySidesForNewEngine()
    {
        var snapshot = new global::MatchingEngine.Core.MatchingEngine().GetSnapshot();

        Assert.Empty(snapshot.Bids);
        Assert.Empty(snapshot.Asks);
    }

    [Fact]
    public void GetSnapshot_ReturnsCompleteOrdersInBookPriority()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var laterBidId = engine.AddRestingLimit(Side.Buy, PriceOf(1_000), 75);
        var bestBidId = engine.AddRestingLimit(Side.Buy, PriceOf(1_100), 50);
        var bestAskId = engine.AddRestingLimit(Side.Sell, PriceOf(1_200), 25);
        var laterAskId = engine.AddRestingLimit(Side.Sell, PriceOf(1_300), 100);

        var snapshot = engine.GetSnapshot();

        // Bids descend and asks ascend because the first order on each side is the best price.
        Assert.Equal([bestBidId, laterBidId], snapshot.Bids.Select(order => order.OrderId));
        Assert.Equal([bestAskId, laterAskId], snapshot.Asks.Select(order => order.OrderId));
        Assert.Equal(new BookOrderSnapshot(bestBidId, Side.Buy, OrderType.Limit, PriceOf(1_100), 50), snapshot.Bids[0]);
        Assert.Equal(new BookOrderSnapshot(bestAskId, Side.Sell, OrderType.Limit, PriceOf(1_200), 25), snapshot.Asks[0]);
    }

    [Fact]
    public void GetSnapshot_PreservesFifoAtTheSamePrice()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var firstId = engine.AddRestingLimit(Side.Sell, PriceOf(1_000), 10);
        var secondId = engine.AddRestingLimit(Side.Sell, PriceOf(1_000), 20);

        var snapshot = engine.GetSnapshot();

        Assert.Equal([firstId, secondId], snapshot.Asks.Select(order => order.OrderId));
    }

    [Fact]
    public void GetSnapshot_IsIndependentFromLaterBookChanges()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine();
        var firstId = engine.AddRestingLimit(Side.Buy, PriceOf(1_000), 10);
        var original = engine.GetSnapshot();

        engine.AddRestingLimit(Side.Buy, PriceOf(1_100), 20);
        var updated = engine.GetSnapshot();

        Assert.Equal([firstId], original.Bids.Select(order => order.OrderId));
        Assert.Equal(2, updated.Bids.Length);
    }

    private static Price PriceOf(long minorUnits)
    {
        Assert.True(Price.TryCreate(minorUnits, out var price));
        return price;
    }
}
