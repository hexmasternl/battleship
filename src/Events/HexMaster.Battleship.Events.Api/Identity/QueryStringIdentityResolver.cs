namespace HexMaster.Battleship.Events.Api.Identity;

// Trusts the userId query parameter. No authorization until identities exist.
public sealed class QueryStringIdentityResolver : IConnectionIdentityResolver
{
    public const string UserIdParameter = "userId";

    public string? ResolveUserId(HttpContext httpContext)
    {
        var userId = httpContext.Request.Query[UserIdParameter].ToString().Trim();

        return userId.Length == 0 ? null : userId;
    }
}
