namespace MatchingEngine.Core.Domain;

public abstract record SubmitRequest
{
    private protected SubmitRequest() { }
}

public sealed record LimitRequest(Side Side, Price Price, long Quantity) : SubmitRequest;

public sealed record MarketRequest(Side Side, long Quantity) : SubmitRequest;

public sealed record PeggedRequest(PegReference Reference, Side Side, long Quantity) : SubmitRequest;

public sealed record CancelRequest(OrderId OrderId);

public sealed record AmendRequest(OrderId OrderId, Price? Price = null, long? Quantity = null);
