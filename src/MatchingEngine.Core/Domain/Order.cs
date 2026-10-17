namespace MatchingEngine.Core.Domain;

internal sealed class Order(
    OrderId id,
    OrderType type,
    Side side,
    long remainingQuantity,
    PrioritySequence prioritySequence,
    Price? effectivePrice)
{
    public OrderId Id { get; } = id;

    public OrderType Type { get; } = type;

    public Side Side { get; } = side;

    public long RemainingQuantity { get; set; } = remainingQuantity;

    public PrioritySequence PrioritySequence { get; set; } = prioritySequence;

    public Price? EffectivePrice { get; set; } = effectivePrice;
}
