using Dapr.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HexMaster.Battleship.Games.Tests.ServiceDefaults;

public class DaprMessagingExtensionsTests
{
    [Fact]
    public void AddServiceDefaults_RegistersDaprClient()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddServiceDefaults();

        using var host = builder.Build();

        var client = host.Services.GetService<DaprClient>();

        Assert.NotNull(client);
    }

    [Fact]
    public void AddDaprMessaging_RegistersDaprClientAsSingleton()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddDaprMessaging();

        using var host = builder.Build();

        var first = host.Services.GetRequiredService<DaprClient>();
        var second = host.Services.GetRequiredService<DaprClient>();

        Assert.Same(first, second);
    }
}
