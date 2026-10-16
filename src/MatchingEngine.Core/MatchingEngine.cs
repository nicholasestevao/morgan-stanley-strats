using System.Collections.Immutable;
using MatchingEngine.Core.Book;
using MatchingEngine.Core.Domain;

namespace MatchingEngine.Core;

public sealed class MatchingEngine
{
    private readonly Dictionary<OrderId, Order> activeOrders = [];
    private readonly OrderBook orderBook = new();
    private ulong nextOrderId = 1;
    private ulong nextPrioritySequence = 1;
    private bool orderIdsExhausted;
    private bool prioritySequencesExhausted;

    public MatchingEngine() { }

    internal MatchingEngine(ulong nextOrderId, ulong nextPrioritySequence)
    {
        ArgumentOutOfRangeException.ThrowIfZero(nextOrderId);
        ArgumentOutOfRangeException.ThrowIfZero(nextPrioritySequence);

        this.nextOrderId = nextOrderId;
        this.nextPrioritySequence = nextPrioritySequence;
    }

    public BookSnapshot GetSnapshot() => new(CreateSideSnapshot(Side.Buy), CreateSideSnapshot(Side.Sell));

    public OperationResult<SubmitResult> Submit(SubmitRequest? request) => request switch
    {
        null => OperationResult<SubmitResult>.Failure(EngineErrorCode.RequestRequired, "request"),
        LimitRequest limit => SubmitLimit(limit),
        MarketRequest market => SubmitMarket(market),
        _ => OperationResult<SubmitResult>.Failure(EngineErrorCode.UnsupportedRequest, "request"),
    };

    internal OrderId AddRestingLimit(Side side, Price price, long quantity)
    {
        if (!IsValidSide(side)) throw new ArgumentOutOfRangeException(nameof(side));
        if (price.MinorUnits <= 0) throw new ArgumentOutOfRangeException(nameof(price));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        if (orderIdsExhausted || prioritySequencesExhausted)
            throw new InvalidOperationException("Order capacity is exhausted.");

        var id = TakeNextOrderId();
        var prioritySequence = TakeNextPrioritySequence();
        AddRestingLimit(id, prioritySequence, side, price, quantity);
        return id;
    }

    private OperationResult<SubmitResult> SubmitLimit(LimitRequest request)
    {
        if (!IsValidSide(request.Side))
            return OperationResult<SubmitResult>.Failure(EngineErrorCode.InvalidSide, "side");
        if (request.Price.MinorUnits <= 0)
            return OperationResult<SubmitResult>.Failure(EngineErrorCode.InvalidPrice, "price");
        if (request.Quantity <= 0)
            return OperationResult<SubmitResult>.Failure(EngineErrorCode.NonPositiveQuantity, "quantity");
        if (orderIdsExhausted)
            return OperationResult<SubmitResult>.Failure(EngineErrorCode.OrderIdExhausted, "orderId");
        if (prioritySequencesExhausted)
            return OperationResult<SubmitResult>.Failure(EngineErrorCode.PrioritySequenceExhausted, "prioritySequence");

        var orderId = TakeNextOrderId();
        var prioritySequence = TakeNextPrioritySequence();
        var remainingQuantity = request.Quantity;
        var trades = ImmutableArray.CreateBuilder<Trade>();
        Match(request.Side, orderId, ref remainingQuantity, trades, request.Price);

        if (remainingQuantity > 0)
            AddRestingLimit(orderId, prioritySequence, request.Side, request.Price, remainingQuantity);

        return OperationResult<SubmitResult>.Success(new SubmitResult(
            orderId,
            trades.ToImmutable(),
            request.Quantity - remainingQuantity,
            remainingQuantity,
            0));
    }

    private OperationResult<SubmitResult> SubmitMarket(MarketRequest request)
    {
        if (!IsValidSide(request.Side))
            return OperationResult<SubmitResult>.Failure(EngineErrorCode.InvalidSide, "side");
        if (request.Quantity <= 0)
            return OperationResult<SubmitResult>.Failure(EngineErrorCode.NonPositiveQuantity, "quantity");
        if (orderIdsExhausted)
            return OperationResult<SubmitResult>.Failure(EngineErrorCode.OrderIdExhausted, "orderId");

        var orderId = TakeNextOrderId();
        var remainingQuantity = request.Quantity;
        var trades = ImmutableArray.CreateBuilder<Trade>();
        Match(request.Side, orderId, ref remainingQuantity, trades, null);

        return OperationResult<SubmitResult>.Success(new SubmitResult(
            orderId,
            trades.ToImmutable(),
            request.Quantity - remainingQuantity,
            0,
            remainingQuantity));
    }

    private void Match(
        Side incomingSide,
        OrderId incomingOrderId,
        ref long incomingQuantity,
        ImmutableArray<Trade>.Builder trades,
        Price? limitPrice)
    {
        var restingSide = incomingSide == Side.Buy ? Side.Sell : Side.Buy;
        while (incomingQuantity > 0 && orderBook.TryGetBest(restingSide, out var best))
        {
            if (limitPrice is { } price && !Crosses(incomingSide, price, best.Price)) break;

            var restingOrder = activeOrders[best.OrderId];
            var tradeQuantity = Math.Min(incomingQuantity, restingOrder.RemainingQuantity);
            var buyOrderId = incomingSide == Side.Buy ? incomingOrderId : restingOrder.Id;
            var sellOrderId = incomingSide == Side.Sell ? incomingOrderId : restingOrder.Id;

            trades.Add(new Trade(buyOrderId, sellOrderId, best.Price, tradeQuantity));
            incomingQuantity -= tradeQuantity;
            restingOrder.RemainingQuantity -= tradeQuantity;

            if (restingOrder.RemainingQuantity > 0) continue;

            orderBook.Remove(restingOrder.Side, best.Price, restingOrder.PrioritySequence);
            activeOrders.Remove(restingOrder.Id);
        }
    }

    private void AddRestingLimit(
        OrderId orderId,
        PrioritySequence prioritySequence,
        Side side,
        Price price,
        long quantity)
    {
        var order = new Order(orderId, OrderType.Limit, side, quantity, prioritySequence, price);
        activeOrders.Add(orderId, order);
        orderBook.Add(side, price, prioritySequence, orderId);
    }

    private ImmutableArray<BookOrderSnapshot> CreateSideSnapshot(Side side) =>
        [.. orderBook.Enumerate(side).Select(entry => ToSnapshot(activeOrders[entry.OrderId]))];

    private static BookOrderSnapshot ToSnapshot(Order order) =>
        new(order.Id, order.Side, order.Type, order.EffectivePrice!.Value, order.RemainingQuantity);

    private OrderId TakeNextOrderId()
    {
        var orderId = new OrderId(nextOrderId);
        if (nextOrderId == ulong.MaxValue) orderIdsExhausted = true;
        else nextOrderId++;
        return orderId;
    }

    private PrioritySequence TakeNextPrioritySequence()
    {
        var prioritySequence = new PrioritySequence(nextPrioritySequence);
        if (nextPrioritySequence == ulong.MaxValue) prioritySequencesExhausted = true;
        else nextPrioritySequence++;
        return prioritySequence;
    }

    private static bool IsValidSide(Side side) => side is Side.Buy or Side.Sell;

    private static bool Crosses(Side incomingSide, Price limitPrice, Price restingPrice) =>
        incomingSide == Side.Buy ? restingPrice <= limitPrice : restingPrice >= limitPrice;
}
