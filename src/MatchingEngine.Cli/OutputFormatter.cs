using System.Collections.Immutable;
using System.Globalization;
using MatchingEngine.Core.Book;
using MatchingEngine.Core.Domain;

namespace MatchingEngine.Cli;

public static class OutputFormatter
{
    private const string BuyHeader = "Buy Orders";
    private const string SellHeader = "Sell Orders";

    public static string FormatError(CommandParseError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return error.Code switch
        {
            CommandParseErrorCode.EmptyInput => "Error: input is empty.",
            CommandParseErrorCode.UnknownCommand => "Error: unknown command.",
            CommandParseErrorCode.InvalidSyntax => "Error: invalid command syntax.",
            CommandParseErrorCode.InvalidSide => "Error: side must be buy or sell.",
            CommandParseErrorCode.InvalidPrice => "Error: invalid price.",
            CommandParseErrorCode.InvalidQuantity => "Error: quantity must be a positive integer.",
            CommandParseErrorCode.InvalidOrderId => "Error: order ID must be a positive integer.",
            _ => "Error: command could not be parsed.",
        };
    }

    public static string FormatError(EngineError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return error.Code switch
        {
            EngineErrorCode.RequestRequired => "Error: request is required.",
            EngineErrorCode.InvalidSide => "Error: side must be buy or sell.",
            EngineErrorCode.InvalidPrice => "Error: invalid price.",
            EngineErrorCode.NonPositiveQuantity => "Error: quantity must be positive.",
            EngineErrorCode.OrderIdExhausted => "Error: order ID capacity is exhausted.",
            EngineErrorCode.PrioritySequenceExhausted => "Error: priority capacity is exhausted.",
            EngineErrorCode.OrderNotFound => "Error: order was not found.",
            EngineErrorCode.UnsupportedRequest => "Error: unsupported request.",
            _ => "Error: operation failed.",
        };
    }

    public static string FormatCancellation(CancelResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return $"Order cancelled, id: {result.OrderId.Value.ToString(CultureInfo.InvariantCulture)}, " +
            $"qty: {result.CancelledQuantity.ToString(CultureInfo.InvariantCulture)}";
    }

    public static ImmutableArray<string> FormatSubmission(SubmitResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var lines = ImmutableArray.CreateBuilder<string>();
        lines.Add($"Order accepted, id: {result.OrderId.Value.ToString(CultureInfo.InvariantCulture)}");
        AppendTrades(lines, result.Trades);
        return lines.ToImmutable();
    }

    public static ImmutableArray<string> FormatAmendment(AmendResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var lines = ImmutableArray.CreateBuilder<string>();
        lines.Add($"Order amended, id: {result.OrderId.Value.ToString(CultureInfo.InvariantCulture)}, " +
            $"qty: {result.RestingQuantity.ToString(CultureInfo.InvariantCulture)}");
        AppendTrades(lines, result.Trades);
        return lines.ToImmutable();
    }

    private static void AppendTrades(
        ImmutableArray<string>.Builder lines,
        ImmutableArray<Trade> trades)
    {
        for (var index = 0; index < trades.Length;)
        {
            var price = trades[index].Price;
            long quantity = 0;

            do quantity += trades[index++].Quantity;
            while (index < trades.Length && trades[index].Price == price);

            lines.Add($"Trade, price: {price}, qty: {quantity.ToString(CultureInfo.InvariantCulture)}");
        }
    }

    public static ImmutableArray<string> FormatBook(BookSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var bids = snapshot.Bids.Select(FormatOrder).ToArray();
        var asks = snapshot.Asks.Select(FormatOrder).ToArray();
        var bidWidth = bids.Aggregate(BuyHeader.Length, static (width, value) => Math.Max(width, value.Length));
        var askWidth = asks.Aggregate(SellHeader.Length, static (width, value) => Math.Max(width, value.Length));
        var lines = ImmutableArray.CreateBuilder<string>(Math.Max(bids.Length, asks.Length) + 2);

        lines.Add($"{BuyHeader.PadRight(bidWidth)} | {SellHeader}");
        lines.Add($"{new string('-', bidWidth + 1)}|{new string('-', askWidth + 1)}");

        for (var index = 0; index < Math.Max(bids.Length, asks.Length); index++)
        {
            var bid = index < bids.Length ? bids[index] : string.Empty;
            var ask = index < asks.Length ? asks[index] : string.Empty;
            lines.Add($"{bid.PadRight(bidWidth)} | {ask}".TrimEnd());
        }

        return lines.ToImmutable();
    }

    private static string FormatOrder(BookOrderSnapshot order) =>
        $"{order.RemainingQuantity.ToString(CultureInfo.InvariantCulture)} @ {order.Price}";
}
