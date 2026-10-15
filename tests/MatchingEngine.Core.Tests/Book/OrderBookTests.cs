using MatchingEngine.Core.Book;
using MatchingEngine.Core.Domain;

namespace MatchingEngine.Core.Tests.Book;

public sealed class OrderBookTests
{
    [Fact]
    public void Enumerate_OrdersBidsFromHighestPriceAndAsksFromLowestPrice()
    {
        var book = new OrderBook();
        Add(book, Side.Buy, 1, 1_000, 1);
        Add(book, Side.Buy, 2, 1_200, 2);
        Add(book, Side.Buy, 3, 1_100, 3);
        Add(book, Side.Sell, 4, 1_400, 4);
        Add(book, Side.Sell, 5, 1_200, 5);
        Add(book, Side.Sell, 6, 1_300, 6);

        // Buyers offering more have priority; sellers asking less have priority.
        Assert.Equal([2UL, 3UL, 1UL], OrderIds(book, Side.Buy));
        Assert.Equal([5UL, 6UL, 4UL], OrderIds(book, Side.Sell));
    }

    [Fact]
    public void Enumerate_UsesPrioritySequenceWithinAPriceLevel()
    {
        var book = new OrderBook();
        Add(book, Side.Buy, 3, 1_000, 30);
        Add(book, Side.Buy, 1, 1_000, 10);
        Add(book, Side.Buy, 2, 1_000, 20);

        // At the same price, the lowest sequence represents the earliest arrival.
        Assert.Equal([1UL, 2UL, 3UL], OrderIds(book, Side.Buy));
    }

    [Fact]
    public void Remove_HandlesFirstMiddleAndLastPositions()
    {
        var book = new OrderBook();
        Add(book, Side.Sell, 1, 1_000, 10);
        Add(book, Side.Sell, 2, 1_000, 20);
        Add(book, Side.Sell, 3, 1_000, 30);

        Assert.True(book.Remove(Side.Sell, PriceOf(1_000), new PrioritySequence(20)));
        Assert.True(book.Remove(Side.Sell, PriceOf(1_000), new PrioritySequence(10)));
        Assert.True(book.Remove(Side.Sell, PriceOf(1_000), new PrioritySequence(30)));
        Assert.Empty(book.Enumerate(Side.Sell));
        Assert.Equal(0, book.Count);
    }

    [Fact]
    public void Remove_LastOrderAtBestPricePromotesNextPrice()
    {
        var book = new OrderBook();
        Add(book, Side.Buy, 1, 1_100, 10);
        Add(book, Side.Buy, 2, 1_000, 20);

        // Removing the best bid exposes the next-highest price as the new best.
        Assert.True(book.Remove(Side.Buy, PriceOf(1_100), new PrioritySequence(10)));
        Assert.True(book.TryGetBest(Side.Buy, out var best));
        Assert.Equal(new OrderId(2), best.OrderId);
        Assert.Equal(PriceOf(1_000), best.Price);
    }

    [Fact]
    public void Remove_UnknownEntryPreservesBook()
    {
        var book = new OrderBook();
        Add(book, Side.Buy, 1, 1_000, 10);

        Assert.False(book.Remove(Side.Buy, PriceOf(1_000), new PrioritySequence(99)));
        Assert.Equal(1, book.Count);
        Assert.Equal([1UL], OrderIds(book, Side.Buy));
    }

    [Fact]
    public void TryGetBest_ReturnsFalseForEmptySide()
    {
        var book = new OrderBook();
        Add(book, Side.Buy, 1, 1_000, 10);

        Assert.False(book.TryGetBest(Side.Sell, out _));
    }

    private static void Add(OrderBook book, Side side, ulong id, long minorUnits, ulong sequence) =>
        book.Add(side, PriceOf(minorUnits), new PrioritySequence(sequence), new OrderId(id));

    private static ulong[] OrderIds(OrderBook book, Side side) =>
        [.. book.Enumerate(side).Select(entry => entry.OrderId.Value)];

    private static Price PriceOf(long minorUnits)
    {
        Assert.True(Price.TryCreate(minorUnits, out var price));
        return price;
    }
}
