using HexMaster.Battleship.IntegrationEvents;
using HexMaster.Battleship.IntegrationEvents.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.Hosting;

// Lets a service publish catalog events. Registers the Dapr client, which reads the sidecar
// endpoints (DAPR_HTTP_ENDPOINT and DAPR_GRPC_ENDPOINT) the AppHost injects into each service,
// and the sender. A publishing API calls it right after AddServiceDefaults().
public static class IntegrationEventsExtensions
{
    public static TBuilder AddIntegrationEvents<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddDaprClient();
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddMetrics();
        builder.Services.TryAddSingleton<IntegrationEventsMetrics>();
        builder.Services.TryAddSingleton<IIntegrationEventSender, DaprIntegrationEventSender>();

        return builder;
    }
}
