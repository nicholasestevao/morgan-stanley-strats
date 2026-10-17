using System.Globalization;
using MatchingEngine.Core.Domain;

namespace MatchingEngine.Cli;

public static class CommandParser
{
    public static CommandParseResult Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return CommandParseResult.Failure(CommandParseErrorCode.EmptyInput, "input");

        var tokens = input.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (Is(tokens[0], "limit")) return ParseLimit(tokens);
        if (Is(tokens[0], "market")) return ParseMarket(tokens);
        if (Is(tokens[0], "cancel")) return ParseCancel(tokens);
        if (Is(tokens[0], "amend")) return ParseAmend(tokens);
        if (Is(tokens[0], "print")) return ParsePrint(tokens);
        return CommandParseResult.Failure(CommandParseErrorCode.UnknownCommand, "command");
    }

    private static CommandParseResult ParseLimit(string[] tokens)
    {
        if (tokens.Length != 4)
            return CommandParseResult.Failure(CommandParseErrorCode.InvalidSyntax, "command");
        if (!TryParseSide(tokens[1], out var side))
            return CommandParseResult.Failure(CommandParseErrorCode.InvalidSide, "side");
        if (!Price.TryParse(tokens[2], out var price, out _))
            return CommandParseResult.Failure(CommandParseErrorCode.InvalidPrice, "price");
        if (!TryParseQuantity(tokens[3], out var quantity))
            return CommandParseResult.Failure(CommandParseErrorCode.InvalidQuantity, "quantity");

        return CommandParseResult.Success(new SubmitOrderCommand(new LimitRequest(side, price, quantity)));
    }

    private static CommandParseResult ParseMarket(string[] tokens)
    {
        if (tokens.Length != 3)
            return CommandParseResult.Failure(CommandParseErrorCode.InvalidSyntax, "command");
        if (!TryParseSide(tokens[1], out var side))
            return CommandParseResult.Failure(CommandParseErrorCode.InvalidSide, "side");
        if (!TryParseQuantity(tokens[2], out var quantity))
            return CommandParseResult.Failure(CommandParseErrorCode.InvalidQuantity, "quantity");

        return CommandParseResult.Success(new SubmitOrderCommand(new MarketRequest(side, quantity)));
    }

    private static CommandParseResult ParseCancel(string[] tokens)
    {
        if (tokens.Length != 3 || !Is(tokens[1], "order"))
            return CommandParseResult.Failure(CommandParseErrorCode.InvalidSyntax, "command");
        if (!TryParseOrderId(tokens[2], out var orderId))
            return CommandParseResult.Failure(CommandParseErrorCode.InvalidOrderId, "orderId");

        return CommandParseResult.Success(new CancelOrderCommand(new CancelRequest(orderId)));
    }

    private static CommandParseResult ParseAmend(string[] tokens)
    {
        if (tokens.Length is not (5 or 7) || !Is(tokens[1], "order"))
            return CommandParseResult.Failure(CommandParseErrorCode.InvalidSyntax, "command");

        var hasPrice = Is(tokens[3], "price");
        var hasQuantity = Is(tokens[tokens.Length == 5 ? 3 : 5], "qty");
        if (tokens.Length == 5 && !hasPrice && !hasQuantity ||
            tokens.Length == 7 && (!hasPrice || !hasQuantity))
            return CommandParseResult.Failure(CommandParseErrorCode.InvalidSyntax, "command");
        if (!TryParseOrderId(tokens[2], out var orderId))
            return CommandParseResult.Failure(CommandParseErrorCode.InvalidOrderId, "orderId");

        Price? price = null;
        if (hasPrice)
        {
            if (!Price.TryParse(tokens[4], out var parsedPrice, out _))
                return CommandParseResult.Failure(CommandParseErrorCode.InvalidPrice, "price");
            price = parsedPrice;
        }

        long? quantity = null;
        var quantityIndex = tokens.Length == 5 ? 4 : 6;
        if (hasQuantity)
        {
            if (!TryParseQuantity(tokens[quantityIndex], out var parsedQuantity))
                return CommandParseResult.Failure(CommandParseErrorCode.InvalidQuantity, "quantity");
            quantity = parsedQuantity;
        }

        return CommandParseResult.Success(new AmendOrderCommand(
            new AmendRequest(orderId, price, quantity)));
    }

    private static CommandParseResult ParsePrint(string[] tokens) =>
        tokens.Length == 2 && Is(tokens[1], "book")
            ? CommandParseResult.Success(new PrintBookCommand())
            : CommandParseResult.Failure(CommandParseErrorCode.InvalidSyntax, "command");

    private static bool TryParseSide(string token, out Side side)
    {
        if (Is(token, "buy"))
        {
            side = Side.Buy;
            return true;
        }

        if (Is(token, "sell"))
        {
            side = Side.Sell;
            return true;
        }

        side = default;
        return false;
    }

    private static bool TryParseQuantity(string token, out long quantity)
    {
        foreach (var character in token)
            if (character is < '0' or > '9')
            {
                quantity = default;
                return false;
            }

        return long.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out quantity) && quantity > 0;
    }

    private static bool TryParseOrderId(string token, out OrderId orderId)
    {
        foreach (var character in token)
            if (character is < '0' or > '9')
            {
                orderId = default;
                return false;
            }

        var isValid = ulong.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0;
        orderId = isValid ? new OrderId(value) : default;
        return isValid;
    }

    private static bool Is(string value, string expected) =>
        value.Equals(expected, StringComparison.OrdinalIgnoreCase);
}
