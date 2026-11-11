using MarketSnapshot.Api.Models;

namespace MarketSnapshot.Api.Services;

public static class DailySummaryAggregator
{
    public static IReadOnlyList<DailySummary> Aggregate(IEnumerable<IntradayBar> bars, TimeZoneInfo exchangeTimeZone)
    {
        ArgumentNullException.ThrowIfNull(bars);
        ArgumentNullException.ThrowIfNull(exchangeTimeZone);
        ///filter out null bars
        var validBars = bars.Where(b => b.Low.HasValue && b.High.HasValue && b.Volume.HasValue);
        var grouped = validBars.GroupBy(bar => 
        {    
            var local = TimeZoneInfo.ConvertTime(bar.Time, exchangeTimeZone);
            return DateOnly.FromDateTime(local.DateTime);
        }).OrderBy(g => g.Key);//order by ascending day
        var summaries = new List<DailySummary>();
        foreach (var group in grouped)
        {
            var avgLow = group.Average(b => b.Low);
            var avgHigh = group.Average(b => b.High);
            var totalVolume = group.Sum(b => b.Volume);
            if (avgLow is null || avgHigh is null)
            {
                continue; // nothing to report for this day
            }
            summaries.Add(new DailySummary(group.Key, Math.Round(avgLow.Value, 4, MidpointRounding.AwayFromZero), Math.Round(avgHigh.Value, 4, MidpointRounding.AwayFromZero), totalVolume.Value));
        }
        return summaries;
    }
}