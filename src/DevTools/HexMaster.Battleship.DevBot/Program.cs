using HexMaster.Battleship.DevBot.Client;
using HexMaster.Battleship.DevBot.Identity;
using HexMaster.Battleship.DevBot.Sessions;

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

var app = builder.Build();

app.MapDefaultEndpoints();

app.Run();

// Exposed for WebApplicationFactory<Program> in the tests.
public partial class Program;
