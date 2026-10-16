using MatchingEngine.Core.Domain;

namespace MatchingEngine.Core.Tests.Matching;

public sealed class MatchingEngineCapacityTests
{
    [Fact]
    public void AddRestingLimit_RejectsOrderAfterPriorityCapacityIsExhausted()
    {
        var engine = new global::MatchingEngine.Core.MatchingEngine(1, ulong.MaxValue);
        var restingId = engine.AddRestingLimit(Side.Sell, PriceOf(1_000), 10);

        Assert.Throws<InvalidOperationException>(() =>
            engine.AddRestingLimit(Side.Sell, PriceOf(1_000), 10));

        var marketResult = engine.Submit(new MarketRequest(Side.Buy, 10));
        Assert.True(marketResult.IsSuccess);
        Assert.Equal(new OrderId(2), marketResult.Value!.OrderId);
        Assert.Equal(restingId, Assert.Single(marketResult.Value.Trades).SellOrderId);
    }

    private static Price PriceOf(long minorUnits)
    {
        Assert.True(Price.TryCreate(minorUnits, out var price));
        return price;
    }
}
