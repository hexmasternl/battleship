using HexMaster.Battleship.Users.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddGatewayCors();

// Validates the access tokens this service issues; endpoints opt in with RequireAuthorization().
builder.AddBattleshipAuthentication();

// The OpenAPI document and the Scalar API reference, both mapped in Development only.
builder.AddApiReference();
builder.Services.AddProblemDetails();

builder.Services.AddUsersModule();
builder.AddUsersData();

var app = builder.Build();

// Answers CORS requests from the frontend that reach this API through the gateway (Development only).
app.UseGatewayCors();

app.MapDefaultEndpoints();

app.MapApiReference();

// Unwraps CloudEvents envelopes delivered by the Dapr sidecar before routing.
// No HTTPS redirection: the sidecar talks to this API over plain HTTP on the loopback
// interface, and a redirect would break subscription discovery and event delivery.
app.UseCloudEvents();

// Lets the sidecar discover this API's topic subscriptions (GET /dapr/subscribe).
app.MapSubscribeHandler();

app.MapUsersEndpoints();

app.Run();

// Exposed for WebApplicationFactory<Program> in the integration tests.
public partial class Program;
