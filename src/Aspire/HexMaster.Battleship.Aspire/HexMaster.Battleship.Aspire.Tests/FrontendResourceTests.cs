using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using HexMaster.Battleship.Aspire.AppHost;

namespace HexMaster.Battleship.Aspire.Tests;

[Collection(AppHostModelCollection.Name)]
public sealed class FrontendResourceTests
{
    // The event stream goes through the gateway too, so both URLs the frontend receives are the gateway's.
    [Fact]
    public async Task Frontend_ReceivesTheGatewayEndpointForBackendAndEvents()
    {
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync(typeof(GatewayRoutes), [], (_, _) => { });
        var frontend = Assert.Single(builder.Resources, r => r.Name == "frontend");

        var environment = await Environment(frontend);

        var gateway = Assert.IsType<EndpointReference>(environment["BACKEND_GATEWAY_URL"]);
        Assert.Equal("gateway", gateway.Resource.Name);
        var events = Assert.IsType<EndpointReference>(environment["EVENTS_API_URL"]);
        Assert.Equal("gateway", events.Resource.Name);
        Assert.Equal("http", events.EndpointName);
    }

    [Fact]
    public async Task Frontend_WaitsForTheGateway()
    {
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync(typeof(GatewayRoutes), [], (_, _) => { });
        var frontend = Assert.Single(builder.Resources, r => r.Name == "frontend");

        var waits = frontend.Annotations.OfType<WaitAnnotation>().Select(w => w.Resource.Name).ToArray();

        Assert.Contains("gateway", waits);
    }

    [Fact]
    public async Task Gateway_WaitsForEveryModuleApi()
    {
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync(typeof(GatewayRoutes), [], (_, _) => { });
        var gateway = Assert.Single(builder.Resources, r => r.Name == "gateway");

        var waits = gateway.Annotations.OfType<WaitAnnotation>().Select(w => w.Resource.Name).ToArray();

        foreach (var module in GatewayRoutes.Modules)
        {
            Assert.Contains(module.ApiResourceName, waits);
        }
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
