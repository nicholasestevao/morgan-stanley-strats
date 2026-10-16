using System.Collections.Immutable;
using MatchingEngine.Core.Book;
using MatchingEngine.Core.Domain;

namespace MatchingEngine.Cli.Tests;

public sealed class OutputFormatterTests
{
    [Theory]
    [InlineData(CommandParseErrorCode.EmptyInput, "Error: input is empty.")]
    [InlineData(CommandParseErrorCode.UnknownCommand, "Error: unknown command.")]
    [InlineData(CommandParseErrorCode.InvalidSyntax, "Error: invalid command syntax.")]
    [InlineData(CommandParseErrorCode.InvalidSide, "Error: side must be buy or sell.")]
    [InlineData(CommandParseErrorCode.InvalidPrice, "Error: invalid price.")]
    [InlineData(CommandParseErrorCode.InvalidQuantity, "Error: quantity must be a positive integer.")]
    [InlineData(CommandParseErrorCode.InvalidOrderId, "Error: order ID must be a positive integer.")]
    public void FormatError_FormatsCommandErrors(CommandParseErrorCode code, string expected) =>
        Assert.Equal(expected, OutputFormatter.FormatError(new CommandParseError(code, "field")));

    [Theory]
    [InlineData(EngineErrorCode.RequestRequired, "Error: request is required.")]
    [InlineData(EngineErrorCode.InvalidSide, "Error: side must be buy or sell.")]
    [InlineData(EngineErrorCode.InvalidPrice, "Error: invalid price.")]
    [InlineData(EngineErrorCode.NonPositiveQuantity, "Error: quantity must be positive.")]
    [InlineData(EngineErrorCode.OrderIdExhausted, "Error: order ID capacity is exhausted.")]
    [InlineData(EngineErrorCode.PrioritySequenceExhausted, "Error: priority capacity is exhausted.")]
    [InlineData(EngineErrorCode.UnsupportedRequest, "Error: unsupported request.")]
    [InlineData(EngineErrorCode.OrderNotFound, "Error: order was not found.")]
    public void FormatError_FormatsEngineErrors(EngineErrorCode code, string expected) =>
        Assert.Equal(expected, OutputFormatter.FormatError(new EngineError(code, "field")));

    [Fact]
    public void FormatSubmission_PrintsAcceptedOrderIdWithoutTrades()
    {
        var result = Result(42);

        Assert.Equal(["Order accepted, id: 42"], OutputFormatter.FormatSubmission(result).ToArray());
    }

    [Fact]
    public void FormatSubmission_UsesRequiredTradeFormat()
    {
        var result = Result(3, Trade(3, 1, 1_050, 25));

        Assert.Equal(
            ["Order accepted, id: 3", "Trade, price: 10.5, qty: 25"],
            OutputFormatter.FormatSubmission(result).ToArray());
    }

    [Fact]
    public void FormatSubmission_GroupsOnlyConsecutiveTradesAtTheSamePrice()
    {
        var result = Result(
            7,
            Trade(7, 1, 2_000, 100),
            Trade(7, 2, 2_000, 50),
            Trade(7, 3, 2_100, 25),
            Trade(7, 4, 2_000, 10));

        Assert.Equal(
            [
                "Order accepted, id: 7",
                "Trade, price: 20, qty: 150",
                "Trade, price: 21, qty: 25",
                "Trade, price: 20, qty: 10",
            ],
            OutputFormatter.FormatSubmission(result).ToArray());
    }

    [Fact]
    public void FormatCancellation_PrintsOrderIdAndCancelledQuantity()
    {
        var result = new CancelResult(new OrderId(42), 75);

        Assert.Equal("Order cancelled, id: 42, qty: 75", OutputFormatter.FormatCancellation(result));
    }

    [Fact]
    public void FormatBook_PrintsHeadersForEmptyBook()
    {
        var snapshot = new BookSnapshot([], []);

        Assert.Equal(
            ["Buy Orders | Sell Orders", "-----------|------------"],
            OutputFormatter.FormatBook(snapshot).ToArray());
    }

    [Fact]
    public void FormatBook_PrintsBothSidesInSnapshotOrder()
    {
        var snapshot = new BookSnapshot(
            [
                Order(1, Side.Buy, 1_000, 200),
                Order(2, Side.Buy, 999, 100),
            ],
            [Order(3, Side.Sell, 1_050, 100)]);

        Assert.Equal(
            [
                "Buy Orders | Sell Orders",
                "-----------|------------",
                "200 @ 10   | 100 @ 10.5",
                "100 @ 9.99 |",
            ],
            OutputFormatter.FormatBook(snapshot).ToArray());
    }

    [Fact]
    public void FormatBook_ExpandsColumnsForLongValues()
    {
        var snapshot = new BookSnapshot(
            [Order(1, Side.Buy, 1, 1_000_000_000_000)],
            []);

        Assert.Equal(
            [
                "Buy Orders           | Sell Orders",
                "---------------------|------------",
                "1000000000000 @ 0.01 |",
            ],
            OutputFormatter.FormatBook(snapshot).ToArray());
    }

    private static SubmitResult Result(ulong id, params Trade[] trades) =>
        new(new OrderId(id), [.. trades], trades.Sum(trade => trade.Quantity), 0, 0);

    private static Trade Trade(ulong buyId, ulong sellId, long minorUnits, long quantity) =>
        new(new OrderId(buyId), new OrderId(sellId), PriceOf(minorUnits), quantity);

    private static BookOrderSnapshot Order(ulong id, Side side, long minorUnits, long quantity) =>
        new(new OrderId(id), side, OrderType.Limit, PriceOf(minorUnits), quantity);

    private static Price PriceOf(long minorUnits)
    {
        Assert.True(Price.TryCreate(minorUnits, out var price));
        return price;
    }
}
