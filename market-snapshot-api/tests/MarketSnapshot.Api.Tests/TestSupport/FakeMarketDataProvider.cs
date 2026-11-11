using MarketSnapshot.Api.Providers;

namespace MarketSnapshot.Api.Tests.TestSupport;

// Stands in for YahooProvider in endpoint tests. Set Result for a success, or Exception
// to simulate SymbolNotFoundException / UpstreamUnavailableException.
internal sealed class FakeMarketDataProvider : IMarketDataProvider
{
    public IntradayData? Result { get; set; }
    public Exception? Exception { get; set; }

    // Lets tests assert the provider was (or, for a 400, was not) called, and with what.
    public List<string> RequestedSymbols { get; } = [];

    public Task<IntradayData> GetIntradayAsync(string symbol, CancellationToken ct)
    {
        RequestedSymbols.Add(symbol);

        if (Exception is not null)
            throw Exception;

        return Task.FromResult(Result
            ?? throw new InvalidOperationException("Set Result or Exception before calling the API."));
    }
}
