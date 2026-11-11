using System.Net;
using System.Text.Json;
using MarketSnapshot.Api.Models;


namespace MarketSnapshot.Api.Providers;

// Contract, exceptions and Yahoo DTOs live in IMarketDataProvider.cs, ProviderExceptions.cs and YahooDtos.cs.

public sealed class YahooProvider(HttpClient http) : IMarketDataProvider
{
    // Web defaults = camelCase + case-insensitive, so Yahoo's JSON maps to the records above
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IntradayData> GetIntradayAsync(string symbol, CancellationToken ct)
    {
        var url = $"v8/finance/chart/{Uri.EscapeDataString(symbol)}?interval=15m&range=1mo";

        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync(url, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new UpstreamUnavailableException("Could not reach Yahoo Finance.", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // Cancelled but not by the caller means the HttpClient timed out
            throw new UpstreamUnavailableException("Yahoo Finance timed out.", ex);
        }
        // TODO: Program.cs now adds a resilience handler that owns the timeouts. Its timeout throws
        //       Polly.Timeout.TimeoutRejectedException, not TaskCanceledException, so catch that here
        //       too, or a timeout will surface as a 500 instead of a 502.

        using (response)
        {
            // TODO: read the body as a string once. It's needed for both success and 404.
            // TODO: HttpStatusCode.NotFound -> parse the body and check chart.error.
            //       Recognized "not found" shape (see invalid-symbol.json) -> throw SymbolNotFoundException(symbol)
            //       Anything else -> throw UpstreamUnavailableException
            // TODO: 429 and 5xx -> UpstreamUnavailableException
            // TODO: other non-success -> UpstreamUnavailableException

            // TODO: deserialize the success body into YahooResponse (catch JsonException -> Upstream)
            // TODO: null/empty chart.result -> Upstream (or NotFound? decide and test it)

            // return Map(yahooResult);
            throw new NotImplementedException();
        }
    }
    
    private static TimeZoneInfo GetTimeZoneByName(string tzName)
    {
        try
        {
            return  TimeZoneInfo.FindSystemTimeZoneById(tzName);
        }
        catch (TimeZoneNotFoundException ex)
        {
            throw new UpstreamUnavailableException($"Unknown exchange time zone '{tzName}'.", ex);
        }
    }
    
    // internal static + pure, so the mapper tests need no HTTP at all
    internal static IntradayData Map(YahooResult result)
    {
        if (result.Meta?.ExchangeTimezoneName is not { } tzName)
            throw new UpstreamUnavailableException("Yahoo response has no exchange time zone.");
        
        if(result.Timestamp is not { } timestampArray )
            throw new UpstreamUnavailableException("Yahoo response has no timestamps.");

        if (result.Indicators?.Quote is not [var quote, ..])
            throw new UpstreamUnavailableException("Yahoo response has no quotes.");

        if (quote.Low is not { } low) 
            throw new UpstreamUnavailableException("Yahoo response is missing Low.");

        if (quote.High is not { } high) 
            throw new UpstreamUnavailableException("Yahoo response is missing High.");

        if (quote.Volume is not { } volume) 
            throw new UpstreamUnavailableException("Yahoo response is missing volume.");
        var timezone = GetTimeZoneByName(tzName);
        var n = timestampArray.Length;
        if (low.Length != n || high.Length != n || volume.Length != n)
            throw new UpstreamUnavailableException("Yahoo quote arrays don't match the timestamp count.");

        var bars = new List<IntradayBar>();
        for(int i = 0 ; i < n; i++)
        {
            //skip zero bar
            if(volume[i] == 0m) continue;
            
            bars.Add(new IntradayBar(DateTimeOffset.FromUnixTimeSeconds(timestampArray[i]), low[i], high[i], volume[i]));
        }
        return new IntradayData(bars, timezone);
    }
}