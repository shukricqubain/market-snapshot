using System.Net;
using System.Web;
using MarketSnapshot.Api.Providers;
using MarketSnapshot.Api.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Polly.Timeout;
using Shouldly;

namespace MarketSnapshot.Api.Tests;

// Tests YahooProvider.GetIntradayAsync end to end over a fake network: the real provider,
// the real HttpClient, and a stub handler returning fixture bodies or status codes.
// Map's own rules are covered in YahooProviderTests; these tests cover HTTP status and error mapping.
public class YahooProviderHttpTests
{
    private static YahooProvider ProviderFor(StubHttpMessageHandler handler) => new(handler.CreateClient());

    private static Task<IntradayData> GetTsla(StubHttpMessageHandler handler) =>
        ProviderFor(handler).GetIntradayAsync("TSLA", CancellationToken.None);

    // ---- Success ----

    [Fact]
    public async Task GetIntraday_SuccessResponse_ReturnsMappedBars()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, Fixture.Read("tsla-1mo-15m.json"));

        var data = await GetTsla(handler);

        data.Bars.Count.ShouldBe(546); // 547 bars minus the zero-volume closing snapshot
        data.ExchangeTimeZone.Id.ShouldBe("America/New_York");
    }

    // interval = bar size, range = how far back. Getting either wrong returns valid but wrong data.
    [Fact]
    public async Task GetIntraday_RequestsFifteenMinuteBarsForOneMonth()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, Fixture.Read("tsla-1mo-15m.json"));

        await GetTsla(handler);

        var uri = handler.LastRequest.ShouldNotBeNull().RequestUri.ShouldNotBeNull();
        uri.AbsolutePath.ShouldBe("/v8/finance/chart/TSLA");
        var query = HttpUtility.ParseQueryString(uri.Query);
        query["interval"].ShouldBe("15m");
        query["range"].ShouldBe("1mo");
    }

    // ---- Unknown symbol vs a broken upstream ----

    // Yahoo answers an unknown ticker with a real 404 and a chart.error body (see invalid-symbol.json).
    [Fact]
    public async Task GetIntraday_NotFoundWithYahooErrorBody_ThrowsSymbolNotFound()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.NotFound, Fixture.Read("invalid-symbol.json"));

        await Should.ThrowAsync<SymbolNotFoundException>(() => GetTsla(handler));
    }

    // A 404 without Yahoo's error shape means the endpoint itself moved or changed:
    // reporting that as "bad ticker" would blame the user for an upstream problem.
    [Theory]
    [InlineData("<html><body>Not Found</body></html>")]
    [InlineData("""{"unexpected":"shape"}""")]
    [InlineData("")]
    public async Task GetIntraday_NotFoundWithUnrecognizedBody_ThrowsUpstreamUnavailable(string body)
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.NotFound, body);

        await Should.ThrowAsync<UpstreamUnavailableException>(() => GetTsla(handler));
    }

    // ---- Other HTTP failures ----

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task GetIntraday_NonSuccessStatus_ThrowsUpstreamUnavailable(HttpStatusCode status)
    {
        var handler = StubHttpMessageHandler.Returning(status, "");

        await Should.ThrowAsync<UpstreamUnavailableException>(() => GetTsla(handler));
    }

    // ---- 200 responses that can't be used ----

    [Fact]
    public async Task GetIntraday_MalformedJson_ThrowsUpstreamUnavailable()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, "{ this is not json");

        await Should.ThrowAsync<UpstreamUnavailableException>(() => GetTsla(handler));
    }

    // Decision: unknown symbols arrive as a real 404, so a 200 with no result is an upstream fault (502).
    [Theory]
    [InlineData("""{"chart":{"result":null,"error":null}}""")]
    [InlineData("""{"chart":{"result":[],"error":null}}""")]
    [InlineData("""{"chart":null}""")]
    [InlineData("null")]
    public async Task GetIntraday_SuccessWithNoResult_ThrowsUpstreamUnavailable(string body)
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, body);

        await Should.ThrowAsync<UpstreamUnavailableException>(() => GetTsla(handler));
    }

    // ---- Network failures, timeouts, cancellation ----

    [Fact]
    public async Task GetIntraday_NetworkError_ThrowsUpstreamUnavailable()
    {
        var handler = StubHttpMessageHandler.Throwing(new HttpRequestException("Connection refused"));

        await Should.ThrowAsync<UpstreamUnavailableException>(() => GetTsla(handler));
    }

    // HttpClient's own timeout surfaces as TaskCanceledException even though the caller didn't cancel.
    [Fact]
    public async Task GetIntraday_HttpClientTimeout_ThrowsUpstreamUnavailable()
    {
        var handler = StubHttpMessageHandler.Throwing(new TaskCanceledException("The request timed out."));

        await Should.ThrowAsync<UpstreamUnavailableException>(() => GetTsla(handler));
    }

    // In production the resilience handler (Program.cs) owns the timeouts and throws Polly's exception instead.
    [Fact]
    public async Task GetIntraday_ResilienceTimeout_ThrowsUpstreamUnavailable()
    {
        var handler = StubHttpMessageHandler.Throwing(new TimeoutRejectedException());

        await Should.ThrowAsync<UpstreamUnavailableException>(() => GetTsla(handler));
    }

    // If the caller cancels (for example the browser disconnects), that's not Yahoo's fault:
    // the cancellation should propagate as-is, not be reported as a 502.
    [Fact]
    public async Task GetIntraday_CallerCancels_PropagatesCancellation()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, Fixture.Read("tsla-1mo-15m.json"));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<TaskCanceledException>(() => ProviderFor(handler).GetIntradayAsync("TSLA", cts.Token));
    }
}

// The User-Agent and base address are set by the DI registration in Program.cs, not by YahooProvider,
// so they're checked against the real container rather than the stub client above.
public class YahooClientRegistrationTests
{
    [Fact]
    public void YahooHttpClient_HasBaseAddressAndBrowserUserAgent()
    {
        using var factory = new ApiFactory();

        // Typed clients are named after the service type: AddHttpClient<IMarketDataProvider, YahooProvider>.
        var client = factory.Services.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(IMarketDataProvider));

        client.BaseAddress.ShouldBe(new Uri("https://query1.finance.yahoo.com/"));
        client.DefaultRequestHeaders.UserAgent.ToString().ShouldStartWith("Mozilla/5.0");
    }
}
