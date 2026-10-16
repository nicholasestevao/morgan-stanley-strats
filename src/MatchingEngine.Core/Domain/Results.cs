using System.Collections.Immutable;

namespace MatchingEngine.Core.Domain;

public enum EngineErrorCode
{
    RequestRequired,
    InvalidSide,
    NonPositiveQuantity,
    OrderIdExhausted,
    UnsupportedRequest,
}

public sealed record EngineError(EngineErrorCode Code, string Field);

public sealed record SubmitResult(
    OrderId OrderId,
    ImmutableArray<Trade> Trades,
    long ExecutedQuantity,
    long RestingQuantity,
    long CancelledQuantity);

public sealed record OperationResult<T> where T : class
{
    private OperationResult(T? value, EngineError? error)
    {
        Value = value;
        Error = error;
    }

    public bool IsSuccess => Error is null;

    public T? Value { get; }

    public EngineError? Error { get; }

    internal static OperationResult<T> Success(T value) => new(value, null);

    internal static OperationResult<T> Failure(EngineErrorCode code, string field) =>
        new(null, new EngineError(code, field));
}
