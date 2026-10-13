using Microsoft.AspNetCore.Mvc.Testing;

namespace HexMaster.Battleship.DevBot.Tests;

public sealed class ProgramTests
{
    [Fact]
    public async Task Host_AnswersHealth()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
    }
}
