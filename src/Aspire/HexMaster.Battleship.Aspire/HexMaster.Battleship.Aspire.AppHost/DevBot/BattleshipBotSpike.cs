using CommunityToolkit.Aspire.Hosting.Dapr;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable ASPIREPROJECTS001

namespace HexMaster.Battleship.Aspire.AppHost.DevBot;

// SPIKE (task 1.1), removed in task 5.1.
internal sealed class BattleshipBotResource(string name) : ProjectResource(name);

internal static class BattleshipBotSpike
{
    public static IResourceBuilder<BattleshipBotResource> AddBattleshipBotSpike(
        this IDistributedApplicationBuilder builder, string name, IResourceBuilder<IDaprComponentResource> pubsub, string? daprConfig)
    {
        var bot = builder.AddResource(new BattleshipBotResource(name))
            .WithAnnotation<IProjectMetadata>(new Projects.HexMaster_Battleship_DevBot())
            .WithProjectDefaults(new ProjectResourceOptions())
            .WithIconName("Bot")
            .ExcludeFromManifest()
            .WithHttpHealthCheck("/health")
            .WithDaprSidecar(sidecar => sidecar
                .WithOptions(new DaprSidecarOptions { AppId = "dev-bot", Config = daprConfig })
                .WithReference(pubsub));

        builder.Eventing.Subscribe<ResourceReadyEvent>(bot.Resource, async (@event, cancellationToken) =>
        {
            var notifications = @event.Services.GetRequiredService<ResourceNotificationService>();
            _ = Task.Run(async () =>
            {
                for (var i = 0; i < 30; i++)
                {
                    await notifications.PublishUpdateAsync(bot.Resource, snapshot => snapshot with
                    {
                        Properties = [.. snapshot.Properties.Where(p => p.Name != "devbot.spike"), new("devbot.spike", $"tick {i}")],
                    });
                    await Task.Delay(TimeSpan.FromSeconds(2));
                }
            }, CancellationToken.None);
            await Task.CompletedTask;
        });

        return bot;
    }
}
