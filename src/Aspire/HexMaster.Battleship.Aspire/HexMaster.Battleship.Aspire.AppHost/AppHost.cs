using CommunityToolkit.Aspire.Hosting.Dapr;
using HexMaster.Battleship.Aspire;
using HexMaster.Battleship.Aspire.AppHost;
using HexMaster.Battleship.Aspire.AppHost.DevBot;

var builder = DistributedApplication.CreateBuilder(args);

// Fail fast with a clear message instead of restarting sidecars when Dapr is missing.
if (builder.ExecutionContext.IsRunMode)
{
    DaprPrerequisites.EnsureInstalled();
}

// Message broker. The credentials are AppHost parameters: the username defaults to
// "guest", the password is generated on first run and persisted in user secrets.
// No special characters, because the password travels inside an amqp:// URL.
var rabbitUsername = builder.AddParameter("rabbitmq-username", "guest");
var rabbitPassword = builder.AddParameter(
    "rabbitmq-password",
    new GenerateParameterDefault { MinLength = 22, Special = false },
    secret: true,
    persist: true);

var messaging = builder.AddRabbitMQ("messaging", rabbitUsername, rabbitPassword)
    .WithManagementPlugin();

// Dapr. The shared pub/sub component is a RabbitMQ component whose host, port and
// credentials are resolved from the messaging resource and its parameters at run time;
// none of them is written to a file in the repository. The secret password parameter is
// what makes the toolkit emit the auth.secretStore section that the hostname reference
// also needs. Messages are durable and a handler failure requeues the message so the
// sidecar redelivers it.
builder.AddDapr();

var messagingEndpoint = messaging.Resource.PrimaryEndpoint;
var messagingHostAndPort = ReferenceExpression.Create(
    $"{messagingEndpoint.Property(EndpointProperty.Host)}:{messagingEndpoint.Property(EndpointProperty.Port)}");

// Sidecar tracing. Each sidecar exports its spans to the dashboard through the OTEL_* settings
// the toolkit gives it; the generated configuration samples every request and replaces the
// `dapr init` default, which would send them to Zipkin. Written to the temp directory per run.
var daprConfig = builder.ExecutionContext.IsRunMode
    ? DaprTracingConfiguration.Write(
        builder.Configuration,
        Path.Combine(Path.GetTempPath(), "hexmaster-battleship"),
        warning => Console.Error.WriteLine($"warning: {warning}"))
    : null;

var pubsub = builder.AddDaprComponent(AspireConstants.PubSubName, "pubsub.rabbitmq")
    .WithMetadata("hostname", messagingHostAndPort)
    .WithMetadata("username", rabbitUsername.Resource)
    .WithMetadata("password", rabbitPassword.Resource)
    .WithMetadata("durable", "true")
    .WithMetadata("deletedWhenUnused", "false")
    .WithMetadata("requeueInFailure", "true");

// The daily cleanup of games without activity for longer than the retention period. A cron input
// binding on the Games API sidecar posts to the API's internal /maintenance/game-cleanup route,
// which the gateway never routes. Set the game-cleanup-schedule parameter to, for example,
// "@every 1m" to watch it run locally.
var gameCleanupSchedule = builder.AddParameter("game-cleanup-schedule", "@daily");
var gameCleanup = builder.AddDaprComponent("game-cleanup", "bindings.cron")
    .WithMetadata("schedule", gameCleanupSchedule.Resource)
    .WithMetadata("route", "/maintenance/game-cleanup")
    .WithMetadata("direction", "input");

// Table storage for the Users service (users, refresh tokens) and the Games service (games).
// Locally this is the Azurite emulator, so no Azure subscription is needed; the persistent
// lifetime keeps users and games across AppHost restarts.
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(emulator => emulator.WithLifetime(ContainerLifetime.Persistent));
var tables = storage.AddTables("tables");

// The key the Users service signs access tokens with and every API validates them with. Generated
// on first run and persisted in the AppHost's user secrets (Parameters:users-signing-key), so
// tokens stay valid across restarts; set that secret to choose a key yourself.
var signingKey = builder.AddParameter(
    "users-signing-key",
    new GenerateParameterDefault { MinLength = 48, Special = false },
    secret: true,
    persist: true);

// Each API runs with its own sidecar that loads the pubsub component. The component is
// referenced from the sidecar (referencing it from the project is obsolete and ignored).
// The APIs reach the broker only through Dapr, so they wait for it but do not reference it.
var gamesApi = builder.AddProject<Projects.HexMaster_Battleship_Games_Api>(GatewayRoutes.GamesApi)
    .WithDaprSidecar(sidecar => sidecar
        .WithOptions(new DaprSidecarOptions { AppId = "games-api", Config = daprConfig })
        .WithReference(pubsub)
        .WithReference(gameCleanup))
    .WithReference(tables)
    .WithEnvironment("Users__Tokens__SigningKey", signingKey)
    .WithApiReferenceUrl()
    .WaitFor(messaging)
    .WaitFor(tables);

var usersApi = builder.AddProject<Projects.HexMaster_Battleship_Users_Api>(GatewayRoutes.UsersApi)
    .WithDaprSidecar(sidecar => sidecar
        .WithOptions(new DaprSidecarOptions { AppId = "users-api", Config = daprConfig })
        .WithReference(pubsub))
    .WithReference(tables)
    .WithEnvironment("Users__Tokens__SigningKey", signingKey)
    .WithApiReferenceUrl()
    .WaitFor(messaging)
    .WaitFor(tables);

// Pushes integration events to browsers over server-sent events. Routing state lives in the
// process, so the service runs as a single instance.
var eventsApi = builder.AddProject<Projects.HexMaster_Battleship_Events_Api>("hexmaster-battleship-events-api")
    .WithDaprSidecar(sidecar => sidecar
        .WithOptions(new DaprSidecarOptions { AppId = "events-api", Config = daprConfig })
        .WithReference(pubsub))
    .WithApiReferenceUrl()
    .WaitFor(messaging);

// The gateway is the single backend endpoint for the frontend. It routes each declared module
// prefix to that module's API with the path unchanged; anything not in GatewayRoutes (internal
// endpoints included) gets no route. The table is owned by the yarp-gateway-routes skill.
Dictionary<string, IResourceBuilder<ProjectResource>> moduleApis = new()
{
    [GatewayRoutes.GamesApi] = gamesApi,
    [GatewayRoutes.UsersApi] = usersApi,
};

GatewayRoutes.Validate(GatewayRoutes.Modules);

var gateway = builder.AddYarp("gateway")
    .WithConfiguration(yarp =>
    {
        foreach (var module in GatewayRoutes.Modules)
        {
            var api = moduleApis.TryGetValue(module.ApiResourceName, out var resource)
                ? resource
                : throw new InvalidOperationException(
                    $"Gateway module '{module.Module}' points at API resource '{module.ApiResourceName}', which the AppHost does not run.");

            foreach (var prefix in module.Prefixes)
            {
                yarp.AddRoute(GatewayRoutes.RoutePath(prefix), api);
            }
        }
    });

foreach (var api in moduleApis.Values)
{
    gateway.WaitFor(api);
}

// The Angular app. `start:aspire` writes the gateway URL into the runtime config.json the app
// loads at start-up (ng serve cannot read environment variables) and serves on PORT.
var frontend = builder.AddJavaScriptApp("frontend", "../../../Frontend", "start:aspire")
    .WithHttpEndpoint(env: "PORT")
    .WithEnvironment("BACKEND_GATEWAY_URL", gateway.GetEndpoint("http"))
    .WaitFor(gateway);

// The browser calls the gateway from the frontend's origin, so each API allows exactly that
// origin for this run (see GatewayCorsExtensions in ServiceDefaults).
foreach (var api in moduleApis.Values)
{
    api.WithEnvironment("Gateway__AllowedOrigins", frontend.GetEndpoint("http"));
}

// The Events API is not behind the gateway yet: the browser opens its stream directly, so its
// own CORS policy must allow the frontend origin of this run (and plain ng serve on 4200).
eventsApi
    .WithEnvironment("Events__AllowedOrigins__0", frontend.GetEndpoint("http"))
    .WithEnvironment("Events__AllowedOrigins__1", "http://localhost:4200");

// A bot opponent for local runs, so one developer can play a whole game alone. It plays through the
// gateway as its own user and joins a new game nobody else joined within the grace period. Its
// commands (set intelligence, auto-join, join game) are on the devbot resource in the dashboard.
if (builder.ExecutionContext.IsRunMode)
{
    builder.AddBattleshipBot("devbot", gateway, pubsub, daprConfig)
        .WithIntelligence("HuntAndTarget")
        .WithJoinGracePeriod(TimeSpan.FromSeconds(10))
        .WithShotDelay(TimeSpan.FromSeconds(1));
}

builder.Build().Run();
