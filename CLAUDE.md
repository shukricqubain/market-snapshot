# CLAUDE.md — market-snapshot

Take-home assessment for Prometheus (full-stack, AI-collaborative). Expected effort is ~4-5 hours: a tight, finished MVP beats an ambitious unfinished one. The owner must understand and be able to defend every line, so explain non-obvious choices briefly as you work.

## How to work with the owner

- The owner (Shukri) wants to be hands-on. Default to guiding: explain concepts, give hints, review what he writes, and point at the failing test. Write full implementations only when he asks.
- He is an experienced Angular/TypeScript/Node developer but newer to C#/.NET. Briefly explain C# idioms (nullable value types, LINQ laziness, records, `internal` visibility) the first time they come up.
- Before relying on a claim about his code, ask him to paste the file. Don't assume types, property names, or constructor order.
- Verify AI-written code against the real model before suggesting it. Past mistakes to avoid: using a `Timestamp` property (it is `Time`), `ConvertTimeFromUtc` (the time is a `DateTimeOffset`), and `new DailySummaryAggregator()` (the class is static).

## The brief (source of truth)

- Backend: self-hosted C# .NET 8+ (or Node/TS) API. One endpoint that takes a stock symbol, queries intraday data for the last month, groups by day, and returns JSON in exactly this shape and precision:

  ```json
  [ { "day": "2009-01-30", "lowAverage": 40.2958, "highAverage": 49.7534, "volume": 49073348 } ]
  ```

  Data source: `https://query1.finance.yahoo.com/v8/finance/chart/TSLA?interval=15m` with a browser-like `User-Agent` header (Yahoo rejects requests without one). The brief's example URL has no `range`; `range=1mo` is needed to get a month of 15m bars. Note: `interval` is the bar size, `range` is how far back. `interval=1mo` would return monthly bars, which is wrong.
- Frontend: React (chosen; matches the team's stack). Symbol input, results as table and/or chart, basic error handling for invalid symbols and failed requests.
- Deliverables: public GitHub repo; `README.md` (build/run both sides locally); `PROMPT_LOG.md` (per entry: prompt sent, why, what was kept/changed/rejected and why); a section describing manual changes made outside AI.
- Expectations: fully functional, production-quality, SOLID, maintainable, assume the MVP will grow, thoroughly tested (tests must be in the repo), tested end to end before submitting. AI use is explicitly encouraged; they grade how well the owner collaborates with it.

## Decisions

- Stack: .NET 10 (current LTS) minimal API + xUnit + Shouldly + `Microsoft.AspNetCore.Mvc.Testing`; React + TypeScript (Vite), tested with Vitest + React Testing Library + MSW.
- Layout: backend in `market-snapshot-api/` (`src/MarketSnapshot.Api`, `tests/MarketSnapshot.Api.Tests`); UI in `market-snapshot-ui/`. README, PROMPT_LOG.md, .gitignore at the root. Single API project with folders `Models/`, `Services/`, `Providers/`, `Endpoints/`; don't split into multiple assemblies. Solution file is `.slnx`.
- Aggregation (stated in the README): `lowAverage` = mean of the 15-minute bar lows for the day, `highAverage` = mean of the highs, `volume` = sum of volumes. Round averages to 4 decimals with `MidpointRounding.AwayFromZero`, applied once to the final mean; use `decimal`; volume is `long`.
- A bar with any null low/high/volume is dropped entirely (filtered before grouping, so a day with no valid bars simply produces no row). Days are grouped in the exchange time zone (`meta.exchangeTimezoneName`), returned in ascending order (oldest first).
- `DailySummary.Day` (`DateOnly`) serializes as `"day": "yyyy-MM-dd"`; confirm this in the endpoint integration tests.
- Design: pure `DailySummaryAggregator` (no I/O) is the TDD core; `IMarketDataProvider` with a Yahoo adapter behind it (the "requirements will grow" story); typed `HttpClient` via `IHttpClientFactory`; validate the symbol (strict regex, length limit) before any URL is built; short-TTL cache; timeout/retry; ProblemDetails errors; CORS for the UI; health endpoint; OpenAPI.
- Error mapping: unknown or delisted symbol -> `SymbolNotFoundException` -> 404 with a ProblemDetails message the UI displays; upstream failure, timeout, 429, or unrecognized 404 body -> `UpstreamUnavailableException` -> 502/503. Yahoo returns a real HTTP 404 for unknown symbols; check the body's `chart.error` so a changed endpoint is not misreported as a bad ticker.

## Models and test conventions (from the existing code)

- `IntradayBar(DateTimeOffset Time, decimal? Low, decimal? High, long? Volume)`: nullable because Yahoo sends nulls.
- `DailySummary(DateOnly Day, decimal LowAverage, decimal HighAverage, long Volume)`.
- `DailySummaryAggregator.Aggregate(IEnumerable<IntradayBar> bars, TimeZoneInfo exchangeTimeZone)` is a static method returning `IReadOnlyList<DailySummary>`. It throws `ArgumentNullException` for null arguments.
- Tests use xUnit + Shouldly. The aggregator tests use a private helper `Bar(string iso, decimal? low, decimal? high, long? volume)` and `TimeZoneInfo.FindSystemTimeZoneById("America/New_York")`. Follow this style in new tests.

## Provider design (in progress)

Skeleton created in `Providers/YahooProvider.cs`; planned split into one public type per file:

- `IMarketDataProvider.cs`: `Task<IntradayData> GetIntradayAsync(string symbol, CancellationToken ct)` and `record IntradayData(IReadOnlyList<IntradayBar> Bars, TimeZoneInfo ExchangeTimeZone)`. The time zone comes back from the provider because the aggregator needs it and Yahoo supplies it in `meta.exchangeTimezoneName`.
- `SymbolNotFoundException` and `UpstreamUnavailableException` (separate files or one `ProviderExceptions.cs`).
- `YahooProvider.cs`: internal Yahoo DTO records (`YahooResponse` > `YahooChart` > `YahooResult` > `YahooMeta`, `timestamp[]`, `YahooIndicators` > `YahooQuote` with `decimal?[]` low/high and `long?[]` volume), deserialized with `JsonSerializerDefaults.Web`.
- `internal static IntradayData Map(YahooResult)`: pure mapper, no HTTP. Zips `timestamp` (Unix seconds -> `DateTimeOffset.FromUnixTimeSeconds`) with low/high/volume; guards missing arrays and unequal lengths; keeps nulls as nulls (the aggregator drops them); resolves the time zone (bad or missing name -> upstream error).
- Registration in `Program.cs`: `AddHttpClient<IMarketDataProvider, YahooProvider>` with base address `https://query1.finance.yahoo.com/`, a browser-like User-Agent, and a 10-second timeout.
- Open: make the DTOs/`Map` visible to tests via `[assembly: InternalsVisibleTo("MarketSnapshot.Api.Tests")]`, or make them public. The owner decides.
- Open: whether a successful response with an empty or null `chart.result` is a 404 or a 502. Decide and test it.

## Findings from the real TSLA response (`range=1mo`, `interval=15m`)

- 547 bars, 21 trading days (2026-10-12 to 2026-11-09); 26 bars per full day (09:30-15:45 ET).
- Parallel arrays under `chart.result[0]`: `timestamp[]` and `indicators.quote[0].{low,high,open,close,volume}`. No nulls in this sample, but still handle them (test with an edited fixture).
- The last day has a stray 27th bar at 16:00 ET (timestamp 1794258000, equal to `regular.end` in the metadata) with `volume: 0` and open/high/low/close all 378.05: a closing-price snapshot, not a real bar. Confirmed in the fixture by the owner. Open decision: exclude zero-volume bars (leaning yes). Handle it in the Yahoo mapper, not the aggregator, since it is a Yahoo quirk and the aggregator's rules stay generic. Add a mapper test against the fixture (for example, 546 bars, last bar at 15:45 ET on 2026-11-09), then document it in the README Assumptions and the PROMPT_LOG/manual-changes entries as appropriate.
- The trading days span the US DST change: clocks fell back on Sunday 2026-11-01, so the fixture runs from 10/30 to 11/02 around it. No UTC offset may be hard-coded.
- Golden values from a prototype (verify against the C# output, don't just copy): 2026-10-12 -> 378.7496 / 379.8398 / 17,558,067; 2026-10-13 -> 374.4611 / 375.5825 / 21,744,117; 2026-11-05 -> 342.6944 / 347.1856 / 70,120,650. Don't use 2026-11-09 until the zero-volume decision is made.

## Status

- Done and pushed: solution scaffolded on net10.0; `DailySummaryAggregator` implemented by the owner and all aggregator tests green: empty input, single day, multiple days ascending and out of order, null bar skipped entirely, rounding away from zero, grouping by exchange-local date (not UTC), and the DST test `Aggregate_AcrossDstChange_GroupsByLocalDate`.
- Fixtures in `tests/MarketSnapshot.Api.Tests/Fixtures/`, copied to output by the test csproj: `tsla-1mo-15m.json` (success) and `invalid-symbol.json` (body of Yahoo's HTTP 404 for an unknown symbol).
- README has overview, assumptions, run instructions for the API, project structure, and a manual-changes section. The 404/502 assumptions describe behavior that is not built yet; make sure the provider and endpoint match them before submitting.
- Empty `Endpoints/` folder (with `.gitkeep`). `Providers/YahooProvider.cs` exists as a skeleton with TODOs.
- Next, in order: (1) Yahoo mapper tests and implementation against the fixtures, including the zero-volume decision; (2) provider HTTP tests with a stub `HttpMessageHandler` (success, 404 mapping via `chart.error`, 5xx, 429, timeout, malformed payload, null `result`; assert the User-Agent and `range`/`interval` query params); (3) endpoint integration tests with `WebApplicationFactory` (200/400/404/502); (4) production touches; (5) React UI with tests; (6) finish README run instructions and PROMPT_LOG; (7) optional: Docker, GitHub Actions CI, chart.

## Working agreements

- Test-driven: red, green, refactor as separate small commits with clear messages, so the history shows the rhythm. Tests never call the live Yahoo API; they use the fixtures.
- Scope discipline: required features first; extras only if cheap. No over-engineering.
- No secrets or machine-specific files in commits; check `git status` for `bin/` and `obj/` before committing.
- Environment: Windows, Git Bash. Use bash commands (`touch`, `&&` work). Run tests with `dotnet test` from `market-snapshot-api/` (or pass the test project path); filter with `--filter "FullyQualifiedName~SomeName"`.

## PROMPT_LOG.md and manual changes: honesty rules

- `PROMPT_LOG.md` is a graded deliverable written by the owner. When you produce significant code or design advice, propose a draft entry (prompt, why, kept/changed/rejected) for the owner to edit. Never write entries claiming actions the owner didn't take, and never leave `[confirm]` placeholders in the committed file.
- Current log: entries 1-8 (brief review, fixture analysis, cross-check of expected values, structure, starter tests, guided aggregator implementation, null/rounding failures, DST test). Trivial prompts (command lookups, syntax questions) are omitted, as the log's header says.
- Test code or advice written by AI belongs in the PROMPT_LOG, not in Manual changes. For example, the DST test was AI-written and the owner pasted it, so it is entry 8 only. The sort order needed no change: the AI-written test already expected ascending, so it is documented only in the README Assumptions ("returned oldest first").
- README Manual changes should list only what the owner did by hand: currently the two fixtures (including re-capturing `invalid-symbol.json` after noticing `curl -i` headers) and the aggregator implementation.
- Pending entries and items: the zero-volume bar decision (README assumption, mapper test, and a manual-changes entry if the owner makes the call himself); optionally an entry on `interval=1mo` vs `range=1mo`; one entry for the provider design and for each significant AI-assisted step from here on.
