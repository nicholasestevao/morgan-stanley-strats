using System.Collections.Immutable;
using MatchingEngine.Core.Domain;

namespace MatchingEngine.Core.Book;

public readonly record struct BookOrderSnapshot(
    OrderId OrderId,
    Side Side,
    OrderType Type,
    Price Price,
    long RemainingQuantity,
    PegReference? PegReference = null);

public readonly record struct SuspendedOrderSnapshot(
    OrderId OrderId,
    Side Side,
    PegReference PegReference,
    long RemainingQuantity);

public sealed record BookSnapshot(
    ImmutableArray<BookOrderSnapshot> Bids,
    ImmutableArray<BookOrderSnapshot> Asks,
    ImmutableArray<SuspendedOrderSnapshot> SuspendedOrders)
{
    public BookSnapshot(
        ImmutableArray<BookOrderSnapshot> bids,
        ImmutableArray<BookOrderSnapshot> asks) : this(bids, asks, []) { }
}
