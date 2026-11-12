# Prompt Log

> Trivial prompts (command lookups, typo fixes, rephrasings, scheduling and
> email wording) are omitted. Every prompt that influenced shipped code or a
> design decision is included.

## 1. Reviewing the brief for ambiguities and risks
- **Prompt:** "Let's examine the task at hand so nothing gets missed, here's the
  file." (with the assessment PDF attached)
- **Why:** I wanted to find gaps in the spec before writing code.
- **AI output:** Flagged that `lowAverage`/`highAverage` are ambiguous, that the
  example URL has no `range` parameter, that Yahoo arrays can contain nulls, and
  that invalid symbols and upstream failures need distinct error handling.
- **Kept:** The list of ambiguities. I documented my interpretation (mean of
  15-minute lows/highs per day, volume summed) as an assumption in the README.
- **Changed/rejected:** The AI suggested Node/Angular as an option. I stayed with
  .NET + React because the job posting names that stack. The AI also suggested
  reaching out to clarify `lowAverage`/`highAverage`; I settled that myself
  once I saw the real data (see entry 2).

## 2. Checking the AI's claims about the Yahoo endpoint against real data
- **Prompt:** Shared my saved curl output of the `range=1mo`, `interval=15m`
  TSLA response and asked the AI to analyze it.
- **Why:** The AI's earlier claims about the API were from memory, so I checked
  them against real data before writing code. I also wanted to know the actual
  shape of the response (how many bars per day, whether nulls appear, any odd
  records) so I could pick test cases and use the file as a test fixture.
- **AI output:** 547 bars over 21 trading days, 26 bars per full day, no nulls in
  this sample, plus a stray 16:00 bar with zero volume on the current day.
- **Kept:** The fixture as test data, and the zero-volume-bar finding, which I
  hadn't noticed.
- **Changed/rejected:** The AI had overstated the time-zone risk. Regular-session
  bars land on the same date in UTC and New York, so the real risk is lower than
  claimed. I still group by exchange time zone, and a later test covers the
  2026-11-01 DST change (see entry 8).
  Looking at this output, I also rejected the AI's earlier suggestion (entry 1)
  to reach out about what `lowAverage` and `highAverage` mean. Each 15-minute
  bar has its own `low` and `high`, so after grouping by day the natural reading
  is the mean of those values within each day. I stated it as an assumption in
  the README instead of waiting on an answer.

## 3. Cross-checking expected daily values
- **Prompt:** Asked for a prototype aggregation of the fixture.
- **Why:** I wanted independent expected values to check my implementation against.
- **AI output:** A Python script and per-day results, for example 2026-10-12:
  low 378.7496, high 379.8398, volume 17,558,067.
- **Kept:** Used the values as a cross-check only. I did not copy them into tests
  without confirming them against the fixture.

## 4. Project structure and scaffolding
- **Prompt:** "I'm planning on making the project structure like so: the root
  directory, and the API and UI have their own subfolders. The API will have
  folders for models, services, controllers; the UI will have a simple
  component for the chart."
- **Why:** I wanted feedback on the layout before scaffolding, since it is hard
  to change later.
- **AI output:** A src/tests layout inside the API folder, one API project with
  `Models/`, `Services/`, `Providers/`, and `Endpoints/` folders, and the
  `dotnet new` commands to scaffold it.
- **Kept:** The root with separate API and UI subfolders, the src/tests split,
  and a single API project. I also took the AI's change from `Controllers/` to
  `Endpoints/`, since a minimal API maps routes directly rather than using
  controller classes, and its added `Providers/` folder for the Yahoo client.
- **Changed/rejected:** The AI proposed .NET 8. I'm on .NET 10 (current LTS), so
  I kept it. The AI first flagged the `.slnx` solution format as a compatibility
  risk, then withdrew the concern. I kept `.slnx`.

## 5. Test list and first failing tests (TDD)
- **Prompt:** Asked for a starter test list for the pure aggregation function.
- **AI output:** Six xUnit tests (empty input, single day, multiple days in
  order, null bars, rounding, exchange-time-zone grouping) with stub types so
  they compile and fail.
- **Kept:** The tests and design decisions: a bar with any null field is
  dropped entirely, rounding is applied once to the final mean using
  `MidpointRounding.AwayFromZero`, and `decimal` is used for prices.
- **Result:** All six tests existed and failed (the aggregator threw
  `NotImplementedException`) before I wrote any implementation.

## 6. Implementing the aggregator (guided, not generated)
- **Prompt:** Pasted the `Aggregate` signature and asked what each part of it
  means and how to approach it, asking for hints instead of a solution.
- **Why:** I wanted to write the implementation myself and use the AI as a guide.
- **AI output:** Explained the signature (static class, `IEnumerable` input,
  `TimeZoneInfo` implying exchange-local days) and suggested a
  convert-group-compute-round-sort pipeline.
- **Kept:** Grouping by a `DateOnly` built from
  `TimeZoneInfo.ConvertTime(bar.Time, exchangeTimeZone).DateTime`, and
  collecting summaries into a list returned after the loop.
- **Changed/rejected:** The AI's examples used a `Timestamp` property,
  `ConvertTimeFromUtc`, and `new DailySummaryAggregator()`. None fit my code
  (the property is `Time`, a `DateTimeOffset`, and the class is static), so I
  corrected each against the real model. I rejected grouping by a formatted
  date string: it sorts as text and adds nothing.

## 7. Null handling and rounding failures
- **Prompt:** Pasted my implementation and the two failing test outputs.
- **Why:** The null test and the rounding test failed and I didn't understand why.
- **AI output:** Explained that `Math.Round` defaults to banker's rounding, so
  the `AwayFromZero` test failed, and that "skipped entirely" means dropping
  the bar, not the day.
- **Kept:** `MidpointRounding.AwayFromZero` on both averages, and filtering out
  any bar with a null field before grouping.
- **Changed/rejected:** The AI's earlier advice was to skip only all-null days
  and to treat volume as null-safe. Both were wrong for this spec, and the
  test name caught them. I also removed the leftover debug `Console.WriteLine`.

## 8. DST grouping test
- **Prompt:** Pointed out that my log claimed DST coverage but no test had it,
  and asked for one.
- **Why:** All six provided tests used October (EDT) dates, so nothing exercised
  the 2026-11-01 fall-back.
- **AI output:** `Aggregate_AcrossDstChange_GroupsByLocalDate`, with two bars
  straddling local midnight after the clocks change (04:30 and 05:30 UTC on 11/02,
  which are 23:30 on 11/01 and 00:30 on 11/02 in New York).
- **Kept:** The test as written.
- **Changed/rejected:** Rejected my first single-bar version for the two-bar one,
  because a hard-coded summer offset would still pass with one bar.

## 9. Provider design and DI registration review
- **Prompt:** Shared the updated CLAUDE.md, my `Program.cs` registration, and the
  `YahooProvider` skeleton I was about to work on, and asked for a review. The
  skeleton (contract, exceptions, Yahoo DTOs, and TODOs for each response case)
  was generated by the AI in an earlier session; the TODOs became my spec for
  `Map` and `GetIntradayAsync`.
- **Why:** I wanted the provider's shape checked before writing any logic into it.
- **AI output:** Found two bugs in my `Program.cs`: the `AddHttpClient`
  registration came after `app.Run()`, so it never ran and services can't be
  added after `Build()`, and the `MarketSnapshot.Api.Providers` using was
  missing. Explained what a typed `HttpClient` from `IHttpClientFactory` does,
  that `internal` types need `InternalsVisibleTo` to be tested, and that the
  interface is the seam the endpoint depends on, not a test-only stand-in.
- **Kept:** Moved the registration above `Build()`, added the using, and chose
  `InternalsVisibleTo` so the Yahoo DTOs and `Map` stay internal.
- **Changed/rejected:** My IDE kept reporting that the `Providers` namespace
  didn't exist. The AI ran `dotnet build`, which succeeded, so the error was a
  stale language server, not the code.

## 10. Time check and splitting the work
- **Prompt:** Asked for a realistic estimate of the time left, since I was running
  past the 4-5 hour estimate.
- **Why:** I needed to decide where my own time mattered most.
- **AI output:** Estimated 7-10 hours left at my current guided pace, and proposed
  a split: the AI writes boilerplate and test plumbing, and I write and defend the
  core logic (mapping, error handling, the endpoint, the React component).
- **Kept:** The split. Most of the time was going into learning C# idioms and
  setting up a project from scratch, not into the core logic, so I kept my own
  effort for the parts that show judgment.

## 11. API and test plumbing
- **Prompt:** "Can you finish writing the boilerplate left, and after I'll begin
  writing the logic and you can help when I make mistakes."
- **AI output:** Split the provider contract, exceptions and Yahoo DTOs into their
  own files. Wired `Program.cs` with a standard resilience handler (retries,
  per-attempt and total timeouts), ProblemDetails, config-driven CORS, `/health`
  and OpenAPI. Added the endpoint route with a TODO handler for me. Wrote the test
  support: `ApiFactory` (the real app in memory with a fake provider),
  `FakeMarketDataProvider`, a fixture loader, a stub `HttpMessageHandler`, and a
  health smoke test.
- **Kept:** All of it.
- **Changed/caught:** `dotnet add package` pulled 11.0.0 packages, which target
  .NET 11. The AI pinned them to the 10.x line. It also flagged that the resilience
  handler throws Polly's `TimeoutRejectedException` on timeout, not
  `TaskCanceledException`, and left that catch as a TODO in my provider code.
  When committing, the AI's first commit swept in files my IDE had already
  staged and didn't build on its own. The AI noticed, undid it with
  `git reset --soft` before it was pushed, and redid it as three clean commits.
- **Deferred:** A response cache, as an optional extra.

## 12. Node upgrade and React scaffold
- **Prompt:** Same request as entry 11, for the UI.
- **AI output:** Found that current Vite, Vitest and MSW need Node 22+ and I had
  Node 18, which is past end of life. Offered either pinning old tool versions or
  upgrading Node.
- **Kept:** I upgraded to Node 26 LTS rather than ship tooling about 1.5 years
  out of date. The AI then scaffolded Vite + React 19 + strict TypeScript with
  Vitest, React Testing Library and MSW, leaving `fetchDailySummaries` and `App`
  as TODO stubs for me.
- **Changed/caught:** MSW 3 renamed `onUnhandledRequest` to `onUnhandledFrame`.
  The smoke test passed with the old name because the option was silently
  ignored. Only `tsc` in the build script caught it.

## 13. Yahoo mapper tests and the zero-volume decision
- **Prompt:** "Please fill in the YahooProviderTests." (I had three empty, named
  tests.)
- **Why:** I wanted the full spec for `Map` before implementing it.
- **AI output:** 15 tests: two against the real TSLA fixture, bar building, nulls
  passed through to the aggregator, and one guard test per malformed-payload case.
  Each guard test builds a valid baseline and breaks one thing with a record `with`
  expression. The fixture values asserted were read from the file, not from notes.
- **Kept:** The tests, committed as a red step. They encode my decision to drop
  zero-volume bars in the mapper (see the README Assumptions and Manual changes).
- **Changed/rejected:** I had also redeclared copies of the provider types inside
  my test class. The AI pointed out those would test the copies, not the real
  code, and I removed them.

## 14. Implementing the Yahoo mapper (guided, not generated)
- **Prompt:** Shared my `Map` code several times while writing it and asked for
  reviews and hints, not a solution.
- **Why:** As with the aggregator, I wanted to write it myself.
- **AI output:** Reviews that pointed at the failing test or compiler error each
  time, with explanations of the C# idioms involved (`is not { } x`, list
  patterns, `async` vs synchronous methods, lifted nullable comparison).
- **Kept:** Guard clauses that throw `UpstreamUnavailableException`, a list
  pattern for `Quote[0]`, a single length check, a `static` time zone helper, and
  `volume[i] == 0` to skip the closing snapshot.
- **Changed/rejected (my mistakes, caught by the compiler, the tests or the
  review):** throwing `NullReferenceException` myself; TypeScript-style
  `for (x of y)` and truthiness checks; an inverted `is { }` guard; chaining `!=`
  for the length check; an `async` helper with nothing to await that also wasn't
  `static`; and `volume[i].Value`, which throws on null bars instead of letting
  them reach the aggregator.

## 15. Tests for GetIntradayAsync
- **Prompt:** "Please go ahead and generate the boilerplate for the
  GetIntradayAsync test."
- **Why:** To save development time, so I could focus on the error-mapping logic.
- **Decision (mine):** A 200 response with no `chart.result` is a 502, not a 404,
  because Yahoo already signals unknown tickers with a real 404 and a
  `chart.error` body.
- **AI output:** 21 HTTP test cases: success, the request URL and query, a 404
  with Yahoo's error body vs an unrecognized 404, other status codes, malformed
  or empty 200 bodies, network errors, both kinds of timeout, and caller
  cancellation. Also a registration test that checks the User-Agent and base
  address against the real DI container, since `Program.cs` sets those, not the
  provider.
- **Kept:** The new tests and the behavior they specify.
- **Changed/caught:** The AI-written stub handler ignored cancellation, so the
  cancellation test failed for the wrong reason. Running the tests caught it, and
  the stub now honors the token like a real handler.

## 16. Implementing GetIntradayAsync (guided, not generated)
- **Prompt:** "I'm working on the deserialize section and need a little help
  understanding how to take from the response and end up with the Yahoo result.
  Would it be to grab the Yahoo charts from the response, then iterate over each
  result in the result array and return those to be mapped?" Then I asked for a
  review of my file against the hints.
- **Why:** I needed help understanding the deserialization process. The 21 HTTP
  tests from entry 15 were the spec, and I wanted to write the code myself.
- **AI output:** Explained the path from the body to `YahooResponse`, `Chart`, and
  `Result[0]`. A single-symbol request returns one result, so there's nothing to
  iterate over. The review recommended collapsing my status checks into one guard
  and removing the fallback `throw`.
- **Kept:** Deserializing the body, then the `?.` chain plus a list pattern for
  `Result[0]`, which covers all four "no result" bodies in one guard. One
  `IsSuccessStatusCode` guard instead of six separate status checks. No fallback
  `throw` at the end, so every path returns or throws for a real reason.
- **Changed/rejected (my mistakes):** I first read `.Chart` from the
  `HttpResponseMessage` instead of the deserialized body, which didn't compile. I
  listed status codes one at a time, which missed others such as 400 and 504;
  those would have surfaced as a 500 instead of a 502.
- **Refactor:** In a separate commit with the tests kept green, I cleaned up the
  error messages: a placeholder `"..."`, a `JsonException` message that described
  the wrong case, and a timeout message naming the library ("(Polly)"). During
  that refactor I accidentally changed the matched error code to `"Not Found."`
  with a period, which would have reported every unknown symbol as a 502. The
  `NotFoundWithYahooErrorBody` test caught it before I committed.

## Manual changes
See the Manual changes section of the [README](README.md).