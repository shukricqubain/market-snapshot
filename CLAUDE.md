# CLAUDE.md — market-snapshot

Take-home assessment for Prometheus (full-stack, AI-collaborative). Expected effort is ~4-5 hours: a tight, finished MVP beats an ambitious unfinished one. The owner must understand and be able to defend every line, so explain non-obvious choices briefly as you work.

## The brief (source of truth)

- Backend: self-hosted C# .NET 8+ (or Node/TS) API. One endpoint that takes a stock symbol, queries intraday data for the last month, groups by day, and returns JSON in exactly this shape and precision:

  ```json
  [ { "day": "2009-01-30", "lowAverage": 40.2958, "highAverage": 49.7534, "volume": 49073348 } ]
  ```

  Data source: `https://query1.finance.yahoo.com/v8/finance/chart/TSLA?interval=15m` with a browser-like `User-Agent` header (Yahoo rejects requests without one). The brief's example URL has no `range`; `range=1mo` is needed to get a month of 15m bars.
- Frontend: React (chosen; matches the team's stack). Symbol input, results as table and/or chart, basic error handling for invalid symbols and failed requests.
- Deliverables: public GitHub repo; `README.md` (build/run both sides locally); `PROMPT_LOG.md` (per entry: prompt sent, why, what was kept/changed/rejected and why); a section describing manual changes made outside AI.
- Expectations: fully functional, production-quality, SOLID, maintainable, assume the MVP will grow, thoroughly tested (tests must be in the repo), tested end to end before submitting.

## Decisions

- Stack: .NET 10 (current LTS) minimal API + xUnit + Shouldly + `Microsoft.AspNetCore.Mvc.Testing`; React + TypeScript (Vite), tested with Vitest + React Testing Library + MSW.
- Layout: backend in `market-snapshot-api/` (`src/MarketSnapshot.Api`, `tests/MarketSnapshot.Api.Tests`); UI in `market-snapshot-ui/`. README, PROMPT_LOG.md, .gitignore at the root. Single API project with folders `Models/`, `Services/`, `Providers/`, `Endpoints/`; don't split into multiple assemblies.
- Aggregation (stated in the README): `lowAverage` = mean of the 15-minute bar lows for the day, `highAverage` = mean of the highs, `volume` = sum of volumes. Round averages to 4 decimals with `MidpointRounding.AwayFromZero`, applied once to the final mean; use `decimal`; volume is `long`.
- A bar with any null low/high/volume is dropped entirely. Days are grouped in the exchange time zone (`meta.exchangeTimezoneName`), returned in ascending order.
- `DailySummary.Day` (`DateOnly`) serializes as `"day": "yyyy-MM-dd"`; confirm this in the endpoint integration tests.
- Design: pure `DailySummaryAggregator` (no I/O) is the TDD core; `IMarketDataProvider` with a Yahoo adapter behind it (the "requirements will grow" story); typed `HttpClient` via `IHttpClientFactory`; validate the symbol (strict regex, length limit); short-TTL cache; timeout/retry; ProblemDetails errors; CORS for the UI; health endpoint; OpenAPI.
- Error mapping: unknown or delisted symbol -> `SymbolNotFoundException` -> 404 with a ProblemDetails message the UI displays; upstream failure, timeout, 429, or unrecognized 404 body -> 502/503. Yahoo returns a real HTTP 404 for unknown symbols; check the body's `chart.error` so a changed endpoint is not misreported as a bad ticker.

## Findings from the real TSLA response (`range=1mo`, `interval=15m`)

- 547 bars, 21 trading days (2026-10-12 to 2026-11-09); 26 bars per full day (09:30-15:45 ET).
- Parallel arrays under `chart.result[0]`: `timestamp[]` and `indicators.quote[0].{low,high,open,close,volume}`. No nulls in this sample, but still handle them (test with an edited fixture).
- The last day has a stray 27th bar at 16:00 ET with `volume: 0` and low == high: a price snapshot, not a real bar. Open decision: exclude zero-volume / off-session bars (leaning yes). Decide, add a test, and document it in the README.
- The data spans the DST change (10/30 -> 11/02); include a test so no UTC offset is hard-coded.
- Golden values from a prototype (verify against the C# output, don't just copy): 2026-10-12 -> 378.7496 / 379.8398 / 17,558,067; 2026-10-13 -> 374.4611 / 375.5825 / 21,744,117; 2026-11-05 -> 342.6944 / 347.1856 / 70,120,650. Don't use 2026-11-09 until the zero-volume decision is made.

## Status

- Done: solution scaffolded and building on net10.0; six failing aggregator tests (red). Stubs: `IntradayBar`, `DailySummary` (property `Day`), `DailySummaryAggregator` (throws `NotImplementedException`). Empty `Providers/` and `Endpoints/` folders (with `.gitkeep`). README has overview, assumptions and manual-changes sections.
- Fixtures in `tests/MarketSnapshot.Api.Tests/Fixtures/`, copied to output by the test csproj: `tsla-1mo-15m.json` (success) and `invalid-symbol.json` (body of Yahoo's HTTP 404 for an unknown symbol).
- Next, in order: (1) make the aggregator tests green, then refactor, as separate commits; (2) Yahoo provider tests against the fixtures with a stub `HttpMessageHandler` (success, 404 mapping, 5xx, malformed payload, null `result`; assert the User-Agent and `range`/`interval` query params); (3) endpoint integration tests with `WebApplicationFactory` (200/400/404/502); (4) production touches; (5) React UI with tests; (6) finish README run instructions and PROMPT_LOG; (7) optional: Docker, GitHub Actions CI, chart.

## Working agreements

- Test-driven: red, green, refactor as separate small commits with clear messages, so the history shows the rhythm. Tests never call the live Yahoo API; they use the fixtures.
- Scope discipline: required features first; extras only if cheap. No over-engineering.
- No secrets or machine-specific files in commits; check `git status` for `bin/` and `obj/` before committing.
- Environment: Windows, Git Bash. Use bash commands (`touch`, `&&` work).
- `PROMPT_LOG.md` is a graded deliverable written by the owner. When you produce significant code or design advice, propose a draft entry (prompt, why, kept/changed/rejected) for the owner to edit. Never write entries claiming actions the owner didn't take. Keep the log curated and honest: include AI mistakes and what was changed, and omit trivial prompts.
- Anything changed by hand outside AI goes in the README's manual-changes section.
