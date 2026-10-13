using HexMaster.Battleship.Events.Connections;

namespace Microsoft.Extensions.DependencyInjection;

public static class EventsCoreRegistration
{
    public const string ConfigurationSection = "Events";

    // Registers the routing core of the Events service. Every component is a singleton:
    // the connections and the replay buffer live in this process's memory.
    public static IServiceCollection AddEventsCore(this IServiceCollection services)
    {
        services.AddSingleton<IConnectionRegistry, ConnectionRegistry>();

        return services;
    }
}
