using HexMaster.Battleship.DevBot.Client;
using HexMaster.Battleship.DevBot.Identity;

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

var app = builder.Build();

app.MapDefaultEndpoints();

app.Run();

// Exposed for WebApplicationFactory<Program> in the tests.
public partial class Program;
