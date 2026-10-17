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
    [InlineData("peg bid buy 100", PegReference.Bid, Side.Buy, 100)]
    [InlineData("PEG BID SELL 25", PegReference.Bid, Side.Sell, 25)]
    [InlineData("peg offer buy 10", PegReference.Offer, Side.Buy, 10)]
    [InlineData("peg offer sell 1", PegReference.Offer, Side.Sell, 1)]
    public void Parse_CreatesPeggedRequest(
        string input,
        PegReference expectedReference,
        Side expectedSide,
        long expectedQuantity)
    {
        var command = Assert.IsType<SubmitOrderCommand>(Success(input));
        var request = Assert.IsType<PeggedRequest>(command.Request);

        Assert.Equal(expectedReference, request.Reference);
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
    [InlineData("amend order 1 qty 100", 1UL, 100)]
    [InlineData("AMEND ORDER 42 QTY 1", 42UL, 1)]
    public void Parse_CreatesAmendQuantityCommand(
        string input,
        ulong expectedOrderId,
        long expectedQuantity)
    {
        var command = Assert.IsType<AmendOrderCommand>(Success(input));

        Assert.Equal(new OrderId(expectedOrderId), command.Request.OrderId);
        Assert.Null(command.Request.Price);
        Assert.Equal(expectedQuantity, command.Request.Quantity);
    }

    [Theory]
    [InlineData("amend order 1 price 10.5", 1UL, 1_050)]
    [InlineData("AMEND ORDER 42 PRICE 0.01", 42UL, 1)]
    public void Parse_CreatesAmendPriceCommand(
        string input,
        ulong expectedOrderId,
        long expectedMinorUnits)
    {
        var command = Assert.IsType<AmendOrderCommand>(Success(input));

        Assert.Equal(new OrderId(expectedOrderId), command.Request.OrderId);
        Assert.Equal(expectedMinorUnits, command.Request.Price?.MinorUnits);
        Assert.Null(command.Request.Quantity);
    }

    [Fact]
    public void Parse_CreatesCombinedAmendCommand()
    {
        var command = Assert.IsType<AmendOrderCommand>(
            Success("amend order 7 price 10.5 qty 80"));

        Assert.Equal(new OrderId(7), command.Request.OrderId);
        Assert.Equal(1_050, command.Request.Price?.MinorUnits);
        Assert.Equal(80, command.Request.Quantity);
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
    [InlineData("peg")]
    [InlineData("peg bid")]
    [InlineData("peg bid buy")]
    [InlineData("peg bid buy 10 extra")]
    [InlineData("print")]
    [InlineData("print trades")]
    [InlineData("print book extra")]
    [InlineData("cancel")]
    [InlineData("cancel order")]
    [InlineData("cancel 1")]
    [InlineData("cancel orders 1")]
    [InlineData("cancel order 1 extra")]
    [InlineData("amend")]
    [InlineData("amend order")]
    [InlineData("amend order 1")]
    [InlineData("amend order 1 qty")]
    [InlineData("amend orders 1 qty 10")]
    [InlineData("amend order 1 quantity 10")]
    [InlineData("amend order 1 qty 10 extra")]
    [InlineData("amend order 1 price")]
    [InlineData("amend order 1 price 10 quantity 20")]
    [InlineData("amend order 1 qty 20 price 10")]
    [InlineData("amend order 1 price 10 qty")]
    [InlineData("amend order 1 price 10 qty 20 extra")]
    public void Parse_RejectsMissingExtraOrUnexpectedTokens(string input) =>
        AssertFailure(input, CommandParseErrorCode.InvalidSyntax, "command");

    [Theory]
    [InlineData("limit hold 10 1")]
    [InlineData("market bid 1")]
    [InlineData("peg bid hold 1")]
    public void Parse_RejectsInvalidSide(string input) =>
        AssertFailure(input, CommandParseErrorCode.InvalidSide, "side");

    [Theory]
    [InlineData("peg mid buy 10")]
    [InlineData("peg best sell 10")]
    public void Parse_RejectsInvalidPegReference(string input) =>
        AssertFailure(input, CommandParseErrorCode.InvalidPegReference, "reference");

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
    [InlineData("peg bid buy 0")]
    [InlineData("peg offer sell -1")]
    [InlineData("peg bid buy 9223372036854775808")]
    public void Parse_RejectsInvalidQuantity(string input) =>
        AssertFailure(input, CommandParseErrorCode.InvalidQuantity, "quantity");

    [Theory]
    [InlineData("cancel order 0")]
    [InlineData("cancel order -1")]
    [InlineData("cancel order +1")]
    [InlineData("cancel order 1x")]
    [InlineData("cancel order 18446744073709551616")]
    [InlineData("cancel order ١")]
    [InlineData("amend order 0 qty 10")]
    [InlineData("amend order -1 qty 10")]
    [InlineData("amend order 1x qty 10")]
    [InlineData("amend order 18446744073709551616 qty 10")]
    [InlineData("amend order 0 price 10")]
    public void Parse_RejectsInvalidOrderId(string input) =>
        AssertFailure(input, CommandParseErrorCode.InvalidOrderId, "orderId");

    [Theory]
    [InlineData("amend order 1 qty 0")]
    [InlineData("amend order 1 qty -1")]
    [InlineData("amend order 1 qty +1")]
    [InlineData("amend order 1 qty 1.5")]
    [InlineData("amend order 1 qty 9223372036854775808")]
    [InlineData("amend order 1 qty ١")]
    public void Parse_RejectsInvalidAmendQuantity(string input) =>
        AssertFailure(input, CommandParseErrorCode.InvalidQuantity, "quantity");

    [Theory]
    [InlineData("amend order 1 price 0")]
    [InlineData("amend order 1 price -10")]
    [InlineData("amend order 1 price 10.001")]
    [InlineData("amend order 1 price 10x")]
    [InlineData("amend order 1 price 999999999999999999999")]
    [InlineData("amend order 1 price 0 qty 10")]
    public void Parse_RejectsInvalidAmendPrice(string input) =>
        AssertFailure(input, CommandParseErrorCode.InvalidPrice, "price");

    [Theory]
    [InlineData("amend order 1 price 10 qty 0")]
    [InlineData("amend order 1 price 10 qty -1")]
    [InlineData("amend order 1 price 10 qty 9223372036854775808")]
    public void Parse_RejectsInvalidCombinedAmendQuantity(string input) =>
        AssertFailure(input, CommandParseErrorCode.InvalidQuantity, "quantity");

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
