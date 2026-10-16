namespace MatchingEngine.Core.Domain;

public readonly record struct Trade(
    OrderId BuyOrderId,
    OrderId SellOrderId,
    Price Price,
    long Quantity);
