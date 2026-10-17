# Matching Engine Challenge

This repository contains an in-memory matching engine developed for the Morgan Stanley Institutional Equity Distribution - Strats software engineering internship challenge.

The engine will process orders for one instrument using price-time priority. The final scope includes limit and market orders, order book visualization, cancellation, amendment, and pegged orders.

## Current status

Milestones M1 and M2 are complete. M3 now includes pegged-order submission, suspension, and automatic repricing from fixed-limit references.

- [x] .NET 10 solution and xUnit test infrastructure
- [x] Exact fixed-point price representation and parsing
- [x] Bid and ask indexes with price-time priority
- [x] Insertion, best-order lookup, ordered enumeration, and removal
- [x] Immutable order book snapshots with order details and remaining quantities
- [x] Market order matching with partial fills and remainder cancellation
- [x] Crossing limit orders with passive remainder handling
- [x] Parsing for limit, market, and print-book commands
- [x] Trade, accepted-order, and order-book output formatting
- [x] Interactive command loop with recoverable input errors
- [x] Cancellation of active resting orders by ID
- [x] Remaining-quantity amendment with priority updates
- [x] Price amendment with matching and combined quantity updates
- [x] Pegged-order reference tracking and suspension
- [x] Automatic pegged-order repricing with preserved priority
- [ ] Stabilized pegged-order matching

Prices are stored as signed 64-bit minor units and are parsed without floating-point conversion. Accepted input has at most two decimal places and uses a dot as the decimal separator.

The order book stores bids from highest to lowest price and asks from lowest to highest price. Orders at the same price are ordered by their monotonic priority sequence, which provides deterministic FIFO behavior while allowing arbitrary removal in later increments.

The engine keeps mutable order state in an internal registry and exposes copied immutable snapshots. A snapshot remains unchanged when the live book changes later.

Market orders consume the best available prices on the opposite side using FIFO within each price level. Trades use the resting order price, and any quantity left after available liquidity is exhausted is cancelled instead of entering the book.

Limit orders consume only prices equal to or better than their limit. Any remaining quantity rests at the submitted price with its original arrival priority. The matching core preserves `requested = executed + resting + cancelled` for every accepted submission.

The command parser accepts limit, market, cancellation, amendment, print-book, and `peg <bid|offer> <buy|sell> <qty>` commands. Keywords are case-insensitive, whitespace is flexible, and numeric input is parsed strictly using culture-independent rules.

Accepted submissions print their generated ID. Consecutive trades at the same price are grouped as `Trade, price: <price>, qty: <quantity>`. Book output displays bids and asks in aligned columns while preserving snapshot priority order.

The command loop processes standard input sequentially and writes results to standard output. Invalid lines produce an error without changing the book or terminating the process. End-of-file exits normally.

Cancelling an active order removes its remaining quantity from both the order registry and the price-time index. Missing, fully executed, or previously cancelled IDs return an error without changing the book.

Amending quantity replaces the order's current remaining quantity. Reductions and no-op amendments preserve time priority, while increases assign a new priority sequence and move the order behind existing orders at the same price. Invalid amendments leave the order book unchanged.

Changing an order's price assigns a new priority sequence and may immediately produce trades under the same rules as a new limit order. Trades use resting counterparty prices, the amended order keeps its ID, and any unmatched quantity rests at the new price.

A pegged order derives its effective price from the best active fixed-price limit on its selected bid or offer reference. Other pegged orders never contribute to that reference. The engine recalculates references after successful submissions, cancellations, amendments, and executions. Repricing preserves the order's priority sequence, including across suspension and reactivation. If no fixed reference exists, the order remains active but suspended outside the executable book and appears in a separate snapshot section. Cancellation and quantity amendment also apply to suspended orders, while direct price amendment is rejected. Reference lookup currently scans active orders in `O(N)` time.

Automatic repricing does not yet execute crossings created by a new reference. Iterative matching and stabilization are the final M3 increment.

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

Run the command-line application after building:

```sh
dotnet run --project src/MatchingEngine.Cli --no-build
```

## Project layout

```text
src/MatchingEngine.Core/          Domain and matching engine
src/MatchingEngine.Cli/           Command-line application, parsing, and output formatting
tests/MatchingEngine.Core.Tests/  Unit and regression tests
tests/MatchingEngine.Cli.Tests/   Command-line interface tests
docs/                             Public design decisions added as the project evolves
```

The core library has no runtime dependencies outside .NET. xUnit is used only for tests.
