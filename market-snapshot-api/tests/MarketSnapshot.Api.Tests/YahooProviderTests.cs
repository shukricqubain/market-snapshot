using MarketSnapshot.Api.Models;
using MarketSnapshot.Api.Providers;
using MarketSnapshot.Api.Tests.TestSupport;
using Shouldly;

namespace MarketSnapshot.Api.Tests;

public class YahooProviderTests
{
    // ---- Helpers: a small valid result that each guard test breaks in exactly one way ----

    private static long Unix(string iso) => DateTimeOffset.Parse(iso).ToUnixTimeSeconds();

    // Two regular-session bars on 2026-10-12 (09:30 and 09:45 ET).
    private static YahooQuote ValidQuote() => new(Low: [10m, 11m], High: [12m, 13m], Volume: [100L, 200L]);

    private static YahooResult ValidResult() => new(
        Meta: new YahooMeta("America/New_York"),
        Timestamp: [Unix("2026-10-12T09:30:00-04:00"), Unix("2026-10-12T09:45:00-04:00")],
        Indicators: new YahooIndicators([ValidQuote()]));

    // Records support `with`: a copy with only the named properties changed.
    private static YahooResult WithQuote(YahooQuote quote) =>
        ValidResult() with { Indicators = new YahooIndicators([quote]) };

    // ---- Real fixture ----

    [Fact]
    public void Map_TslaFixture_ReturnsBarsInExchangeTimeZone()
    {
        var data = YahooProvider.Map(Fixture.LoadYahooResult("tsla-1mo-15m.json"));

        data.ExchangeTimeZone.Id.ShouldBe("America/New_York");
        data.Bars[0].ShouldBe(new IntradayBar(
            DateTimeOffset.Parse("2026-10-12T09:30:00-04:00"), 378.5400085449219m, 382.6050109863281m, 2765836));
    }

    // The fixture's 547th bar is a 16:00 ET closing-price snapshot with volume 0, not a traded bar.
    [Fact]
    public void Map_TslaFixture_DropsZeroVolumeClosingSnapshot()
    {
        var data = YahooProvider.Map(Fixture.LoadYahooResult("tsla-1mo-15m.json"));

        data.Bars.Count.ShouldBe(546);
        data.Bars[^1].Time.ShouldBe(DateTimeOffset.Parse("2026-11-09T15:45:00-05:00"));
    }

    // ---- Bar building ----

    [Fact]
    public void Map_ValidResult_ZipsParallelArraysIntoBars()
    {
        var data = YahooProvider.Map(ValidResult());

        data.Bars.ShouldBe([
            new IntradayBar(DateTimeOffset.Parse("2026-10-12T09:30:00-04:00"), 10m, 12m, 100),
            new IntradayBar(DateTimeOffset.Parse("2026-10-12T09:45:00-04:00"), 11m, 13m, 200),
        ]);
    }

    // Nulls pass through unchanged: dropping incomplete bars is the aggregator's rule, not the mapper's.
    [Fact]
    public void Map_NullValues_AreKeptForTheAggregator()
    {
        var data = YahooProvider.Map(WithQuote(new YahooQuote(Low: [null, 11m], High: [12m, null], Volume: [100L, null])));

        data.Bars.Count.ShouldBe(2);
        data.Bars[0].Low.ShouldBeNull();
        data.Bars[1].High.ShouldBeNull();
        data.Bars[1].Volume.ShouldBeNull();
    }

    [Fact]
    public void Map_ZeroVolumeBar_IsDropped()
    {
        var data = YahooProvider.Map(WithQuote(ValidQuote() with { Volume = [0L, 200L] }));

        data.Bars.ShouldHaveSingleItem().Volume.ShouldBe(200);
    }

    // ---- Guards: a malformed payload is an upstream problem (502), never a crash (500) ----

    [Fact]
    public void Map_MissingTimezone_ThrowsUpstreamUnavailable()
    {
        Should.Throw<UpstreamUnavailableException>(() => YahooProvider.Map(ValidResult() with { Meta = new YahooMeta(null) }));
    }

    [Fact]
    public void Map_MissingMeta_ThrowsUpstreamUnavailable()
    {
        Should.Throw<UpstreamUnavailableException>(() => YahooProvider.Map(ValidResult() with { Meta = null }));
    }

    [Fact]
    public void Map_UnknownTimezone_ThrowsUpstreamUnavailable()
    {
        Should.Throw<UpstreamUnavailableException>(() => YahooProvider.Map(ValidResult() with { Meta = new YahooMeta("Mars/Olympus_Mons") }));
    }

    [Fact]
    public void Map_MissingTimestamps_ThrowsUpstreamUnavailable()
    {
        Should.Throw<UpstreamUnavailableException>(() => YahooProvider.Map(ValidResult() with { Timestamp = null }));
    }

    [Fact]
    public void Map_MissingIndicators_ThrowsUpstreamUnavailable()
    {
        Should.Throw<UpstreamUnavailableException>(() => YahooProvider.Map(ValidResult() with { Indicators = null }));
    }

    [Fact]
    public void Map_EmptyQuoteArray_ThrowsUpstreamUnavailable()
    {
        Should.Throw<UpstreamUnavailableException>(() => YahooProvider.Map(ValidResult() with { Indicators = new YahooIndicators([]) }));
    }

    [Fact]
    public void Map_MissingLow_ThrowsUpstreamUnavailable()
    {
        Should.Throw<UpstreamUnavailableException>(() => YahooProvider.Map(WithQuote(ValidQuote() with { Low = null })));
    }

    [Fact]
    public void Map_MissingHigh_ThrowsUpstreamUnavailable()
    {
        Should.Throw<UpstreamUnavailableException>(() => YahooProvider.Map(WithQuote(ValidQuote() with { High = null })));
    }

    [Fact]
    public void Map_MissingVolume_ThrowsUpstreamUnavailable()
    {
        Should.Throw<UpstreamUnavailableException>(() => YahooProvider.Map(WithQuote(ValidQuote() with { Volume = null })));
    }

    // Parallel arrays must line up, or bars would pair a timestamp with another bar's prices.
    [Fact]
    public void Map_MismatchedArrayLengths_ThrowsUpstreamUnavailable()
    {
        Should.Throw<UpstreamUnavailableException>(() => YahooProvider.Map(WithQuote(ValidQuote() with { Volume = [100L] })));
    }
}
