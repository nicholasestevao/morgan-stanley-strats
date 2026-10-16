namespace MatchingEngine.Core.Domain;

public abstract record SubmitRequest
{
    private protected SubmitRequest() { }
}

public sealed record MarketRequest(Side Side, long Quantity) : SubmitRequest;
