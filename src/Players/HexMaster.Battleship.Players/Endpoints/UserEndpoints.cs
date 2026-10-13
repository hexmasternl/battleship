using System.Security.Claims;
using HexMaster.Battleship.Core.Cqrs;
using HexMaster.Battleship.Players.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Players.Features.GetCurrentUser;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace HexMaster.Battleship.Players.Endpoints;

// /users: the identity of the caller. Every endpoint here requires a bearer access token.
public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/users").WithTags("Users").RequireAuthorization();

        group.MapGet("/me", GetCurrentUser)
            .WithName("GetCurrentUser")
            .WithSummary("Returns the identifier and display name of the user the access token belongs to.")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<Results<Ok<CurrentUserResponse>, ProblemHttpResult>> GetCurrentUser(
        ClaimsPrincipal principal,
        IQueryHandler<GetCurrentUserQuery, CurrentUserResponse?> handler,
        CancellationToken cancellationToken)
    {
        if (Guid.TryParse(principal.FindFirstValue("sub"), out var userId)
            && await handler.Handle(new GetCurrentUserQuery(userId), cancellationToken) is { } user)
        {
            return TypedResults.Ok(user);
        }

        return TypedResults.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "The access token does not identify a known user.");
    }
}
