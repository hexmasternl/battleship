using System.Diagnostics;
using HexMaster.Battleship.Core.Cqrs;
using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.Abstractions.Repositories;
using HexMaster.Battleship.Games.DomainModels;
using HexMaster.Battleship.Games.Services;
using HexMaster.Battleship.IntegrationEvents;
using HexMaster.Battleship.IntegrationEvents.Games;
using Microsoft.Extensions.Logging;

namespace HexMaster.Battleship.Games.Features.CreateGame;

public sealed class CreateGameCommandHandler(
    IGameRepository games,
    IGameCodeGenerator codes,
    IIntegrationEventSender events,
    GamesTelemetry telemetry,
    TimeProvider time,
    ILogger<CreateGameCommandHandler> logger) : ICommandHandler<CreateGameCommand, CreateGameResponse>
{
    // With 36^8 possible codes, five collisions in a row mean something is broken, not unlucky.
    public const int MaxCodeAttempts = 5;

    public async Task<CreateGameResponse> Handle(CreateGameCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        using var activity = GamesTelemetry.ActivitySource.StartActivity("games.create");

        var now = time.GetUtcNow();
        for (var attempt = 1; attempt <= MaxCodeAttempts; attempt++)
        {
            var game = Game.Create(command.PlayerId, command.PlayerName, codes.Generate(), now);
            if (!await games.TryAddAsync(game, cancellationToken))
            {
                logger.LogWarning("Game code {GameCode} is already in use (attempt {Attempt} of {MaxAttempts}).",
                    game.GameCode, attempt, MaxCodeAttempts);
                continue;
            }

            activity?.SetTag("game.code", game.GameCode);
            telemetry.GameCreated();
            await AnnounceAsync(game, cancellationToken);

            return new CreateGameResponse(game.Id, game.GameCode);
        }

        activity?.SetStatus(ActivityStatusCode.Error, "No free game code.");
        throw new InvalidOperationException($"No free game code was found in {MaxCodeAttempts} attempts.");
    }

    // The game is stored; a broker hiccup must not fail the player's request. Nothing depends on
    // the event for correctness yet, so a failure is logged and the game stands.
    private async Task AnnounceAsync(Game game, CancellationToken cancellationToken)
    {
        try
        {
            await events.SendAsync(
                new GameCreatedEvent(game.Id, game.GameCode, game.PlayerOne.PlayerId),
                EventAudience.Players(game.PlayerOne.PlayerId),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Publishing {EventType} for game {GameCode} failed; the game was created.",
                GameCreatedEvent.EventType, game.GameCode);
        }
    }
}
