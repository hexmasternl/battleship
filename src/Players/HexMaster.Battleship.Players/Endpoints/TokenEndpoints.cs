using HexMaster.Battleship.Core.Cqrs;
using HexMaster.Battleship.Core.DomainModels;
using HexMaster.Battleship.Players.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Players.Features.CreateTokens;
using HexMaster.Battleship.Players.Features.RefreshTokens;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace HexMaster.Battleship.Players.Endpoints;

// /users/tokens: anonymous token issuance and refresh with rotation.
public static class TokenEndpoints
{
    public static IEndpointRouteBuilder MapTokenEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/users/tokens").WithTags("Tokens").AllowAnonymous();

        group.MapPost("", CreateTokens)
            .WithName("CreateTokens")
            .WithSummary("Creates an anonymous user and issues its first access and refresh token.")
            .AddEndpointFilter<DataAnnotationsValidationFilter<CreateTokensRequest>>();

        group.MapPost("/refresh", RefreshTokens)
            .WithName("RefreshTokens")
            .WithSummary("Exchanges a refresh token for a new token pair; the presented token is invalidated.")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .AddEndpointFilter<DataAnnotationsValidationFilter<RefreshTokensRequest>>();

        return app;
    }

    private static async Task<Results<Created<TokenPairResponse>, ValidationProblem>> CreateTokens(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CreateTokensRequest? request,
        ICommandHandler<CreateTokensCommand, TokenPairResponse> handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var tokens = await handler.Handle(new CreateTokensCommand(request?.DisplayName), cancellationToken);
            return TypedResults.Created((string?)null, tokens);
        }
        catch (DomainException exception)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["displayName"] = [exception.Message],
            });
        }
    }

    private static async Task<Results<Ok<TokenPairResponse>, ValidationProblem, ProblemHttpResult>> RefreshTokens(
        RefreshTokensRequest request,
        ICommandHandler<RefreshTokensCommand, TokenPairResponse> handler,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            return TypedResults.Ok(await handler.Handle(new RefreshTokensCommand(request.RefreshToken), cancellationToken));
        }
        catch (TokenRefusedException exception)
        {
            // The reason is logged, never returned: a client learns only that the token is no good.
            loggerFactory.CreateLogger(typeof(TokenEndpoints)).LogInformation("Refused a refresh token: {Reason}", exception.Reason);

            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "The refresh token was refused.",
                detail: "Request new tokens with POST /users/tokens.");
        }
    }
}
