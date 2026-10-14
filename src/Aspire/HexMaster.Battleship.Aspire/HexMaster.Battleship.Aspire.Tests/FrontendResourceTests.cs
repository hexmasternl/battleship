using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using HexMaster.Battleship.Aspire.AppHost;

namespace HexMaster.Battleship.Aspire.Tests;

[Collection(AppHostModelCollection.Name)]
public sealed class FrontendResourceTests
{
    private const string EventsApiResourceName = "hexmaster-battleship-events-api";

    [Fact]
    public async Task Frontend_ReceivesTheGatewayAndEventsApiEndpoints()
    {
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync(typeof(GatewayRoutes), [], (_, _) => { });
        var frontend = Assert.Single(builder.Resources, r => r.Name == "frontend");

        var environment = await Environment(frontend);

        var gateway = Assert.IsType<EndpointReference>(environment["BACKEND_GATEWAY_URL"]);
        Assert.Equal("gateway", gateway.Resource.Name);
        var events = Assert.IsType<EndpointReference>(environment["EVENTS_API_URL"]);
        Assert.Equal(EventsApiResourceName, events.Resource.Name);
        Assert.Equal("http", events.EndpointName);
    }

    [Fact]
    public async Task Frontend_WaitsForTheGatewayAndTheEventsApi()
    {
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync(typeof(GatewayRoutes), [], (_, _) => { });
        var frontend = Assert.Single(builder.Resources, r => r.Name == "frontend");

        var waits = frontend.Annotations.OfType<WaitAnnotation>().Select(w => w.Resource.Name).ToArray();

        Assert.Contains("gateway", waits);
        Assert.Contains(EventsApiResourceName, waits);
    }

    // Runs the environment callbacks that set values without a running application; the others are skipped.
    private static async Task<Dictionary<string, object>> Environment(IResource resource)
    {
        var values = new Dictionary<string, object>();
        var context = new EnvironmentCallbackContext(new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run), values);
        foreach (var callback in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            try
            {
                await callback.Callback(context);
            }
            catch (Exception exception) when (exception is InvalidOperationException or NullReferenceException)
            {
            }
        }

        return values;
    }
}
