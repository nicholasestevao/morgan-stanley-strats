# Matching Engine Challenge

This repository contains an in-memory matching engine developed for the Morgan Stanley Institutional Equity Distribution - Strats software engineering internship challenge.

The engine will process orders for one instrument using price-time priority. The final scope includes limit and market orders, order book visualization, cancellation, amendment, and pegged orders.

## Current status

The project now includes the internal order registry, price-time indexes, and immutable book snapshots used by the matching engine.

- [x] .NET 10 solution and xUnit test infrastructure
- [x] Exact fixed-point price representation and parsing
- [x] Bid and ask indexes with price-time priority
- [x] Insertion, best-order lookup, ordered enumeration, and removal
- [x] Immutable order book snapshots with order details and remaining quantities
- [ ] Limit and market order matching
- [ ] Command-line interface and book visualization
- [ ] Order cancellation and amendment
- [ ] Pegged orders

Prices are stored as signed 64-bit minor units and are parsed without floating-point conversion. Accepted input has at most two decimal places and uses a dot as the decimal separator.

The order book stores bids from highest to lowest price and asks from lowest to highest price. Orders at the same price are ordered by their monotonic priority sequence, which provides deterministic FIFO behavior while allowing arbitrary removal in later increments.

The engine keeps mutable order state in an internal registry and exposes copied immutable snapshots. A snapshot remains unchanged when the live book changes later. Public order submission will be introduced together with matching so crossing orders are handled correctly from the first exposed API.

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
tests/MatchingEngine.Core.Tests/  Unit and regression tests
docs/                             Public design decisions added as the project evolves
```

The core library has no runtime dependencies outside .NET. xUnit is used only for tests.
