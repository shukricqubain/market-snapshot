using System.Net;
using System.Text.Json;
using MarketSnapshot.Api.Models;


namespace MarketSnapshot.Api.Providers;

// Contract, exceptions and Yahoo DTOs live in IMarketDataProvider.cs, ProviderExceptions.cs and YahooDtos.cs.

public sealed class YahooProvider(HttpClient http) : IMarketDataProvider
{
    // Web defaults = camelCase + case-insensitive, so Yahoo's JSON maps to the records in YahooDtos.cs
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
        catch (Polly.Timeout.TimeoutRejectedException ex)
        {
            throw new UpstreamUnavailableException("Yahoo Finance timed out.", ex);
        }
        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                try
                {
                    var notFoundBody = await response.Content
                        .ReadFromJsonAsync<YahooResponse>(JsonOptions, ct);

                    if (notFoundBody?.Chart?.Error is { Code: "Not Found" })
                        throw new SymbolNotFoundException(symbol);
                }
                catch (JsonException)
                {
                    // body wasn't JSON, so it's not the recognized shape; fall through
                }

                throw new UpstreamUnavailableException("Yahoo Finance returned 404 with an unrecognized body.");
            }

            if (!response.IsSuccessStatusCode)
                throw new UpstreamUnavailableException($"Yahoo Finance returned {(int)response.StatusCode} {response.StatusCode}.");

            try
            {
                var yahoo = await response.Content
                    .ReadFromJsonAsync<YahooResponse>(JsonOptions, ct);

                if (yahoo?.Chart?.Result is not [var result, ..])
                    throw new UpstreamUnavailableException("Yahoo Finance provided no results.");

                return Map(result);
            }
            catch (JsonException ex)
            {
                throw new UpstreamUnavailableException("Yahoo Finance returned an unreadable response", ex);
            }
        }
    }

    private static TimeZoneInfo GetTimeZoneByName(string tzName)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(tzName);
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

        if (result.Timestamp is not { } timestampArray)
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
        for (int i = 0; i < n; i++)
        {
            //skip zero bar
            if (volume[i] == 0m) continue;

            bars.Add(new IntradayBar(DateTimeOffset.FromUnixTimeSeconds(timestampArray[i]), low[i], high[i], volume[i]));
        }
        return new IntradayData(bars, timezone);
    }
}