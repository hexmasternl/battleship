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

builder.AddProject<Projects.HexMaster_Battleship_Games_Api>("hexmaster-battleship-games-api");

builder.AddProject<Projects.HexMaster_Battleship_Players_Api>("hexmaster-battleship-players-api");

builder.Build().Run();
