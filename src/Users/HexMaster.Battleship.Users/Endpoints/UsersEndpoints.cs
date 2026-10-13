using Microsoft.AspNetCore.Routing;

namespace HexMaster.Battleship.Users.Endpoints;

// The single entry point the Users API calls to map every endpoint of the module.
public static class UsersEndpoints
{
    public static IEndpointRouteBuilder MapUsersEndpoints(this IEndpointRouteBuilder app) =>
        app.MapTokenEndpoints()
            .MapUserEndpoints();
}
