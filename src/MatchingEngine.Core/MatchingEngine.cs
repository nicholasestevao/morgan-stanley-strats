using System.Collections.Immutable;
using MatchingEngine.Core.Book;
using MatchingEngine.Core.Domain;

namespace MatchingEngine.Core;

public sealed class MatchingEngine
{
    private readonly Dictionary<OrderId, Order> activeOrders = [];
    private readonly OrderBook orderBook = new();
    private ulong nextOrderId = 1;
    private ulong nextPrioritySequence = 1;

    public BookSnapshot GetSnapshot() => new(CreateSideSnapshot(Side.Buy), CreateSideSnapshot(Side.Sell));

    internal OrderId AddRestingLimit(Side side, Price price, long quantity)
    {
        var id = new OrderId(nextOrderId++);
        var prioritySequence = new PrioritySequence(nextPrioritySequence++);
        var order = new Order(id, OrderType.Limit, side, quantity, prioritySequence, price);

        activeOrders.Add(id, order);
        orderBook.Add(side, price, prioritySequence, id);
        return id;
    }

    private ImmutableArray<BookOrderSnapshot> CreateSideSnapshot(Side side) =>
        [.. orderBook.Enumerate(side).Select(entry => ToSnapshot(activeOrders[entry.OrderId]))];

    private static BookOrderSnapshot ToSnapshot(Order order) =>
        new(order.Id, order.Side, order.Type, order.EffectivePrice!.Value, order.RemainingQuantity);
}
