# market-snapshot

A small full-stack app that summarizes a stock's intraday trading by day.

## Overview

The goal is a self-hosted .NET API and a React frontend:

- **API:** one endpoint that takes a stock symbol, fetches the last month of
  15-minute intraday data from Yahoo Finance, groups it by day, and returns
  JSON in this shape:

  ```json
  [ { "day": "2009-01-30", "lowAverage": 40.2958, "highAverage": 49.7534, "volume": 49073348 } ]
  ```

- **Frontend:** a React page where the user enters a symbol and sees the
  results as a table, with error handling for invalid symbols and failed
  requests.

Data comes from
`https://query1.finance.yahoo.com/v8/finance/chart/{symbol}?interval=15m&range=1mo`.

## Assumptions

- **`lowAverage` and `highAverage`** are the mean of the 15-minute bar lows and
  highs within each day. **`volume`** is the sum of the bar volumes for that day.
- **Precision:** averages are rounded to 4 decimal places (midpoint rounded away
  from zero), applied once to the final mean. Prices use `decimal` to avoid
  floating-point drift.
- **Date range:** the example URL in the brief has no `range` parameter, so it
  doesn't return a month of data. The API adds `range=1mo`.
- **Grouping:** days are grouped in the exchange's own time zone (for example
  `America/New_York`), not UTC, and returned oldest first.
- **Incomplete bars:** Yahoo can return `null` for a bar's low, high, or volume.
  Any bar with a missing value is skipped entirely, so it doesn't skew the
  averages or the volume total.
- **Unknown or delisted symbols:** when Yahoo reports a symbol as not found or
  possibly delisted, the API returns HTTP 404 with a JSON error object that
  includes the message. The React frontend shows that message to the user.
- **Other failures:** if Yahoo is unreachable, times out, or returns an
  unexpected response, the API returns a 502 error so it isn't mistaken for a
  bad symbol.

## Running locally

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### API

```bash
cd market-snapshot-api
dotnet build
dotnet test
dotnet run --project src/MarketSnapshot.Api
```

_Endpoint usage and frontend instructions to be added._

## Project structure

```
market-snapshot/
├── market-snapshot-api/
│   ├── src/MarketSnapshot.Api/          # Minimal API: Models, Services, Providers, Endpoints
│   └── tests/MarketSnapshot.Api.Tests/  # xUnit tests and Yahoo response fixtures
├── PROMPT_LOG.md                        # AI prompts used, and what was kept or changed
└── README.md
```

## Manual changes

Changes I made by hand, outside the AI:

### Test fixtures
Created both fixtures (`tsla-1mo-15m.json`, `invalid-symbol.json`) by calling the
Yahoo endpoint with curl. The first capture of `invalid-symbol.json` included HTTP
headers from `curl -i`; I noticed and re-captured it with only the JSON body.

### Aggregator implementation
I wrote `DailySummaryAggregator.Aggregate` myself, using the AI-written tests as
the spec and the AI for explanations and hints. I corrected AI examples that
didn't match my model (`Time` is a `DateTimeOffset`; the class is static).
