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
  hadn't noticed. Decision on handling it: `[confirm]`.
- **Changed/rejected:** The AI had overstated the time-zone risk. Regular-session
  bars land on the same date in UTC and New York, so the real risk is lower than
  claimed. I still group by exchange time zone and test across the DST change
  (10/30 to 11/02).
  Looking at this output, I also rejected the AI's earlier suggestion (entry 1)
  to reach out about what `lowAverage` and `highAverage` mean. Each 15-minute
  bar has its own `low` and `high`, so after grouping by day the natural reading
  is the mean of those values within each day. I stated it as an assumption in
  the README instead of waiting on an answer.

## 3. Cross-checking expected daily values
- **Prompt:** Asked for a prototype aggregation of the fixture.
- **Why:** I wanted independent expected values for golden tests.
- **AI output:** A Python script and per-day results, for example 2026-10-12:
  low 378.7496, high 379.8398, volume 17,558,067.
- **Kept:** The values as a cross-check only. `[confirm: I verified them against
  my own C# output / tests]`.

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
- **Result:** 6 red tests committed before any implementation.

## Manual changes
See the Manual changes section of the [README](README.md).