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
- Design: pure `DailySummaryAggregator` (no I/O) is the TDD core; `IMarketDataProvider` with a Yahoo adapter behind it (the "requirements will grow" story); typed `HttpClient` via `IHttpClientFactory`; validate the symbol (strict regex, length limit) before any URL is built; timeout/retry via the standard resilience handler; ProblemDetails errors; CORS for the UI; health endpoint; OpenAPI. A short-TTL response cache was deferred as an optional extra and is not built.
- Symbol rule: 1-10 characters of letters (any case), digits, `.`, `-` or `^` (`[GeneratedRegex(@"^[A-Za-z0-9.^-]{1,10}\z")]`; `\z`, not `$`, so a trailing newline is rejected). Invalid -> 400 before the provider is called.
- Error mapping: unknown or delisted symbol -> `SymbolNotFoundException` -> 404 with a ProblemDetails message the UI displays; upstream failure, timeout, any non-success status, unrecognized 404 body, malformed JSON, or a 200 with no `chart.result` -> `UpstreamUnavailableException` -> 502. Yahoo returns a real HTTP 404 for unknown symbols; the body's `chart.error.code` must be `"Not Found"`, so a changed endpoint is not misreported as a bad ticker. Caller cancellation propagates unchanged.
- UI error display: 4xx shows the API's `detail` (written for users); 5xx and network failures show friendly messages instead, because the 502 detail is an internal upstream reason. An aborted request is ignored, since a newer search replaced it.

## Models and test conventions (from the existing code)

- `IntradayBar(DateTimeOffset Time, decimal? Low, decimal? High, long? Volume)`: nullable because Yahoo sends nulls.
- `DailySummary(DateOnly Day, decimal LowAverage, decimal HighAverage, long Volume)`.
- `DailySummaryAggregator.Aggregate(IEnumerable<IntradayBar> bars, TimeZoneInfo exchangeTimeZone)` is a static method returning `IReadOnlyList<DailySummary>`. It throws `ArgumentNullException` for null arguments.
- Tests use xUnit + Shouldly. The aggregator tests use a private helper `Bar(string iso, decimal? low, decimal? high, long? volume)` and `TimeZoneInfo.FindSystemTimeZoneById("America/New_York")`. Follow this style in new tests.

## Provider design (implemented)

- `Providers/IMarketDataProvider.cs`: `Task<IntradayData> GetIntradayAsync(string symbol, CancellationToken ct)` and `record IntradayData(IReadOnlyList<IntradayBar> Bars, TimeZoneInfo ExchangeTimeZone)`. The time zone comes back from the provider because the aggregator needs it and Yahoo supplies it in `meta.exchangeTimezoneName`.
- `Providers/ProviderExceptions.cs`: `SymbolNotFoundException` and `UpstreamUnavailableException`.
- `Providers/YahooDtos.cs`: internal Yahoo DTO records (`YahooResponse` > `YahooChart` > `YahooResult` > `YahooMeta`, `timestamp[]`, `YahooIndicators` > `YahooQuote` with `decimal?[]` low/high and `long?[]` volume), deserialized with `JsonSerializerDefaults.Web`. Visible to the test project through `<InternalsVisibleTo>` in the API csproj (decided: keep them internal).
- `YahooProvider.GetIntradayAsync`: 404 -> check `chart.error`; any other non-success -> 502; deserialize; `Result is not [var result, ..]` -> 502 (decided: a 200 with no result is an upstream fault, since unknown symbols already arrive as a real 404); then `Map`.
- `internal static IntradayData Map(YahooResult)`: pure mapper, no HTTP. Zips `timestamp` (Unix seconds -> `DateTimeOffset.FromUnixTimeSeconds`) with low/high/volume; guards missing arrays and unequal lengths; keeps nulls as nulls (the aggregator drops them); drops zero-volume bars; resolves the time zone (bad or missing name -> upstream error).
- Registration in `Program.cs`: `AddHttpClient<IMarketDataProvider, YahooProvider>` with base address `https://query1.finance.yahoo.com/` and a browser-like User-Agent, plus `AddStandardResilienceHandler` (2 retries, 5 s per attempt, 15 s total). Its timeout throws Polly's `TimeoutRejectedException`, which the provider maps to 502.

## UI design (implemented)

- `src/api/`: `dailySummaries.ts` (`fetchDailySummaries(symbol, signal?)`, the only place that knows HTTP status rules), `types.ts` (shared `DailySummary` and `ProblemDetails`; never redeclare them), `config.ts` (`VITE_API_BASE_URL`, default `http://localhost:5015`).
- `App.tsx` is the container: `rows` (`null` = no search yet, `[]` = no data), `error`, `loading`, and an `AbortController` in a `useRef` so a newer search cancels the older one. `components/SymbolSearchForm` and `components/DailySummaryTable` are presentational and take props only.
- Display: prices with `toFixed(4)`, volume with `toLocaleString('en-US')`; table headers in sentence case (Day / Low average / High average / Volume).
- Tests query by label and role. MSW intercepts `fetch`; unhandled requests fail the test (`onUnhandledFrame: 'error'`, MSW 3's name for the old `onUnhandledRequest`). Vitest doesn't type-check, so also run `npx tsc`.

## Findings from the real TSLA response (`range=1mo`, `interval=15m`)

- 547 bars, 21 trading days (2026-10-12 to 2026-11-09); 26 bars per full day (09:30-15:45 ET).
- Parallel arrays under `chart.result[0]`: `timestamp[]` and `indicators.quote[0].{low,high,open,close,volume}`. No nulls in this sample, but still handle them (test with an edited fixture).
- The last day has a stray 27th bar at 16:00 ET (timestamp 1794258000, equal to `regular.end` in the metadata) with `volume: 0` and open/high/low/close all 378.05: a closing-price snapshot, not a real bar. Confirmed in the fixture by the owner. Decided by the owner: zero-volume bars are dropped in the Yahoo mapper, not the aggregator, since it is a Yahoo quirk and the aggregator's rules stay generic. Covered by the mapper tests (546 bars, last bar at 15:45 ET on 2026-11-09) and documented in the README Assumptions and Manual changes.
- The trading days span the US DST change: clocks fell back on Sunday 2026-11-01, so the fixture runs from 10/30 to 11/02 around it. No UTC offset may be hard-coded.
- Golden values from a prototype, verified against the C# output (fixture -> `Map` -> `Aggregate`) and locked in by `PipelineGoldenValueTests`: 21 days; 2026-10-12 -> 378.7496 / 379.8398 / 17,558,067; 2026-10-13 -> 374.4611 / 375.5825 / 21,744,117; 2026-11-05 -> 342.6944 / 347.1856 / 70,120,650; 2026-11-09 (after dropping the zero-volume bar) -> 376.6860 / 379.0226 / 94,947,412.

## Status

- Feature-complete MVP, tested end to end by the owner with both apps running.
- API: aggregator, Yahoo mapper and HTTP handling, and the endpoint, all implemented by the owner against AI-written tests. Tests cover the aggregator, the mapper, provider HTTP behaviour (stub `HttpMessageHandler`), DI registration (User-Agent, base address), endpoint integration (`WebApplicationFactory`: JSON shape, 400/404/502, CORS), health, and the fixture-to-summary pipeline with golden values.
- UI: `fetchDailySummaries` written by the owner; `App`, the search form and the table written by the owner, except `App.handleSearch`, which the AI wrote at the owner's request. 19 Vitest/RTL/MSW tests.
- Fixtures in `tests/MarketSnapshot.Api.Tests/Fixtures/`, copied to output by the test csproj: `tsla-1mo-15m.json` (success) and `invalid-symbol.json` (body of Yahoo's HTTP 404 for an unknown symbol).
- README covers overview, assumptions, run instructions for both apps, endpoint usage, project structure, and Manual changes.
- Known leftover: the aggregator's CS8629 nullable warning (the `.Value` reads after the null filter), for a refactor commit.
- Not built (optional): response cache, Docker, GitHub Actions CI, chart.

## Working agreements

- Test-driven: red, green, refactor as separate small commits with clear messages, so the history shows the rhythm. Tests never call the live Yahoo API; they use the fixtures.
- Scope discipline: required features first; extras only if cheap. No over-engineering.
- No secrets or machine-specific files in commits; check `git status` for `bin/` and `obj/` before committing.
- Environment: Windows, Git Bash. Use bash commands (`touch`, `&&` work). Run tests with `dotnet test` from `market-snapshot-api/` (or pass the test project path); filter with `--filter "FullyQualifiedName~SomeName"`.

## PROMPT_LOG.md and manual changes: honesty rules

- `PROMPT_LOG.md` is a graded deliverable written by the owner. When you produce significant code or design advice, propose a draft entry (prompt, why, kept/changed/rejected) for the owner to edit. Never write entries claiming actions the owner didn't take, and never leave `[confirm]` placeholders in the committed file.
- Current log: entries 1-21, from the brief review through the React UI. The work split from entry 10 onwards: the AI writes tests and plumbing, the owner writes the logic with AI review. Entry 21 records that the AI wrote `App.handleSearch`. Trivial prompts (command lookups, syntax questions) are omitted, as the log's header says.
- Test code or advice written by AI belongs in the PROMPT_LOG, not in Manual changes. For example, the DST test was AI-written and the owner pasted it, so it is entry 8 only. The sort order needed no change: the AI-written test already expected ascending, so it is documented only in the README Assumptions ("returned oldest first").
- README Manual changes should list only what the owner did by hand: the two fixtures (including re-capturing `invalid-symbol.json` after noticing `curl -i` headers), the aggregator, the Yahoo mapper, the zero-volume decision, `GetIntradayAsync`, the daily summary endpoint, and the React work (stating that `handleSearch` was AI-written).
- Going forward: one log entry per significant AI-assisted step, written right after the step and committed after its code.
