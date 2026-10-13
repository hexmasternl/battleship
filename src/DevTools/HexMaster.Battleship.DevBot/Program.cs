using HexMaster.Battleship.DevBot.Client;
using HexMaster.Battleship.DevBot.Endpoints;
using HexMaster.Battleship.DevBot.Events;
using HexMaster.Battleship.DevBot.Identity;
using HexMaster.Battleship.DevBot.Sessions;
using OpenTelemetry.Instrumentation.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// The bot plays through the gateway, exactly like the frontend; the AppHost references the gateway,
// so service discovery resolves this address.
var gateway = new Uri(builder.Configuration["DevBot:GatewayUrl"] ?? "https+http://gateway/");

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<TokenStore>();
builder.Services.AddTransient<BearerTokenHandler>();
builder.Services.AddHttpClient<IUsersClient, UsersClient>(client => client.BaseAddress = gateway);
builder.Services.AddHttpClient<IGamesClient, GamesClient>(client => client.BaseAddress = gateway)
    .AddHttpMessageHandler<BearerTokenHandler>();
builder.Services.AddHostedService<IdentityStartupService>();

var options = builder.Configuration.GetSection(DevBotOptions.ConfigurationSection).Get<DevBotOptions>() ?? new DevBotOptions();
builder.Services.AddSingleton(options);
builder.Services.AddSingleton<BotSettings>();
builder.Services.AddSingleton<GameSessions>();
builder.Services.AddSingleton<IntegrationEventHandler>();
builder.Services.AddHostedService<ReconcileTimerService>();

builder.Services.AddProblemDetails();

// The AppHost polls /admin/state every two seconds; keep those requests out of the traces.
builder.Services.PostConfigure<AspNetCoreTraceInstrumentationOptions>(tracing =>
{
    var filter = tracing.Filter;
    tracing.Filter = context =>
        !context.Request.Path.StartsWithSegments(AdminEndpoints.StatePath) && (filter?.Invoke(context) ?? true);
});

var app = builder.Build();

app.MapDefaultEndpoints();

// Unwraps the CloudEvents envelopes the sidecar delivers. No HTTPS redirection: the sidecar calls
// over plain HTTP on the loopback interface.
app.UseCloudEvents();

// Lets the sidecar discover the topic subscription (GET /dapr/subscribe).
app.MapSubscribeHandler();

app.MapIntegrationEventEndpoints();
app.MapAdminEndpoints();

app.Run();

// Exposed for WebApplicationFactory<Program> in the tests.
public partial class Program;
