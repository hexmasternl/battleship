using CommunityToolkit.Aspire.Hosting.Dapr;
using HexMaster.Battleship.Aspire;
using HexMaster.Battleship.Aspire.AppHost;

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

var pubsub = builder.AddDaprComponent(AspireConstants.PubSubName, "pubsub.rabbitmq")
    .WithMetadata("hostname", messagingHostAndPort)
    .WithMetadata("username", rabbitUsername.Resource)
    .WithMetadata("password", rabbitPassword.Resource)
    .WithMetadata("durable", "true")
    .WithMetadata("deletedWhenUnused", "false")
    .WithMetadata("requeueInFailure", "true");

// Identity storage for the Players service. Locally this is the Azurite emulator, so no Azure
// subscription is needed; the persistent lifetime keeps users across AppHost restarts.
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(emulator => emulator.WithLifetime(ContainerLifetime.Persistent));
var tables = storage.AddTables("tables");

// The key the Players service signs access tokens with and every API validates them with. Generated
// on first run and persisted in the AppHost's user secrets (Parameters:players-signing-key), so
// tokens stay valid across restarts; set that secret to choose a key yourself.
var signingKey = builder.AddParameter(
    "players-signing-key",
    new GenerateParameterDefault { MinLength = 48, Special = false },
    secret: true,
    persist: true);

// Each API runs with its own sidecar that loads the pubsub component. The component is
// referenced from the sidecar (referencing it from the project is obsolete and ignored).
// The APIs reach the broker only through Dapr, so they wait for it but do not reference it.
var gamesApi = builder.AddProject<Projects.HexMaster_Battleship_Games_Api>(GatewayRoutes.GamesApi)
    .WithDaprSidecar(sidecar => sidecar
        .WithOptions(new DaprSidecarOptions { AppId = "games-api" })
        .WithReference(pubsub))
    .WaitFor(messaging);

var playersApi = builder.AddProject<Projects.HexMaster_Battleship_Players_Api>(GatewayRoutes.PlayersApi)
    .WithDaprSidecar(sidecar => sidecar
        .WithOptions(new DaprSidecarOptions { AppId = "players-api" })
        .WithReference(pubsub))
    .WithReference(tables)
    .WithEnvironment("Players__Tokens__SigningKey", signingKey)
    .WaitFor(messaging)
    .WaitFor(tables);

// Pushes integration events to browsers over server-sent events. Routing state lives in the
// process, so the service runs as a single instance.
var eventsApi = builder.AddProject<Projects.HexMaster_Battleship_Events_Api>("hexmaster-battleship-events-api")
    .WithDaprSidecar(sidecar => sidecar
        .WithOptions(new DaprSidecarOptions { AppId = "events-api" })
        .WithReference(pubsub))
    .WaitFor(messaging);

// The gateway is the single backend endpoint for the frontend. It routes each declared module
// prefix to that module's API with the path unchanged; anything not in GatewayRoutes (internal
// endpoints included) gets no route. The table is owned by the yarp-gateway-routes skill.
Dictionary<string, IResourceBuilder<ProjectResource>> moduleApis = new()
{
    [GatewayRoutes.GamesApi] = gamesApi,
    [GatewayRoutes.PlayersApi] = playersApi,
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

builder.Build().Run();
