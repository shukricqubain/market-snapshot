namespace MarketSnapshot.Api.Models;

public sealed record DailySummary(DateOnly Day, decimal LowAverage, decimal HighAverage, long Volume);