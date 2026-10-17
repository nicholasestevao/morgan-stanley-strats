namespace MatchingEngine.Core.Domain;

internal sealed class Order(
    OrderId id,
    OrderType type,
    Side side,
    long remainingQuantity,
    PrioritySequence prioritySequence,
    Price? effectivePrice,
    PegReference? pegReference = null,
    OrderState state = OrderState.Resting)
{
    public OrderId Id { get; } = id;

    public OrderType Type { get; } = type;

    public Side Side { get; } = side;

    public long RemainingQuantity { get; set; } = remainingQuantity;

    public PrioritySequence PrioritySequence { get; set; } = prioritySequence;

    public Price? EffectivePrice { get; set; } = effectivePrice;

    public PegReference? PegReference { get; } = pegReference;

    public OrderState State { get; set; } = state;
}
