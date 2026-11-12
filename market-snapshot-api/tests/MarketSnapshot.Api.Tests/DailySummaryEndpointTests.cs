using System.Net;
using System.Text.Json;
using MarketSnapshot.Api.Models;
using MarketSnapshot.Api.Providers;
using MarketSnapshot.Api.Tests.TestSupport;
using Shouldly;

namespace MarketSnapshot.Api.Tests;

// Endpoint tests run the real app in memory (routing, JSON settings, ProblemDetails, CORS)
// with FakeMarketDataProvider in place of Yahoo. They assert on the HTTP contract the UI relies on.
public class DailySummaryEndpointTests
{
    private static readonly TimeZoneInfo NewYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    private static IntradayBar Bar(string iso, decimal? low, decimal? high, long? volume) =>
        new(DateTimeOffset.Parse(iso), low, high, volume);

    // Two trading days, deliberately out of order, with easy-to-check averages.
    private static IntradayData TwoDays() => new(
    [
        Bar("2026-10-13T09:30:00-04:00", 20m, 22m, 1_000),
        Bar("2026-10-12T09:30:00-04:00", 10m, 12m, 100),
        Bar("2026-10-12T09:45:00-04:00", 11m, 13m, 200),
    ], NewYork);

    private static string Url(string symbol) => $"/api/stocks/{symbol}/daily-summaries";

    private static async Task<JsonElement> ReadJson(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    // ---- 200: the exact JSON shape from the brief ----

    [Fact]
    public async Task Get_KnownSymbol_ReturnsDailySummariesInBriefShape()
    {
        using var factory = new ApiFactory();
        factory.Provider.Result = TwoDays();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Url("TSLA"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");

        // Assert on raw JSON, not deserialized objects: the property names, the
        // "yyyy-MM-dd" day format and the number formats are the contract.
        var days = (await ReadJson(response)).EnumerateArray().ToList();
        days.Count.ShouldBe(2);

        var first = days[0];
        first.GetProperty("day").GetString().ShouldBe("2026-10-12"); // oldest first, DateOnly as yyyy-MM-dd
        first.GetProperty("lowAverage").GetDecimal().ShouldBe(10.5m);
        first.GetProperty("highAverage").GetDecimal().ShouldBe(12.5m);
        first.GetProperty("volume").GetInt64().ShouldBe(300);
        first.EnumerateObject().Select(p => p.Name)
            .ShouldBe(["day", "lowAverage", "highAverage", "volume"], ignoreOrder: true);

        days[1].GetProperty("day").GetString().ShouldBe("2026-10-13");
    }

    [Fact]
    public async Task Get_KnownSymbol_PassesSymbolToProvider()
    {
        using var factory = new ApiFactory();
        factory.Provider.Result = TwoDays();
        using var client = factory.CreateClient();

        await client.GetAsync(Url("TSLA"));

        factory.Provider.RequestedSymbols.ShouldBe(["TSLA"]);
    }

    // A symbol with no usable bars is still a valid answer: an empty list, not an error.
    [Fact]
    public async Task Get_NoBars_ReturnsEmptyArray()
    {
        using var factory = new ApiFactory();
        factory.Provider.Result = new IntradayData([], NewYork);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Url("TSLA"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadJson(response)).GetArrayLength().ShouldBe(0);
    }

    // ---- 400: symbol validation happens before any upstream call ----

    // Real tickers include share classes (BRK.B), indices (^GSPC), pairs (BTC-USD) and lowercase input.
    [Theory]
    [InlineData("TSLA", "TSLA")]
    [InlineData("tsla", "tsla")]
    [InlineData("BRK.B", "BRK.B")]
    [InlineData("%5EGSPC", "^GSPC")]
    [InlineData("BTC-USD", "BTC-USD")]
    [InlineData("A", "A")]
    [InlineData("ABCDEFGHIJ", "ABCDEFGHIJ")] // 10 characters: the limit
    public async Task Get_ValidSymbol_IsAccepted(string urlSymbol, string expectedSymbol)
    {
        using var factory = new ApiFactory();
        factory.Provider.Result = TwoDays();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Url(urlSymbol));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        factory.Provider.RequestedSymbols.ShouldBe([expectedSymbol]);
    }

    [Theory]
    [InlineData("ABCDEFGHIJK")]  // 11 characters: over the limit
    [InlineData("TS%20LA")]      // space
    [InlineData("TSLA%21")]      // !
    [InlineData("%3Cscript%3E")] // <script>
    [InlineData("TSLA%3Fx%3D1")] // ?x=1, an attempt to inject query parameters into the Yahoo URL
    [InlineData("%20")]          // whitespace only
    public async Task Get_InvalidSymbol_Returns400ProblemWithoutCallingProvider(string urlSymbol)
    {
        using var factory = new ApiFactory();
        factory.Provider.Result = TwoDays();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Url(urlSymbol));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        factory.Provider.RequestedSymbols.ShouldBeEmpty();
    }

    // ---- 404 and 502: provider exceptions become ProblemDetails the UI can show ----

    [Fact]
    public async Task Get_UnknownSymbol_Returns404ProblemNamingTheSymbol()
    {
        using var factory = new ApiFactory();
        factory.Provider.Exception = new SymbolNotFoundException("ZZZZ");
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Url("ZZZZ"));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await ReadJson(response);
        problem.GetProperty("status").GetInt32().ShouldBe(404);
        problem.GetProperty("detail").GetString().ShouldNotBeNull().ShouldContain("ZZZZ");
    }

    [Fact]
    public async Task Get_UpstreamUnavailable_Returns502Problem()
    {
        using var factory = new ApiFactory();
        factory.Provider.Exception = new UpstreamUnavailableException("Yahoo Finance timed out.");
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Url("TSLA"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await ReadJson(response);
        problem.GetProperty("status").GetInt32().ShouldBe(502);
        problem.TryGetProperty("detail", out _).ShouldBeTrue(); // the UI needs a message to show
    }

    // ---- CORS: the browser only lets the React dev server read responses it's allowed to ----

    [Fact]
    public async Task Get_FromUiOrigin_AllowsCors()
    {
        using var factory = new ApiFactory();
        factory.Provider.Result = TwoDays();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, Url("TSLA"));
        request.Headers.Add("Origin", "http://localhost:5173");

        var response = await client.SendAsync(request);

        response.Headers.GetValues("Access-Control-Allow-Origin").ShouldBe(["http://localhost:5173"]);
    }

    [Fact]
    public async Task Get_FromOtherOrigin_DoesNotAllowCors()
    {
        using var factory = new ApiFactory();
        factory.Provider.Result = TwoDays();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, Url("TSLA"));
        request.Headers.Add("Origin", "https://evil.example");

        var response = await client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }
}
