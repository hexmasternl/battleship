namespace HexMaster.Battleship.Events.Api.Identity;

// Trusts the playerId query parameter. No authorization until identities exist.
public sealed class QueryStringIdentityResolver : IConnectionIdentityResolver
{
    public const string PlayerIdParameter = "playerId";

    public string? ResolvePlayerId(HttpContext httpContext)
    {
        var playerId = httpContext.Request.Query[PlayerIdParameter].ToString().Trim();

        return playerId.Length == 0 ? null : playerId;
    }
}
