using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HexMaster.Battleship.Events.Tests.Integration;

// The Events document lists only what browsers call; the Dapr delivery and discovery routes stay out.
public class OpenApiDocumentTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Document_ListsTheEventStream()
    {
        using var paths = await GetPathsAsync();

        Assert.True(paths.RootElement.GetProperty("/events").TryGetProperty("get", out _));
    }

    [Theory]
    [InlineData("/integration-events")]
    [InlineData("/dapr/subscribe")]
    public async Task Document_OmitsInfrastructureEndpoints(string path)
    {
        using var paths = await GetPathsAsync();

        Assert.False(paths.RootElement.TryGetProperty(path, out _));
    }

    private async Task<JsonDocument> GetPathsAsync()
    {
        using var client = factory.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));

        return JsonDocument.Parse(document.RootElement.GetProperty("paths").GetRawText());
    }
}
