using HexMaster.Battleship.Events.Api;
using HexMaster.Battleship.Events.Api.Endpoints;
using HexMaster.Battleship.Events.Api.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

builder.Services.AddEventsCore();
builder.Services.AddSingleton<IConnectionIdentityResolver, QueryStringIdentityResolver>();

var pubSub = builder.Configuration.GetSection(PubSubOptions.ConfigurationSection).Get<PubSubOptions>() ?? new PubSubOptions();

var app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Unwraps CloudEvents envelopes delivered by the Dapr sidecar before routing.
// No HTTPS redirection: the sidecar talks to this API over plain HTTP on the loopback
// interface, and a redirect would break subscription discovery and event delivery.
app.UseCloudEvents();

// Lets the sidecar discover this API's topic subscriptions (GET /dapr/subscribe).
app.MapSubscribeHandler();

app.MapEventStreamEndpoints();
app.MapIntegrationEventEndpoints(pubSub);

app.Run();

// Exposed for WebApplicationFactory<Program> in the integration tests.
public partial class Program;
