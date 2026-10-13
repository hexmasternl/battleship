namespace HexMaster.Battleship.Games.DomainModels;

// What joining a game did: seated the caller as player two, or nothing because the caller already plays.
public enum JoinOutcome
{
    Joined,
    AlreadyPlayer,
}
