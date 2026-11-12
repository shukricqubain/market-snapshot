using System.Text.RegularExpressions;
using MarketSnapshot.Api.Models;
using MarketSnapshot.Api.Providers;
namespace MarketSnapshot.Api.Endpoints;
using MarketSnapshot.Api.Services;

// Extension method so Program.cs stays a short list of registrations (app.MapDailySummaryEndpoints()).
// New endpoint groups get their own file and Map...Endpoints method as the API grows.
public static partial class DailySummaryEndpoints
{
    [GeneratedRegex(@"^[A-Za-z0-9.^-]{1,10}\z")]
    private static partial Regex SymbolRegex();
    public static IEndpointRouteBuilder MapDailySummaryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/stocks/{symbol}/daily-summaries", GetDailySummariesAsync)
            .WithName("GetDailySummaries")
            .WithSummary("Daily low/high averages and total volume for the last month of 15-minute bars.")
            .Produces<IReadOnlyList<DailySummary>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        return app;
    }

    // Minimal APIs bind parameters by name and type: {symbol} from the route, the provider from DI,
    // and the CancellationToken from the request (cancelled when the client disconnects).
    internal static async Task<IResult> GetDailySummariesAsync(
        string symbol, IMarketDataProvider provider, CancellationToken ct)
    {
        bool validSymbol = SymbolRegex().IsMatch(symbol);
        if(!validSymbol)
            return TypedResults.Problem("Invalid Symbol was provided", statusCode: 400);
        
        try
        {
            var intraDay = await provider.GetIntradayAsync(symbol, ct); 
            var summaries =  DailySummaryAggregator.Aggregate(intraDay.Bars, intraDay.ExchangeTimeZone);
            return TypedResults.Ok(summaries);
        }
        catch (SymbolNotFoundException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: 404);
        }
        catch (UpstreamUnavailableException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: 502);
        }
    }
}
