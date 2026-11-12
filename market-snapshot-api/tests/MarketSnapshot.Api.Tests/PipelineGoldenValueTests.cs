using MarketSnapshot.Api.Models;
using MarketSnapshot.Api.Providers;
using MarketSnapshot.Api.Services;
using MarketSnapshot.Api.Tests.TestSupport;
using Shouldly;

namespace MarketSnapshot.Api.Tests;

// The whole data pipeline on real data: recorded Yahoo response -> YahooProvider.Map -> DailySummaryAggregator.
// The expected values were computed independently by a prototype script before the C# code existed
// (PROMPT_LOG entry 3), so this checks the implementation against numbers it didn't produce.
public class PipelineGoldenValueTests
{
    private static IReadOnlyList<DailySummary> SummariesFromFixture()
    {
        var data = YahooProvider.Map(Fixture.LoadYahooResult("tsla-1mo-15m.json"));
        return DailySummaryAggregator.Aggregate(data.Bars, data.ExchangeTimeZone);
    }

    [Fact]
    public void TslaFixture_ProducesOneSummaryPerTradingDay()
    {
        var summaries = SummariesFromFixture();

        summaries.Count.ShouldBe(21);
        summaries[0].Day.ShouldBe(new DateOnly(2026, 10, 12));
        summaries[^1].Day.ShouldBe(new DateOnly(2026, 11, 9));
    }

    [Theory]
    [InlineData("2026-10-12", "378.7496", "379.8398", 17_558_067)]
    [InlineData("2026-10-13", "374.4611", "375.5825", 21_744_117)]
    [InlineData("2026-11-05", "342.6944", "347.1856", 70_120_650)]
    // decimal can't be an attribute argument, so prices are passed as strings and parsed.
    public void TslaFixture_MatchesPrototypeGoldenValues(string day, string lowAverage, string highAverage, long volume)
    {
        var summary = SummariesFromFixture().Single(s => s.Day == DateOnly.Parse(day));

        summary.ShouldBe(new DailySummary(DateOnly.Parse(day), decimal.Parse(lowAverage), decimal.Parse(highAverage), volume));
    }

    // The last day is the one with Yahoo's 16:00 zero-volume closing snapshot. These values were taken
    // from the C# output after the decision to drop that bar, so they guard against regressions rather
    // than coming from the prototype.
    [Fact]
    public void TslaFixture_LastDayExcludesZeroVolumeClosingSnapshot()
    {
        var lastDay = SummariesFromFixture()[^1];

        lastDay.ShouldBe(new DailySummary(new DateOnly(2026, 11, 9), 376.6860m, 379.0226m, 94_947_412));
    }
}
