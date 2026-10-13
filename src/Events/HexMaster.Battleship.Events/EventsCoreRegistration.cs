using HexMaster.Battleship.Events.Connections;
using HexMaster.Battleship.Events.Observability;
using HexMaster.Battleship.Events.Replay;
using HexMaster.Battleship.Events.Routing;
using HexMaster.Battleship.Events.Streaming;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

public static class EventsCoreRegistration
{
    // Registers the routing core of the Events service. Every component is a singleton:
    // the connections and the replay buffer live in this process's memory.
    public static IServiceCollection AddEventsCore(this IServiceCollection services)
    {
        services.AddOptions<ReplayOptions>().BindConfiguration(ReplayOptions.ConfigurationSection);
        services.AddOptions<StreamOptions>().BindConfiguration(StreamOptions.ConfigurationSection);

        services.TryAddSingleton(TimeProvider.System);
        services.AddMetrics();
        services.TryAddSingleton<EventsMetrics>();
        services.AddSingleton<IConnectionRegistry, ConnectionRegistry>();
        services.AddSingleton<IReplayBuffer, ReplayBuffer>();
        services.AddSingleton<IEventRouter, EventRouter>();

        return services;
    }
}
