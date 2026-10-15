using System.Globalization;

namespace MatchingEngine.Core.Domain;

public readonly record struct Price : IComparable<Price>
{
    private const long MinorUnitsPerMajorUnit = 100;

    private Price(long minorUnits) => MinorUnits = minorUnits;

    public long MinorUnits { get; }

    public static bool TryCreate(long minorUnits, out Price price)
    {
        var isValid = minorUnits > 0;
        price = isValid ? new Price(minorUnits) : default;
        return isValid;
    }

    public static bool TryParse(string? text, out Price price, out PriceParseError error)
    {
        if (string.IsNullOrEmpty(text)) return Fail(out price, out error, PriceParseError.Empty);

        var decimalPosition = text.IndexOf('.', StringComparison.Ordinal);
        if (decimalPosition == 0 || decimalPosition == text.Length - 1)
            return Fail(out price, out error, PriceParseError.InvalidFormat);
        if (decimalPosition >= 0 && text.IndexOf('.', decimalPosition + 1) >= 0)
            return Fail(out price, out error, PriceParseError.InvalidFormat);

        var wholePart = decimalPosition < 0 ? text.AsSpan() : text.AsSpan(0, decimalPosition);
        ReadOnlySpan<char> fractionalPart = decimalPosition < 0 ? [] : text.AsSpan(decimalPosition + 1);
        if (fractionalPart.Length > 2)
            return Fail(out price, out error, PriceParseError.TooManyDecimalPlaces);
        if (!ContainsOnlyAsciiDigits(wholePart) || !ContainsOnlyAsciiDigits(fractionalPart))
            return Fail(out price, out error, PriceParseError.InvalidFormat);

        long wholeValue = 0;
        foreach (var character in wholePart)
        {
            var digit = character - '0';
            if (wholeValue > (long.MaxValue - digit) / 10)
                return Fail(out price, out error, PriceParseError.Overflow);
            wholeValue = (wholeValue * 10) + digit;
        }

        var fractionalValue = fractionalPart.Length switch
        {
            0 => 0,
            1 => (fractionalPart[0] - '0') * 10L,
            _ => ((fractionalPart[0] - '0') * 10L) + fractionalPart[1] - '0',
        };

        if (wholeValue > (long.MaxValue - fractionalValue) / MinorUnitsPerMajorUnit)
            return Fail(out price, out error, PriceParseError.Overflow);

        var minorUnits = (wholeValue * MinorUnitsPerMajorUnit) + fractionalValue;
        if (minorUnits == 0) return Fail(out price, out error, PriceParseError.NonPositive);

        price = new Price(minorUnits);
        error = PriceParseError.None;
        return true;
    }

    public int CompareTo(Price other) => MinorUnits.CompareTo(other.MinorUnits);

    public override string ToString()
    {
        var wholeValue = MinorUnits / MinorUnitsPerMajorUnit;
        var fractionalValue = MinorUnits % MinorUnitsPerMajorUnit;

        return fractionalValue switch
        {
            0 => wholeValue.ToString(CultureInfo.InvariantCulture),
            _ when fractionalValue % 10 == 0 => string.Create(CultureInfo.InvariantCulture, $"{wholeValue}.{fractionalValue / 10}"),
            _ => string.Create(CultureInfo.InvariantCulture, $"{wholeValue}.{fractionalValue:00}"),
        };
    }

    public static bool operator <(Price left, Price right) => left.CompareTo(right) < 0;

    public static bool operator >(Price left, Price right) => left.CompareTo(right) > 0;

    public static bool operator <=(Price left, Price right) => left.CompareTo(right) <= 0;

    public static bool operator >=(Price left, Price right) => left.CompareTo(right) >= 0;

    private static bool Fail(out Price price, out PriceParseError error, PriceParseError value)
    {
        price = default;
        error = value;
        return false;
    }

    private static bool ContainsOnlyAsciiDigits(ReadOnlySpan<char> value)
    {
        foreach (var character in value)
            if (character is < '0' or > '9') return false;

        return true;
    }
}
