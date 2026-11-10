using MarketSnapshot.Api.Models;

namespace MarketSnapshot.Api.Services;

public static class DailySummaryAggregator
{
    public static IReadOnlyList<DailySummary> Aggregate(
        IEnumerable<IntradayBar> bars, TimeZoneInfo exchangeTimeZone)
        => throw new NotImplementedException();
}