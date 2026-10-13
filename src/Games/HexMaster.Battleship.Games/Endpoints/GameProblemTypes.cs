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
}
