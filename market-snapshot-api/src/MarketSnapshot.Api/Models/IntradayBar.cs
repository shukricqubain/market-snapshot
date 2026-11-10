namespace MarketSnapshot.Api.Models;

public sealed record IntradayBar(DateTimeOffset Time, decimal? Low, decimal? High, long? Volume);