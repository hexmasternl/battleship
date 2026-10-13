namespace HexMaster.Battleship.Games.Endpoints;

// The problem details types the Games API answers with. They are a contract with the frontend,
// which keys on the trailing slug, so a type never changes once published.
public static class GameProblemTypes
{
    public const string BaseUri = "https://battleship.hexmaster.nl/problems/";

    public const string GameNotFound = BaseUri + "game-not-found";
    public const string GameAlreadyStarted = BaseUri + "game-already-started";
    public const string GameFull = BaseUri + "game-full";
    public const string GameUpdateConflict = BaseUri + "game-update-conflict";
    public const string PlacementOutOfBoard = BaseUri + "placement-out-of-board";
    public const string PlacementOverlap = BaseUri + "placement-overlap";
    public const string PlayerReady = BaseUri + "player-ready";
    public const string FleetIncomplete = BaseUri + "fleet-incomplete";
    public const string GameNotStarted = BaseUri + "game-not-started";
    public const string NotYourTurn = BaseUri + "not-your-turn";
    public const string CellAlreadyShot = BaseUri + "cell-already-shot";
}
