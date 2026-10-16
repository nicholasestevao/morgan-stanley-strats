using System.Collections.Immutable;
using System.Globalization;
using MatchingEngine.Core.Book;
using MatchingEngine.Core.Domain;

namespace MatchingEngine.Cli;

public static class OutputFormatter
{
    private const string BuyHeader = "Buy Orders";
    private const string SellHeader = "Sell Orders";

    public static ImmutableArray<string> FormatSubmission(SubmitResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var lines = ImmutableArray.CreateBuilder<string>();
        lines.Add($"Order accepted, id: {result.OrderId.Value.ToString(CultureInfo.InvariantCulture)}");

        for (var index = 0; index < result.Trades.Length;)
        {
            var price = result.Trades[index].Price;
            long quantity = 0;

            do quantity += result.Trades[index++].Quantity;
            while (index < result.Trades.Length && result.Trades[index].Price == price);

            lines.Add($"Trade, price: {price}, qty: {quantity.ToString(CultureInfo.InvariantCulture)}");
        }

        return lines.ToImmutable();
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
