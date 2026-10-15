using MatchingEngine.Core.Domain;

namespace MatchingEngine.Core.Tests.Domain;

public sealed class PriceTests
{
    [Theory]
    [InlineData("10", 1_000)]
    [InlineData("10.0", 1_000)]
    [InlineData("10.00", 1_000)]
    [InlineData("10.5", 1_050)]
    [InlineData("10.50", 1_050)]
    [InlineData("0.01", 1)]
    public void TryParse_AcceptsExactDecimalInput(string text, long expectedMinorUnits)
    {
        Assert.True(Price.TryParse(text, out var price, out var error));
        Assert.Equal(PriceParseError.None, error);
        Assert.Equal(expectedMinorUnits, price.MinorUnits);
    }

    [Theory]
    [InlineData("10.00", "10")]
    [InlineData("10.50", "10.5")]
    [InlineData("10.05", "10.05")]
    [InlineData("0.01", "0.01")]
    public void ToString_UsesCanonicalDecimalNotation(string text, string expected) =>
        Assert.Equal(expected, ParsePrice(text).ToString());

    [Theory]
    [InlineData(null, PriceParseError.Empty)]
    [InlineData("", PriceParseError.Empty)]
    [InlineData(".50", PriceParseError.InvalidFormat)]
    [InlineData("10.", PriceParseError.InvalidFormat)]
    [InlineData("+10", PriceParseError.InvalidFormat)]
    [InlineData("-10", PriceParseError.InvalidFormat)]
    [InlineData("1e2", PriceParseError.InvalidFormat)]
    [InlineData("10x", PriceParseError.InvalidFormat)]
    [InlineData("10.2x", PriceParseError.InvalidFormat)]
    [InlineData("10.2.0", PriceParseError.InvalidFormat)]
    public void TryParse_RejectsInvalidSyntax(string? text, PriceParseError expectedError)
    {
        Assert.False(Price.TryParse(text, out _, out var error));
        Assert.Equal(expectedError, error);
    }

    [Theory]
    [InlineData("0", PriceParseError.NonPositive)]
    [InlineData("0.00", PriceParseError.NonPositive)]
    [InlineData("10.001", PriceParseError.TooManyDecimalPlaces)]
    [InlineData("92233720368547758.08", PriceParseError.Overflow)]
    [InlineData("999999999999999999999", PriceParseError.Overflow)]
    public void TryParse_RejectsUnsupportedValues(string text, PriceParseError expectedError)
    {
        Assert.False(Price.TryParse(text, out _, out var error));
        Assert.Equal(expectedError, error);
    }

    [Fact]
    public void TryParse_AcceptsLargestRepresentableValue()
    {
        var price = ParsePrice("92233720368547758.07");

        Assert.Equal(long.MaxValue, price.MinorUnits);
        Assert.Equal("92233720368547758.07", price.ToString());
    }

    [Fact]
    public void Comparison_UsesExactMinorUnits()
    {
        Assert.True(ParsePrice("9.99") < ParsePrice("10"));
        Assert.True(ParsePrice("10") < ParsePrice("10.01"));
        Assert.Equal(ParsePrice("10.5"), ParsePrice("10.50"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TryCreate_RejectsNonPositiveMinorUnits(long minorUnits) =>
        Assert.False(Price.TryCreate(minorUnits, out _));

    [Fact]
    public void TryCreate_AcceptsPositiveMinorUnits()
    {
        Assert.True(Price.TryCreate(1, out var price));
        Assert.Equal(1, price.MinorUnits);
    }

    private static Price ParsePrice(string text)
    {
        Assert.True(Price.TryParse(text, out var price, out var error), $"Expected a valid price but received {error}.");
        return price;
    }
}
