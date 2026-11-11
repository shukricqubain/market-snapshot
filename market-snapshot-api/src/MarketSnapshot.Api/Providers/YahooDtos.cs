namespace MarketSnapshot.Api.Providers;

// Yahoo chart response DTOs: only the fields we need. Internal because they are a Yahoo
// implementation detail; the test project sees them via InternalsVisibleTo (see the csproj).

internal sealed record YahooResponse(YahooChart? Chart);

internal sealed record YahooChart(YahooResult[]? Result, YahooError? Error);

internal sealed record YahooError(string? Code, string? Description);

internal sealed record YahooResult(YahooMeta? Meta, long[]? Timestamp, YahooIndicators? Indicators);

internal sealed record YahooMeta(string? ExchangeTimezoneName);

internal sealed record YahooIndicators(YahooQuote[]? Quote);

// Elements are nullable because Yahoo sends null for incomplete bars
internal sealed record YahooQuote(decimal?[]? Low, decimal?[]? High, long?[]? Volume);
