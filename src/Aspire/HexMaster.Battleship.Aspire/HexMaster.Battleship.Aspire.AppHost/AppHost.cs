var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.HexMaster_Battleship_Games_Api>("hexmaster-battleship-games-api");

builder.AddProject<Projects.HexMaster_Battleship_Players_Api>("hexmaster-battleship-players-api");

builder.Build().Run();
