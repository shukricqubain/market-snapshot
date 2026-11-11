using MarketSnapshot.Api.Models;

namespace MarketSnapshot.Api.Providers;

// The seam between the endpoint and any market data source. The endpoint depends only on
// this; DI decides which implementation fills it (YahooProvider in production, a fake in tests).
public interface IMarketDataProvider
{
    Task<IntradayData> GetIntradayAsync(string symbol, CancellationToken ct);
}

// The time zone travels with the bars because the aggregator needs it to group by exchange-local day.
public sealed record IntradayData(IReadOnlyList<IntradayBar> Bars, TimeZoneInfo ExchangeTimeZone);
