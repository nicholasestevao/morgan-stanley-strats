namespace MatchingEngine.Core.Domain;

public enum Side
{
    Buy,
    Sell,
}

public enum OrderType
{
    Limit,
    Market,
}

public readonly record struct OrderId(ulong Value);

public readonly record struct PrioritySequence(ulong Value) : IComparable<PrioritySequence>
{
    public int CompareTo(PrioritySequence other) => Value.CompareTo(other.Value);

    public static bool operator <(PrioritySequence left, PrioritySequence right) => left.CompareTo(right) < 0;

    public static bool operator >(PrioritySequence left, PrioritySequence right) => left.CompareTo(right) > 0;

    public static bool operator <=(PrioritySequence left, PrioritySequence right) => left.CompareTo(right) <= 0;

    public static bool operator >=(PrioritySequence left, PrioritySequence right) => left.CompareTo(right) >= 0;
}
