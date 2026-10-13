using Dapr.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HexMaster.Battleship.IntegrationEvents.Tests.Registration;

public class IntegrationEventsRegistrationTests
{
    [Fact]
    public void AddIntegrationEvents_RegistersTheDaprSender()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddServiceDefaults();
        builder.AddIntegrationEvents();

        using var host = builder.Build();

        var sender = host.Services.GetRequiredService<IIntegrationEventSender>();

        Assert.IsType<DaprIntegrationEventSender>(sender);
        Assert.Same(sender, host.Services.GetRequiredService<IIntegrationEventSender>());
    }

    [Fact]
    public void AddServiceDefaults_AloneRegistersNoSender()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddServiceDefaults();

        Assert.DoesNotContain(builder.Services, s => s.ServiceType == typeof(IIntegrationEventSender));
    }

    [Fact]
    public void AddServiceDefaultsAndIntegrationEvents_RegisterOneSingletonDaprClient()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddServiceDefaults();
        builder.AddIntegrationEvents();

        var registrations = builder.Services.Where(s => s.ServiceType == typeof(DaprClient)).ToList();

        var registration = Assert.Single(registrations);
        Assert.Equal(ServiceLifetime.Singleton, registration.Lifetime);

        using var host = builder.Build();

        Assert.Same(host.Services.GetRequiredService<DaprClient>(), host.Services.GetRequiredService<DaprClient>());
    }

    [Fact]
    public void AddIntegrationEvents_CalledTwice_KeepsOneSenderAndOneDaprClient()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddIntegrationEvents();
        builder.AddIntegrationEvents();

        Assert.Single(builder.Services, s => s.ServiceType == typeof(IIntegrationEventSender));
        Assert.Single(builder.Services, s => s.ServiceType == typeof(DaprClient));
    }

    [Fact]
    public void AddIntegrationEvents_KeepsATimeProviderRegisteredBefore()
    {
        var builder = Host.CreateApplicationBuilder();
        var custom = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
        builder.Services.AddSingleton<TimeProvider>(custom);

        builder.AddIntegrationEvents();

        using var host = builder.Build();

        Assert.Same(custom, host.Services.GetRequiredService<TimeProvider>());
    }
}
