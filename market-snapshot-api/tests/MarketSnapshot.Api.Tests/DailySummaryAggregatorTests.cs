using MarketSnapshot.Api.Models;
using MarketSnapshot.Api.Services;
using Shouldly;

namespace MarketSnapshot.Api.Tests;

public class DailySummaryAggregatorTests
{
    private static readonly TimeZoneInfo NewYork =
        TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    private static IntradayBar Bar(string iso, decimal? low, decimal? high, long? volume)
        => new(DateTimeOffset.Parse(iso), low, high, volume);

    [Fact]
    public void Aggregate_EmptyInput_ReturnsEmptyList()
    {
        DailySummaryAggregator.Aggregate([], NewYork).ShouldBeEmpty();
    }

    [Fact]
    public void Aggregate_SingleDay_ReturnsMeanOfLowsAndHighsAndSumOfVolume()
    {
        var bars = new[]
        {
            Bar("2026-10-12T09:30:00-04:00", 10m, 12m, 100),
            Bar("2026-10-12T09:45:00-04:00", 20m, 24m, 200),
        };

        var result = DailySummaryAggregator.Aggregate(bars, NewYork);

        result.ShouldHaveSingleItem()
              .ShouldBe(new DailySummary(new DateOnly(2026, 10, 12), 15m, 18m, 300));
    }

    [Fact]
    public void Aggregate_MultipleDays_ReturnsOneRowPerDayInAscendingOrder()
    {
        var bars = new[]
        {
            Bar("2026-10-13T09:30:00-04:00", 5m, 6m, 10),   // deliberately out of order
            Bar("2026-10-12T09:30:00-04:00", 1m, 2m, 20),
        };

        var result = DailySummaryAggregator.Aggregate(bars, NewYork);

        result.Select(r => r.Date).ShouldBe(
            [new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 13)]);
    }

    [Fact]
    public void Aggregate_BarWithNullValue_IsSkippedEntirely()
    {
        var bars = new[]
        {
            Bar("2026-10-12T09:30:00-04:00", 10m, 12m, 100),
            Bar("2026-10-12T09:45:00-04:00", null, 99m, 999),  // skipped, volume too
            Bar("2026-10-12T10:00:00-04:00", 20m, 24m, 200),
        };

        var day = DailySummaryAggregator.Aggregate(bars, NewYork).Single();

        day.LowAverage.ShouldBe(15m);
        day.HighAverage.ShouldBe(18m);
        day.Volume.ShouldBe(300);
    }

    [Fact]
    public void Aggregate_RoundsToFourDecimalsAwayFromZero()
    {
        var bars = new[]
        {
            Bar("2026-10-12T09:30:00-04:00", 1.0000m, 1m, 1),
            Bar("2026-10-12T09:45:00-04:00", 1.0001m, 1m, 1),  // mean 1.00005
        };

        DailySummaryAggregator.Aggregate(bars, NewYork).Single()
            .LowAverage.ShouldBe(1.0001m);
    }

    [Fact]
    public void Aggregate_GroupsByExchangeLocalDate_NotUtc()
    {
        // 00:30 UTC on the 13th is 20:30 EDT on the 12th.
        var bars = new[] { Bar("2026-10-13T00:30:00+00:00", 1m, 2m, 5) };

        DailySummaryAggregator.Aggregate(bars, NewYork).Single()
            .Date.ShouldBe(new DateOnly(2026, 10, 12));
    }
}