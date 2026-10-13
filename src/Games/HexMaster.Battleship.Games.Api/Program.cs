using System.Text.Json;
using System.Text.Json.Serialization;
using HexMaster.Battleship.Games.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Games publishes integration events (game created, joined, ...) through its Dapr sidecar.
builder.AddIntegrationEvents();

builder.AddGatewayCors();

// Validates the access tokens the Players service issues; endpoints opt in with RequireAuthorization().
builder.AddBattleshipAuthentication();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

// Enums travel as camelCase strings (ship kind "carrier", orientation "horizontal") and are read
// case-insensitively. GameView keeps its state by name through its own converter.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

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
