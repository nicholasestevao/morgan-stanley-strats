using Engine = MatchingEngine.Core.MatchingEngine;

namespace MatchingEngine.Cli.Tests;

public sealed class CommandLoopTests
{
    [Fact]
    public void Run_EndsNormallyAtEofWithoutOutput()
    {
        var output = Run(string.Empty);

        Assert.Equal(string.Empty, output);
    }

    [Fact]
    public void Run_ReportsInvalidLineAndContinuesWithUnchangedState()
    {
        var output = Run(
            "market buy 0\n" +
            "limit buy 10 100\n" +
            "print book\n");

        Assert.Equal(
            Lines(
                "Error: quantity must be a positive integer.",
                "Order accepted, id: 1",
                "Buy Orders | Sell Orders",
                "-----------|------------",
                "100 @ 10   |"),
            output);
    }

    [Fact]
    public void Run_ExecutesChallengeSequenceThroughParserEngineAndFormatter()
    {
        var output = Run(
            "limit buy 10 100\n" +
            "limit sell 20 100\n" +
            "limit sell 20 200\n" +
            "market buy 150\n" +
            "market buy 200\n" +
            "market sell 200\n" +
            "print book\n");

        Assert.Equal(
            Lines(
                "Order accepted, id: 1",
                "Order accepted, id: 2",
                "Order accepted, id: 3",
                "Order accepted, id: 4",
                "Trade, price: 20, qty: 150",
                "Order accepted, id: 5",
                "Trade, price: 20, qty: 150",
                "Order accepted, id: 6",
                "Trade, price: 10, qty: 100",
                "Buy Orders | Sell Orders",
                "-----------|------------"),
            output);
    }

    [Fact]
    public void Run_PrintsCurrentBookWithoutChangingIt()
    {
        var output = Run(
            "limit buy 10 200\n" +
            "limit buy 9.99 100\n" +
            "limit sell 10.5 100\n" +
            "print book\n" +
            "print book\n");

        var table = Lines(
            "Buy Orders | Sell Orders",
            "-----------|------------",
            "200 @ 10   | 100 @ 10.5",
            "100 @ 9.99 |");

        Assert.Equal(
            Lines("Order accepted, id: 1", "Order accepted, id: 2", "Order accepted, id: 3") +
            table + table,
            output);
    }

    [Fact]
    public void Run_CancelsOrderAndContinuesAfterRepeatedCancellation()
    {
        var output = Run(
            "limit buy 10 100\n" +
            "cancel order 1\n" +
            "print book\n" +
            "cancel order 1\n" +
            "limit buy 9 50\n");

        Assert.Equal(
            Lines(
                "Order accepted, id: 1",
                "Order cancelled, id: 1, qty: 100",
                "Buy Orders | Sell Orders",
                "-----------|------------",
                "Error: order was not found.",
                "Order accepted, id: 2"),
            output);
    }

    [Fact]
    public void Run_AmendsQuantityAndContinuesAfterInvalidAmendment()
    {
        var output = Run(
            "limit sell 10 100\n" +
            "amend order 1 qty 60\n" +
            "amend order 1 qty 0\n" +
            "print book\n");

        Assert.Equal(
            Lines(
                "Order accepted, id: 1",
                "Order amended, id: 1, qty: 60",
                "Error: quantity must be a positive integer.",
                "Buy Orders | Sell Orders",
                "-----------|------------",
                "           | 60 @ 10"),
            output);
    }

    [Fact]
    public void Run_AmendsPriceAndQuantityThenMatchesCrossingOrder()
    {
        var output = Run(
            "limit sell 10 30\n" +
            "limit buy 9 50\n" +
            "amend order 2 price 10 qty 40\n" +
            "print book\n");

        Assert.Equal(
            Lines(
                "Order accepted, id: 1",
                "Order accepted, id: 2",
                "Order amended, id: 2, qty: 10",
                "Trade, price: 10, qty: 30",
                "Buy Orders | Sell Orders",
                "-----------|------------",
                "10 @ 10    |"),
            output);
    }

    [Fact]
    public void Run_AcceptsAndPrintsSuspendedPeggedOrder()
    {
        var output = Run(
            "peg bid buy 100\n" +
            "print book\n");

        Assert.Equal(
            Lines(
                "Order accepted, id: 1",
                "Buy Orders | Sell Orders",
                "-----------|------------",
                "Suspended Orders",
                "----------------",
                "id: 1, peg bid buy, qty: 100"),
            output);
    }

    private static string Run(string input)
    {
        using var reader = new StringReader(input);
        using var writer = new StringWriter();
        new CommandLoop(new Engine()).Run(reader, writer);
        return writer.ToString();
    }

    private static string Lines(params string[] lines) =>
        string.Join(Environment.NewLine, lines) + Environment.NewLine;
}
