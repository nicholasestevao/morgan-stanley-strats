using MatchingEngine.Core.Domain;

namespace MatchingEngine.Core.Book;

internal readonly record struct OrderBookEntry(
    OrderId OrderId,
    Side Side,
    Price Price,
    PrioritySequence PrioritySequence);

internal sealed class OrderBook
{
    private static readonly IComparer<Price> DescendingPriceComparer =
        Comparer<Price>.Create(static (left, right) => right.CompareTo(left));

    private readonly SortedDictionary<Price, SortedDictionary<PrioritySequence, OrderId>> bids =
        new(DescendingPriceComparer);
    private readonly SortedDictionary<Price, SortedDictionary<PrioritySequence, OrderId>> asks = [];

    public int Count { get; private set; }

    public void Add(Side side, Price price, PrioritySequence prioritySequence, OrderId orderId)
    {
        var bookSide = GetBookSide(side);
        if (!bookSide.TryGetValue(price, out var level))
            bookSide.Add(price, level = []);

        level.Add(prioritySequence, orderId);
        Count++;
    }

    public bool Remove(Side side, Price price, PrioritySequence prioritySequence)
    {
        var bookSide = GetBookSide(side);

        if (!bookSide.TryGetValue(price, out var level) || !level.Remove(prioritySequence)) 
            return false;
            
        if (level.Count == 0) 
            bookSide.Remove(price);

        Count--;
        return true;
    }

    public bool TryGetBest(Side side, out OrderBookEntry entry)
    {
        foreach (var (price, level) in GetBookSide(side))
        {
            var (prioritySequence, orderId) = level.First();
            entry = new OrderBookEntry(orderId, side, price, prioritySequence);
            return true;
        }

        entry = default;
        return false;
    }

    public IEnumerable<OrderBookEntry> Enumerate(Side side)
    {
        foreach (var (price, level) in GetBookSide(side))
            foreach (var (prioritySequence, orderId) in level)
                yield return new OrderBookEntry(orderId, side, price, prioritySequence);
    }

    private SortedDictionary<Price, SortedDictionary<PrioritySequence, OrderId>> GetBookSide(Side side) =>
        side == Side.Buy ? bids : asks;
}
