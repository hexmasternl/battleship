using System.Diagnostics;
using HexMaster.Battleship.DevBot.Client;
using HexMaster.Battleship.DevBot.Observability;
using HexMaster.Battleship.DevBot.Placement;
using HexMaster.Battleship.DevBot.Strategies;
using HexMaster.Battleship.Games.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Games.Abstractions.DomainModels;

namespace HexMaster.Battleship.DevBot.Sessions;

// One game the bot plays. Events and timers only trigger ReconcileAsync; what to do is decided from
// the server's view of the game every time, so missed, repeated or out-of-order events do no harm.
// One reconcile runs at a time per game.
public sealed partial class GameSession(
    string code,
    IGamesClient gamesClient,
    BotSettings settings,
    TimeProvider timeProvider,
    Random random,
    ILogger logger)
{
    // A shot always passes the turn, so a reconcile needs only a few steps: place, ready, fire, and
    // one re-read after a refused shot.
    internal const int MaxSteps = 8;

    private readonly SemaphoreSlim gate = new(1, 1);
    private bool replacedFleet;

    public string Code { get; } = code;

    public bool IsEnded { get; private set; }

    public async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            using var activity = DevBotTelemetry.ActivitySource.StartActivity("devbot.reconcile");
            activity?.SetTag(DevBotTelemetry.GameCodeAttribute, Code);

            var outcome = await RunAsync(cancellationToken);
            activity?.SetTag(DevBotTelemetry.OutcomeAttribute, outcome);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<string> RunAsync(CancellationToken cancellationToken)
    {
        if (IsEnded)
        {
            return "ended";
        }

        var read = await gamesClient.GetGameAsync(Code, cancellationToken);
        for (var step = 0; step < MaxSteps; step++)
        {
            if (read.Status == GameCallStatus.NotFound)
            {
                return End("not-found");
            }

            if (!read.IsOk)
            {
                LogReadFailed(logger, Code, read.Status, read.Detail);
                return "read-failed";
            }

            var view = read.Value;
            if (view.State is GameState.Completed or GameState.Abandoned)
            {
                return End(view.State == GameState.Completed && view.WinnerId == view.Me.PlayerId ? "won" : "over");
            }

            if (!view.Me.Ready)
            {
                if (view.Me.Ships.Count < Fleet.Kinds.Count)
                {
                    if (!await PlaceMissingShipsAsync(view, cancellationToken))
                    {
                        return "placement-failed";
                    }
                }

                read = await gamesClient.ReadyAsync(Code, cancellationToken);
                if (read.Status == GameCallStatus.FleetIncomplete)
                {
                    read = await gamesClient.GetGameAsync(Code, cancellationToken);
                }

                continue;
            }

            if (view.State != GameState.Started || !view.Me.HasTurn)
            {
                return "waiting";
            }

            await Task.Delay(settings.ShotDelay, timeProvider, cancellationToken);

            var intelligence = settings.Intelligence;
            var at = ShotStrategies.For(intelligence).NextShot(view.Me.Shots, random);
            var shot = await gamesClient.FireAsync(Code, at, cancellationToken);
            if (shot.IsOk)
            {
                LogFired(logger, Code, at.Column, at.Row, shot.Value.Hit, intelligence);
                return shot.Value.State == GameState.Completed ? End("won") : "fired";
            }

            if (shot.Status == GameCallStatus.NotFound)
            {
                return End("not-found");
            }

            // Not our turn, cell already shot or game over: the view was stale. Read it again rather
            // than retrying the same shot.
            LogShotRefused(logger, Code, at.Column, at.Row, shot.Status);
            read = await gamesClient.GetGameAsync(Code, cancellationToken);
        }

        return "step-limit";
    }

    private async Task<bool> PlaceMissingShipsAsync(GameView view, CancellationToken cancellationToken)
    {
        var placed = view.Me.Ships
            .Select(s => new ShipPlacement(s.Kind, new Coordinate(s.Column, s.Row), s.Orientation))
            .ToList();

        foreach (var placement in RandomFleet.Complete(random, placed))
        {
            var result = await gamesClient.PlaceShipAsync(Code, placement, cancellationToken);
            if (result.IsOk)
            {
                continue;
            }

            if (result.Status == GameCallStatus.PlacementRefused && !replacedFleet)
            {
                // Should not happen: the placements avoid the ships the server holds. Read the game
                // again and place what is missing once more; a second refusal is an error.
                replacedFleet = true;
                LogPlacementRefused(logger, Code, placement.Kind, result.Detail);
                var read = await gamesClient.GetGameAsync(Code, cancellationToken);
                return read.IsOk && await PlaceMissingShipsAsync(read.Value, cancellationToken);
            }

            LogPlacementFailed(logger, Code, placement.Kind, result.Status, result.Detail);
            return false;
        }

        return true;
    }

    private string End(string outcome)
    {
        IsEnded = true;
        LogEnded(logger, Code, outcome);
        Activity.Current?.SetTag(DevBotTelemetry.OutcomeAttribute, outcome);
        return outcome;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Game {GameCode}: fired at ({Column}, {Row}), hit {Hit}, playing {Intelligence}.")]
    private static partial void LogFired(ILogger logger, string gameCode, int column, int row, bool hit, BotIntelligence intelligence);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Game {GameCode}: shot at ({Column}, {Row}) refused with {Status}; reading the game again.")]
    private static partial void LogShotRefused(ILogger logger, string gameCode, int column, int row, GameCallStatus status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Game {GameCode}: could not read the game ({Status}): {Detail}")]
    private static partial void LogReadFailed(ILogger logger, string gameCode, GameCallStatus status, string? detail);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Game {GameCode}: placing the {Kind} was refused ({Detail}); placing the missing ships again.")]
    private static partial void LogPlacementRefused(ILogger logger, string gameCode, ShipKind kind, string? detail);

    [LoggerMessage(Level = LogLevel.Error, Message = "Game {GameCode}: could not place the {Kind} ({Status}): {Detail}")]
    private static partial void LogPlacementFailed(ILogger logger, string gameCode, ShipKind kind, GameCallStatus status, string? detail);

    [LoggerMessage(Level = LogLevel.Information, Message = "Game {GameCode}: the bot stops playing ({Outcome}).")]
    private static partial void LogEnded(ILogger logger, string gameCode, string outcome);
}
