var builder = DistributedApplication.CreateBuilder(args);

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

// Dapr. The shared pub/sub component is a RabbitMQ component whose connection string is
// resolved from the messaging resource at run time (host, port and credentials are never
// written to a file in the repository). Messages are durable and a handler failure
// requeues the message so the sidecar redelivers it.
builder.AddDapr();

var pubsub = builder.AddDaprComponent("pubsub", "pubsub.rabbitmq")
    .WithMetadata("connectionString", messaging.Resource.ConnectionStringExpression)
    .WithMetadata("durable", "true")
    .WithMetadata("deletedWhenUnused", "false")
    .WithMetadata("requeueInFailure", "true");

builder.AddProject<Projects.HexMaster_Battleship_Games_Api>("hexmaster-battleship-games-api");

builder.AddProject<Projects.HexMaster_Battleship_Players_Api>("hexmaster-battleship-players-api");

builder.Build().Run();
