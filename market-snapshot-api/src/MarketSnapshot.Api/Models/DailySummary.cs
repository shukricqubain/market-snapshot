namespace MarketSnapshot.Api.Models;

public sealed record DailySummary(DateOnly Date, decimal LowAverage, decimal HighAverage, long Volume);