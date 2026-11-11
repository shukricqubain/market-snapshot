using MarketSnapshot.Api.Providers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MarketSnapshot.Api.Tests.TestSupport;

// Boots the real app (Program.cs, middleware, routing, JSON settings) in memory, with one change:
// IMarketDataProvider is the fake, so endpoint tests never reach Yahoo.
// Usage: using var factory = new ApiFactory(); factory.Provider.Result = ...; var client = factory.CreateClient();
internal sealed class ApiFactory : WebApplicationFactory<Program>
{
    public FakeMarketDataProvider Provider { get; } = new();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Drop the typed-HttpClient registration of YahooProvider and use the fake instead.
            services.RemoveAll<IMarketDataProvider>();
            services.AddSingleton<IMarketDataProvider>(Provider);
        });
    }
}
