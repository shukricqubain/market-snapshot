using System.Text.Json;
using MarketSnapshot.Api.Providers;

namespace MarketSnapshot.Api.Tests.TestSupport;

// Loads the JSON fixtures that the test csproj copies next to the test DLL.
internal static class Fixture
{
    // Same defaults the provider uses (camelCase, case-insensitive), so a fixture
    // deserializes here exactly as Yahoo's real body would in production.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string Read(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName));

    // Returns chart.result[0] from a success fixture, ready to pass to YahooProvider.Map.
    public static YahooResult LoadYahooResult(string fileName)
    {
        var response = JsonSerializer.Deserialize<YahooResponse>(Read(fileName), JsonOptions)
            ?? throw new InvalidOperationException($"Fixture '{fileName}' deserialized to null.");

        return response.Chart?.Result?.FirstOrDefault()
            ?? throw new InvalidOperationException($"Fixture '{fileName}' has no chart.result[0].");
    }
}
