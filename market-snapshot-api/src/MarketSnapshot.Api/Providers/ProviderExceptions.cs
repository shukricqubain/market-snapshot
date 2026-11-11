namespace MarketSnapshot.Api.Providers;

// Unknown or delisted symbol -> endpoint maps to 404
public sealed class SymbolNotFoundException(string symbol)
    : Exception($"Symbol '{symbol}' was not found.");

// Timeout, 429, 5xx, malformed or unrecognized payload -> endpoint maps to 502/503
public sealed class UpstreamUnavailableException(string message, Exception? inner = null)
    : Exception(message, inner);
