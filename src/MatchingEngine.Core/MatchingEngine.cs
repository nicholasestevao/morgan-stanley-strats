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

    public BookSnapshot GetSnapshot() => new(
        CreateSideSnapshot(Side.Buy),
        CreateSideSnapshot(Side.Sell),
        CreateSuspendedSnapshot());

    public OperationResult<SubmitResult> Submit(SubmitRequest? request) => RepriceAfter(request switch
    {
        null => OperationResult<SubmitResult>.Failure(EngineErrorCode.RequestRequired, "request"),
        LimitRequest limit => SubmitLimit(limit),
        MarketRequest market => SubmitMarket(market),
        PeggedRequest pegged => SubmitPegged(pegged),
        _ => OperationResult<SubmitResult>.Failure(EngineErrorCode.UnsupportedRequest, "request"),
    });

    public OperationResult<CancelResult> Cancel(CancelRequest? request)
    {
        if (request is null)
            return OperationResult<CancelResult>.Failure(EngineErrorCode.RequestRequired, "request");
        if (!activeOrders.TryGetValue(request.OrderId, out var order))
            return OperationResult<CancelResult>.Failure(EngineErrorCode.OrderNotFound, "orderId");

        var cancelledQuantity = order.RemainingQuantity;
        RemoveOrder(order);
        return RepriceAfter(OperationResult<CancelResult>.Success(
            new CancelResult(order.Id, cancelledQuantity)));
    }

    public OperationResult<AmendResult> Amend(AmendRequest? request)
    {
        if (request is null)
            return OperationResult<AmendResult>.Failure(EngineErrorCode.RequestRequired, "request");
        if (request.Price is null && request.Quantity is null)
            return OperationResult<AmendResult>.Failure(EngineErrorCode.UnsupportedRequest, "request");
        if (request.Price is { MinorUnits: <= 0 })
            return OperationResult<AmendResult>.Failure(EngineErrorCode.InvalidPrice, "price");
        if (request.Quantity <= 0)
            return OperationResult<AmendResult>.Failure(EngineErrorCode.NonPositiveQuantity, "quantity");
        if (!activeOrders.TryGetValue(request.OrderId, out var order))
            return OperationResult<AmendResult>.Failure(EngineErrorCode.OrderNotFound, "orderId");
        if (order.Type == OrderType.Pegged && request.Price is not null)
            return OperationResult<AmendResult>.Failure(
                EngineErrorCode.PriceAmendmentNotAllowed,
                "price");

        if (order.State == OrderState.Suspended)
        {
            var suspendedQuantity = request.Quantity!.Value;
            if (suspendedQuantity > order.RemainingQuantity)
            {
                if (prioritySequencesExhausted)
                    return OperationResult<AmendResult>.Failure(
                        EngineErrorCode.PrioritySequenceExhausted,
                        "prioritySequence");
                order.PrioritySequence = TakeNextPrioritySequence();
            }

            order.RemainingQuantity = suspendedQuantity;
            return RepriceAfter(OperationResult<AmendResult>.Success(
                new AmendResult(order.Id, [], 0, suspendedQuantity)));
        }

        var currentPrice = order.EffectivePrice!.Value;
        var price = request.Price ?? currentPrice;
        var quantity = request.Quantity ?? order.RemainingQuantity;
        var losesPriority = price != currentPrice || quantity > order.RemainingQuantity;
        if (!losesPriority)
        {
            order.RemainingQuantity = quantity;
            return RepriceAfter(OperationResult<AmendResult>.Success(
                new AmendResult(order.Id, [], 0, quantity)));
        }
        if (prioritySequencesExhausted)
            return OperationResult<AmendResult>.Failure(
                EngineErrorCode.PrioritySequenceExhausted,
                "prioritySequence");

        var prioritySequence = TakeNextPrioritySequence();
        RemoveFromBook(order);
        var remainingQuantity = quantity;
        var trades = ImmutableArray.CreateBuilder<Trade>();
        Match(order.Side, order.Id, ref remainingQuantity, trades, price);

        if (remainingQuantity > 0)
        {
            order.RemainingQuantity = remainingQuantity;
            order.PrioritySequence = prioritySequence;
            order.EffectivePrice = price;
            orderBook.Add(order.Side, price, prioritySequence, order.Id);
        }
        else if (!activeOrders.Remove(order.Id))
            throw new InvalidOperationException("Order is missing from the active registry.");

        return RepriceAfter(OperationResult<AmendResult>.Success(new AmendResult(
            order.Id,
            trades.ToImmutable(),
            quantity - remainingQuantity,
            remainingQuantity)));
    }

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
        RepricePeggedOrders();
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

    private OperationResult<SubmitResult> SubmitPegged(PeggedRequest request)
    {
        if (!IsValidPegReference(request.Reference))
            return OperationResult<SubmitResult>.Failure(
                EngineErrorCode.InvalidPegReference,
                "reference");
        if (!IsValidSide(request.Side))
            return OperationResult<SubmitResult>.Failure(EngineErrorCode.InvalidSide, "side");
        if (request.Quantity <= 0)
            return OperationResult<SubmitResult>.Failure(EngineErrorCode.NonPositiveQuantity, "quantity");
        if (orderIdsExhausted)
            return OperationResult<SubmitResult>.Failure(EngineErrorCode.OrderIdExhausted, "orderId");
        if (prioritySequencesExhausted)
            return OperationResult<SubmitResult>.Failure(
                EngineErrorCode.PrioritySequenceExhausted,
                "prioritySequence");

        var orderId = TakeNextOrderId();
        var prioritySequence = TakeNextPrioritySequence();
        var referencePrice = FindReferencePrice(request.Reference);
        if (referencePrice is null)
        {
            activeOrders.Add(orderId, new Order(
                orderId,
                OrderType.Pegged,
                request.Side,
                request.Quantity,
                prioritySequence,
                null,
                request.Reference,
                OrderState.Suspended));
            return OperationResult<SubmitResult>.Success(
                new SubmitResult(orderId, [], 0, request.Quantity, 0));
        }

        var remainingQuantity = request.Quantity;
        var trades = ImmutableArray.CreateBuilder<Trade>();
        Match(request.Side, orderId, ref remainingQuantity, trades, referencePrice);
        if (remainingQuantity > 0)
        {
            var order = new Order(
                orderId,
                OrderType.Pegged,
                request.Side,
                remainingQuantity,
                prioritySequence,
                referencePrice,
                request.Reference);
            activeOrders.Add(orderId, order);
            orderBook.Add(request.Side, referencePrice.Value, prioritySequence, orderId);
        }

        return OperationResult<SubmitResult>.Success(new SubmitResult(
            orderId,
            trades.ToImmutable(),
            request.Quantity - remainingQuantity,
            remainingQuantity,
            0));
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

            RemoveOrder(restingOrder);
        }
    }

    private void RemoveOrder(Order order)
    {
        RemoveFromBook(order);
        if (!activeOrders.Remove(order.Id))
            throw new InvalidOperationException("Order is missing from the active registry.");
    }

    private void RemoveFromBook(Order order)
    {
        if (order.EffectivePrice is { } price &&
            !orderBook.Remove(order.Side, price, order.PrioritySequence))
            throw new InvalidOperationException("Active order is missing from the order book.");
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

    private ImmutableArray<SuspendedOrderSnapshot> CreateSuspendedSnapshot() =>
        [.. activeOrders.Values
            .Where(order => order.State == OrderState.Suspended)
            .OrderBy(order => order.PrioritySequence)
            .Select(order => new SuspendedOrderSnapshot(
                order.Id,
                order.Side,
                order.PegReference!.Value,
                order.RemainingQuantity))];

    private static BookOrderSnapshot ToSnapshot(Order order) =>
        new(
            order.Id,
            order.Side,
            order.Type,
            order.EffectivePrice!.Value,
            order.RemainingQuantity,
            order.PegReference);

    private Price? FindReferencePrice(PegReference reference)
    {
        var side = reference == PegReference.Bid ? Side.Buy : Side.Sell;
        Price? bestPrice = null;
        foreach (var order in activeOrders.Values)
        {
            if (order.Type != OrderType.Limit || order.Side != side || order.EffectivePrice is not { } price)
                continue;
            if (bestPrice is null || side == Side.Buy && price > bestPrice.Value ||
                side == Side.Sell && price < bestPrice.Value)
                bestPrice = price;
        }

        return bestPrice;
    }

    private OperationResult<T> RepriceAfter<T>(OperationResult<T> result) where T : class
    {
        if (result.IsSuccess) RepricePeggedOrders();
        return result;
    }

    private bool RepricePeggedOrders()
    {
        var changed = false;
        foreach (var order in activeOrders.Values
            .Where(order => order.Type == OrderType.Pegged)
            .OrderBy(order => order.PrioritySequence))
        {
            var referencePrice = FindReferencePrice(order.PegReference!.Value);
            if (referencePrice == order.EffectivePrice) continue;

            RemoveFromBook(order);
            order.EffectivePrice = referencePrice;
            order.State = referencePrice is null ? OrderState.Suspended : OrderState.Resting;
            if (referencePrice is { } price)
                orderBook.Add(order.Side, price, order.PrioritySequence, order.Id);
            changed = true;
        }

        return changed;
    }

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

    private static bool IsValidPegReference(PegReference reference) =>
        reference is PegReference.Bid or PegReference.Offer;

    private static bool Crosses(Side incomingSide, Price limitPrice, Price restingPrice) =>
        incomingSide == Side.Buy ? restingPrice <= limitPrice : restingPrice >= limitPrice;
}
