using MarketSnapshot.Api.Models;
using MarketSnapshot.Api.Providers;

namespace MarketSnapshot.Api.Endpoints;

// Extension method so Program.cs stays a short list of registrations (app.MapDailySummaryEndpoints()).
// New endpoint groups get their own file and Map...Endpoints method as the API grows.
public static class DailySummaryEndpoints
{
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
        // TODO: validate the symbol (strict regex + length limit) before calling the provider.
        //       Invalid -> TypedResults.Problem(..., statusCode: 400)
        // TODO: call provider.GetIntradayAsync, then DailySummaryAggregator.Aggregate(bars, timeZone)
        //       -> TypedResults.Ok(summaries)
        // TODO: SymbolNotFoundException -> 404 ProblemDetails with a message the UI can show
        //       UpstreamUnavailableException -> 502 ProblemDetails
        await Task.CompletedTask;
        throw new NotImplementedException();
    }
}
