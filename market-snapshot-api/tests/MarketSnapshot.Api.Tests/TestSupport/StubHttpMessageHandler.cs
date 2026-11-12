using System.Net;
using System.Text;

namespace MarketSnapshot.Api.Tests.TestSupport;

// Fake network layer for YahooProvider tests: HttpClient sends every request through
// its handler, so swapping the handler means no real HTTP call is ever made.
// It also records the last request so tests can assert on the URL and headers.
internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
    : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    // Responds with a fixed status code and body (for example, a fixture's JSON).
    public static StubHttpMessageHandler Returning(HttpStatusCode status, string body = "") =>
        new(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });

    // Throws instead of responding, to simulate network failures or timeouts.
    public static StubHttpMessageHandler Throwing(Exception exception) =>
        new(_ => throw exception);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        // Real handlers stop when the caller cancels; HttpClient doesn't check the token for us.
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(respond(request));
    }

    // Builds an HttpClient configured like the production registration in Program.cs,
    // so relative URLs and the User-Agent behave the same way in tests.
    public HttpClient CreateClient()
    {
        var client = new HttpClient(this) { BaseAddress = new Uri("https://query1.finance.yahoo.com/") };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        return client;
    }
}
