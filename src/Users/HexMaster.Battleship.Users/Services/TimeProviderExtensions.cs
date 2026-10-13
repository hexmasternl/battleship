namespace HexMaster.Battleship.Users.Services;

public static class TimeProviderExtensions
{
    // Tokens carry whole seconds, so handlers work in whole seconds too: the user's creation time
    // then equals the token's issue time exactly.
    public static DateTimeOffset GetUtcNowInSeconds(this TimeProvider time) =>
        DateTimeOffset.FromUnixTimeSeconds(time.GetUtcNow().ToUnixTimeSeconds());
}
