using System.Collections.Immutable;
using MatchingEngine.Core.Domain;

namespace MatchingEngine.Core.Book;

public readonly record struct BookOrderSnapshot(
    OrderId OrderId,
    Side Side,
    OrderType Type,
    Price Price,
    long RemainingQuantity);

public sealed record BookSnapshot(
    ImmutableArray<BookOrderSnapshot> Bids,
    ImmutableArray<BookOrderSnapshot> Asks);
