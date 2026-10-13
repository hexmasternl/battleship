namespace HexMaster.Battleship.Events.Api.Identity;

// Works out which player opened an event stream. The one place that changes when access
// tokens replace the query parameter (issue-7-user-identification).
public interface IConnectionIdentityResolver
{
    // Null when the request does not identify a player.
    string? ResolvePlayerId(HttpContext httpContext);
}
