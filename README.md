# Matching Engine Challenge

This repository contains an in-memory matching engine developed for the Morgan Stanley Institutional Equity Distribution - Strats software engineering internship challenge.

The engine will process orders for one instrument using price-time priority. The final scope includes limit and market orders, order book visualization, cancellation, amendment, and pegged orders.

## Current status

The project now supports limit and market order matching, command parsing, and deterministic text formatting.

- [x] .NET 10 solution and xUnit test infrastructure
- [x] Exact fixed-point price representation and parsing
- [x] Bid and ask indexes with price-time priority
- [x] Insertion, best-order lookup, ordered enumeration, and removal
- [x] Immutable order book snapshots with order details and remaining quantities
- [x] Market order matching with partial fills and remainder cancellation
- [x] Crossing limit orders with passive remainder handling
- [x] Parsing for limit, market, and print-book commands
- [x] Trade, accepted-order, and order-book output formatting
- [ ] Interactive command loop
- [ ] Order cancellation and amendment
- [ ] Pegged orders

Prices are stored as signed 64-bit minor units and are parsed without floating-point conversion. Accepted input has at most two decimal places and uses a dot as the decimal separator.

The order book stores bids from highest to lowest price and asks from lowest to highest price. Orders at the same price are ordered by their monotonic priority sequence, which provides deterministic FIFO behavior while allowing arbitrary removal in later increments.

The engine keeps mutable order state in an internal registry and exposes copied immutable snapshots. A snapshot remains unchanged when the live book changes later.

Market orders consume the best available prices on the opposite side using FIFO within each price level. Trades use the resting order price, and any quantity left after available liquidity is exhausted is cancelled instead of entering the book.

Limit orders consume only prices equal to or better than their limit. Any remaining quantity rests at the submitted price with its original arrival priority. The matching core preserves `requested = executed + resting + cancelled` for every accepted submission.

The command parser accepts `limit <buy|sell> <price> <qty>`, `market <buy|sell> <qty>`, and `print book`. Keywords are case-insensitive, whitespace is flexible, and numeric input is parsed strictly using culture-independent rules.

Accepted submissions print their generated ID. Consecutive trades at the same price are grouped as `Trade, price: <price>, qty: <quantity>`. Book output displays bids and asks in aligned columns while preserving snapshot priority order.

## Build and test

Restore dependencies, build the solution, and run the test suite:

```sh
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

Run a release build and its tests:

```sh
dotnet build --configuration Release
dotnet test --configuration Release --no-build
```

## Project layout

```text
src/MatchingEngine.Core/          Domain and matching engine
src/MatchingEngine.Cli/           Command parsing and, in later increments, the console application
tests/MatchingEngine.Core.Tests/  Unit and regression tests
tests/MatchingEngine.Cli.Tests/   Command-line interface tests
docs/                             Public design decisions added as the project evolves
```

The core library has no runtime dependencies outside .NET. xUnit is used only for tests.
