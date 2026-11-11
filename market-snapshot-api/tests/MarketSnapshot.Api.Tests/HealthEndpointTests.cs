using System.Net;
using MarketSnapshot.Api.Tests.TestSupport;
using Shouldly;

namespace MarketSnapshot.Api.Tests;

// Smoke test for the test host itself: if this fails, the problem is ApiFactory or Program.cs wiring,
// not the endpoint logic.
public class HealthEndpointTests
{
    [Fact]
    public async Task Health_ReturnsOk()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
