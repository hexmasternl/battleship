using CommunityToolkit.Aspire.Hosting.Dapr;
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

var pubsub = builder.AddDaprComponent("pubsub", "pubsub.rabbitmq")
    .WithMetadata("hostname", messagingHostAndPort)
    .WithMetadata("username", rabbitUsername.Resource)
    .WithMetadata("password", rabbitPassword.Resource)
    .WithMetadata("durable", "true")
    .WithMetadata("deletedWhenUnused", "false")
    .WithMetadata("requeueInFailure", "true");

// Each API runs with its own sidecar that loads the pubsub component. The component is
// referenced from the sidecar (referencing it from the project is obsolete and ignored).
// The APIs reach the broker only through Dapr, so they wait for it but do not reference it.
builder.AddProject<Projects.HexMaster_Battleship_Games_Api>("hexmaster-battleship-games-api")
    .WithDaprSidecar(sidecar => sidecar
        .WithOptions(new DaprSidecarOptions { AppId = "games-api" })
        .WithReference(pubsub))
    .WaitFor(messaging);

builder.AddProject<Projects.HexMaster_Battleship_Players_Api>("hexmaster-battleship-players-api")
    .WithDaprSidecar(sidecar => sidecar
        .WithOptions(new DaprSidecarOptions { AppId = "players-api" })
        .WithReference(pubsub))
    .WaitFor(messaging);

builder.Build().Run();
