using MatchingEngine.Core.Domain;

namespace MatchingEngine.Cli.Tests;

public sealed class CommandParserTests
{
    [Theory]
    [InlineData("limit buy 10.50 100", Side.Buy, 1_050, 100)]
    [InlineData("limit sell 0.01 25", Side.Sell, 1, 25)]
    [InlineData("  LIMIT\tBUY  10  5  ", Side.Buy, 1_000, 5)]
    public void Parse_CreatesLimitRequest(
        string input,
        Side expectedSide,
        long expectedMinorUnits,
        long expectedQuantity)
    {
        var command = Assert.IsType<SubmitOrderCommand>(Success(input));
        var request = Assert.IsType<LimitRequest>(command.Request);

        Assert.Equal(expectedSide, request.Side);
        Assert.Equal(expectedMinorUnits, request.Price.MinorUnits);
        Assert.Equal(expectedQuantity, request.Quantity);
    }

    [Theory]
    [InlineData("market buy 100", Side.Buy, 100)]
    [InlineData("market sell 25", Side.Sell, 25)]
    [InlineData("MARKET SELL 1", Side.Sell, 1)]
    public void Parse_CreatesMarketRequest(string input, Side expectedSide, long expectedQuantity)
    {
        var command = Assert.IsType<SubmitOrderCommand>(Success(input));
        var request = Assert.IsType<MarketRequest>(command.Request);

        Assert.Equal(expectedSide, request.Side);
        Assert.Equal(expectedQuantity, request.Quantity);
    }

    [Theory]
    [InlineData("print book")]
    [InlineData("PRINT BOOK")]
    public void Parse_CreatesPrintBookCommand(string input) =>
        Assert.IsType<PrintBookCommand>(Success(input));

    [Theory]
    [InlineData("cancel order 1", 1UL)]
    [InlineData("CANCEL ORDER 42", 42UL)]
    [InlineData("cancel order 18446744073709551615", ulong.MaxValue)]
    public void Parse_CreatesCancelOrderCommand(string input, ulong expectedOrderId)
    {
        var command = Assert.IsType<CancelOrderCommand>(Success(input));

        Assert.Equal(new OrderId(expectedOrderId), command.Request.OrderId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_RejectsEmptyInput(string? input) =>
        AssertFailure(input, CommandParseErrorCode.EmptyInput, "input");

    [Theory]
    [InlineData("unknown")]
    public void Parse_RejectsUnknownCommand(string input) =>
        AssertFailure(input, CommandParseErrorCode.UnknownCommand, "command");

    [Theory]
    [InlineData("limit")]
    [InlineData("limit buy 10")]
    [InlineData("limit buy 10 1 extra")]
    [InlineData("market")]
    [InlineData("market buy")]
    [InlineData("market buy 1 extra")]
    [InlineData("print")]
    [InlineData("print trades")]
    [InlineData("print book extra")]
    [InlineData("cancel")]
    [InlineData("cancel order")]
    [InlineData("cancel 1")]
    [InlineData("cancel orders 1")]
    [InlineData("cancel order 1 extra")]
    public void Parse_RejectsMissingExtraOrUnexpectedTokens(string input) =>
        AssertFailure(input, CommandParseErrorCode.InvalidSyntax, "command");

    [Theory]
    [InlineData("limit hold 10 1")]
    [InlineData("market bid 1")]
    public void Parse_RejectsInvalidSide(string input) =>
        AssertFailure(input, CommandParseErrorCode.InvalidSide, "side");

    [Theory]
    [InlineData("limit buy 0 1")]
    [InlineData("limit buy -10 1")]
    [InlineData("limit buy 10.001 1")]
    [InlineData("limit buy 10x 1")]
    [InlineData("limit buy 999999999999999999999 1")]
    public void Parse_RejectsInvalidPrice(string input) =>
        AssertFailure(input, CommandParseErrorCode.InvalidPrice, "price");

    [Theory]
    [InlineData("market buy 0")]
    [InlineData("market buy -1")]
    [InlineData("market buy +1")]
    [InlineData("market buy 1.5")]
    [InlineData("market buy 10x")]
    [InlineData("market buy 9223372036854775808")]
    [InlineData("limit sell 10 ١")]
    public void Parse_RejectsInvalidQuantity(string input) =>
        AssertFailure(input, CommandParseErrorCode.InvalidQuantity, "quantity");

    [Theory]
    [InlineData("cancel order 0")]
    [InlineData("cancel order -1")]
    [InlineData("cancel order +1")]
    [InlineData("cancel order 1x")]
    [InlineData("cancel order 18446744073709551616")]
    [InlineData("cancel order ١")]
    public void Parse_RejectsInvalidOrderId(string input) =>
        AssertFailure(input, CommandParseErrorCode.InvalidOrderId, "orderId");

    private static CliCommand Success(string input)
    {
        var result = CommandParser.Parse(input);
        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        return Assert.IsAssignableFrom<CliCommand>(result.Command);
    }

    private static void AssertFailure(
        string? input,
        CommandParseErrorCode expectedCode,
        string expectedField)
    {
        var result = CommandParser.Parse(input);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Command);
        Assert.Equal(new CommandParseError(expectedCode, expectedField), result.Error);
    }
}
