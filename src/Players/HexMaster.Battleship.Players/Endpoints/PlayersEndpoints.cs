using Microsoft.AspNetCore.Routing;

namespace HexMaster.Battleship.Players.Endpoints;

// The single entry point the Players API calls to map every endpoint of the module.
public static class PlayersEndpoints
{
    public static IEndpointRouteBuilder MapPlayersEndpoints(this IEndpointRouteBuilder app) =>
        app.MapTokenEndpoints()
            .MapUserEndpoints();
}
