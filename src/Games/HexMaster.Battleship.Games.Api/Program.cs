using HexMaster.Battleship.Games;
using HexMaster.Battleship.Games.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddGatewayCors();

// Validates the access tokens the Players service issues; endpoints opt in with RequireAuthorization().
builder.AddBattleshipAuthentication();

// Exports the module's own activities and counters next to the service defaults' instrumentation.
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(GamesTelemetry.Name))
    .WithMetrics(metrics => metrics.AddMeter(GamesTelemetry.Name));

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

builder.Services.AddGamesModule();
builder.AddGamesData();

var app = builder.Build();

// Answers CORS requests from the frontend that reach this API through the gateway (Development only).
app.UseGatewayCors();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
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

app.MapGamesEndpoints();

app.Run();

// Exposed for WebApplicationFactory<Program> in the integration tests.
public partial class Program;
