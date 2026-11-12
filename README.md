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
- **Closing snapshot bar:** Yahoo appends a bar at the 16:00 ET close with
  volume `0` and identical open/high/low/close: a closing-price snapshot, not
  15 minutes of trading. Zero-volume bars are dropped when mapping Yahoo's
  response, so they don't pull the day's averages toward the closing price.
  Bars with `null` values are kept at that stage and skipped by the
  aggregation rule above.

## Running locally

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) LTS: 22.12+, 24, or 26 (required by the
  current Vite and Vitest)

Run the API and the UI in two terminals, starting from the repository root.

### API

```bash
cd market-snapshot-api
dotnet build
dotnet test
dotnet run --project src/MarketSnapshot.Api
```

The API listens on `http://localhost:5015`.

- `GET /api/stocks/{symbol}/daily-summaries`, for example
  `http://localhost:5015/api/stocks/TSLA/daily-summaries`, returns one entry per
  trading day, oldest first:

  ```json
  [ { "day": "2026-10-12", "lowAverage": 378.7496, "highAverage": 379.8398, "volume": 17558067 } ]
  ```

  A symbol is 1-10 characters of letters, digits, `.`, `-` or `^` (for example
  `BRK.B`, `^GSPC`, `BTC-USD`). Errors are returned as
  [ProblemDetails](https://www.rfc-editor.org/rfc/rfc9457) JSON: `400` for an
  invalid symbol, `404` for an unknown symbol, and `502` when Yahoo Finance is
  unavailable or returns an unexpected response.
- `GET /health` is a health check.
- `GET /openapi/v1.json` is the OpenAPI description (Development only).

The tests never call Yahoo; they use recorded responses in
`tests/MarketSnapshot.Api.Tests/Fixtures/`.

### UI

```bash
cd market-snapshot-ui
npm ci
npm run dev
```

Open `http://localhost:5173`, enter a symbol such as `TSLA`, and press Search.
The dev server uses port 5173 on purpose: it's the origin the API allows through
CORS (`Cors:AllowedOrigins` in `appsettings.Development.json`).

The UI calls `http://localhost:5015` by default. To point it at a different API,
copy `.env.example` to `.env.local` and set `VITE_API_BASE_URL`.

Other scripts:

```bash
npm test          # run the tests once (Vitest, React Testing Library, MSW)
npm run test:watch
npm run build     # type-check with tsc, then build to dist/
```

The UI tests mock the API with MSW, so the API doesn't need to be running for
them.

## Project structure

```
market-snapshot/
├── market-snapshot-api/
│   ├── src/MarketSnapshot.Api/          # Minimal API: Models, Services, Providers, Endpoints
│   └── tests/MarketSnapshot.Api.Tests/  # xUnit tests and Yahoo response fixtures
├── market-snapshot-ui/
│   └── src/
│       ├── api/                         # fetch client, shared types, API URL config
│       ├── components/                  # SymbolSearchForm, DailySummaryTable
│       ├── test/                        # MSW server, handlers and test setup
│       └── App.tsx                      # state, fetching and cancellation
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

### Yahoo mapper implementation
I wrote `YahooProvider.Map` and its time zone helper myself, using the
AI-written tests as the spec and the AI for reviews and hints. Mistakes I
fixed along the way: chaining `!=` for the length check, an `async` helper
with nothing to await (and not `static`, so `Map` couldn't call it), and
`volume[i].Value`, which throws on null bars instead of letting them reach
the aggregator.

### Zero-volume closing bar
Investigated the 16:00 zero-volume bar in the fixture and decided to exclude it in the mapper
as it would throw off the daily averages.

### GetIntradayAsync implementation
I wrote the response handling in `YahooProvider.GetIntradayAsync` myself, with
the AI-written HTTP tests as the spec and the AI for hints and reviews, then
cleaned up its error messages in a separate refactor commit. The tests caught a
mistake I made during that refactor (a typo in the matched "Not Found" code).

### Daily summary endpoint
I wrote the handler in `DailySummaryEndpoints.cs` myself, with the AI-written
endpoint tests as the spec and the AI for reviews. With the AI's help I replaced
my planned length-and-characters check with one anchored `[GeneratedRegex]`
ending in `\z`. Review caught my mistakes: an unreturned 400 result, awaiting a
synchronous method, and a missing `partial`. Details are in PROMPT_LOG entry 18.

### React UI
I wrote `fetchDailySummaries`, the component split, `SymbolSearchForm`,
`DailySummaryTable`, and `App`'s state and rendering myself, with the AI-written
UI tests as the spec and the AI for reviews. The exception is `App.handleSearch`
(cancelling the previous request, loading and error state): the AI wrote it at
my request after my own attempt didn't work, and explained how it works. Details
are in PROMPT_LOG entries 20 and 21.
