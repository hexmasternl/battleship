using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting;

// Registers the Dapr client every service uses to publish to and subscribe from the shared
// pub/sub component. The client reads the sidecar endpoints (DAPR_HTTP_ENDPOINT and
// DAPR_GRPC_ENDPOINT) that the AppHost injects into each service's environment.
public static class DaprMessagingExtensions
{
    public static TBuilder AddDaprMessaging<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddDaprClient();

        return builder;
    }
}
