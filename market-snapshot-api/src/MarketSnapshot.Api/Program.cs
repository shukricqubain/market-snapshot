using MarketSnapshot.Api.Endpoints;
using MarketSnapshot.Api.Providers;

var builder = WebApplication.CreateBuilder(args);

// ---- Services (must all be registered before Build()) ----

// Typed HttpClient: IHttpClientFactory pools the handlers, and YahooProvider receives a configured client.
builder.Services
    .AddHttpClient<IMarketDataProvider, YahooProvider>(client =>
    {
        client.BaseAddress = new Uri("https://query1.finance.yahoo.com/");
        // Yahoo rejects requests without a browser-like User-Agent
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    })
    // Retries transient failures (5xx, 408, 429, network errors) with backoff, and owns the timeouts.
    // This replaces HttpClient.Timeout: a timeout here throws Polly's TimeoutRejectedException.
    .AddStandardResilienceHandler(options =>
    {
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);       // per try
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15); // all tries together
        options.Retry.MaxRetryAttempts = 2;
    });

// Errors come back as RFC 7807 ProblemDetails JSON instead of empty or HTML bodies.
builder.Services.AddProblemDetails();

// The React dev server runs on another origin, so the browser needs CORS permission.
// Allowed origins come from config (appsettings.Development.json) rather than being hard-coded.
const string UiCorsPolicy = "ui";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddPolicy(UiCorsPolicy, policy => policy.WithOrigins(allowedOrigins).AllowAnyHeader().WithMethods("GET")));

builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();

var app = builder.Build();

// ---- Middleware and endpoints ----

app.UseExceptionHandler(); // unhandled exception -> 500 ProblemDetails (no stack trace leaked)
app.UseStatusCodePages();  // bare status codes (for example an unmatched route) -> ProblemDetails
app.UseCors(UiCorsPolicy);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // GET /openapi/v1.json
}

app.MapHealthChecks("/health");
app.MapDailySummaryEndpoints();

app.Run();
