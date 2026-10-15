namespace MatchingEngine.Core.Domain;

public enum PriceParseError
{
    None,
    Empty,
    InvalidFormat,
    TooManyDecimalPlaces,
    NonPositive,
    Overflow,
}

