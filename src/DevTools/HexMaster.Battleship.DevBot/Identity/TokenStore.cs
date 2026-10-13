using System.Buffers.Text;
using System.Text.Json;
using HexMaster.Battleship.DevBot.Client;
using HexMaster.Battleship.Users.Abstractions.DataTransferObjects;

namespace HexMaster.Battleship.DevBot.Identity;

// The bot's own anonymous user and its token pair, kept in memory. The access token is refreshed a
// minute before it expires, and on demand after a 401. When a refresh is refused the bot becomes a
// new user: the games of the old identity are lost, which is logged.
public sealed partial class TokenStore(IUsersClient usersClient, TimeProvider timeProvider, ILogger<TokenStore> logger)
{
    public const string DisplayName = "Dev Bot";

    internal static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(1);

    private readonly SemaphoreSlim gate = new(1, 1);
    private TokenPairResponse? tokens;
    private Guid userId;

    // The bot's user id, creating the user on first use.
    public async Task<Guid> GetUserIdAsync(CancellationToken cancellationToken)
    {
        await GetAccessTokenAsync(cancellationToken);
        return userId;
    }

    // The current user id, or null before the user exists. For read-only callers such as the state endpoint.
    public Guid? CurrentUserId => tokens is null ? null : userId;

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (tokens is null)
            {
                await CreateUserAsync(cancellationToken);
            }
            else if (timeProvider.GetUtcNow() >= tokens.AccessTokenExpiresAt - RefreshMargin)
            {
                await RefreshAsync(cancellationToken);
            }

            return tokens!.AccessToken;
        }
        finally
        {
            gate.Release();
        }
    }

    // Called after a 401 with the token that was refused. When another call already replaced that
    // token, the newer one is returned without a second refresh.
    public async Task<string> RenewAsync(string refusedAccessToken, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (tokens is null)
            {
                await CreateUserAsync(cancellationToken);
            }
            else if (tokens.AccessToken == refusedAccessToken)
            {
                await RefreshAsync(cancellationToken);
            }

            return tokens!.AccessToken;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var refreshed = await usersClient.RefreshAsync(tokens!.RefreshToken, cancellationToken);
        if (refreshed is null)
        {
            LogRefreshRefused(logger, userId);
            await CreateUserAsync(cancellationToken);
            return;
        }

        tokens = refreshed;
    }

    private async Task CreateUserAsync(CancellationToken cancellationToken)
    {
        var created = await usersClient.CreateTokensAsync(DisplayName, cancellationToken);
        userId = ReadSubject(created.AccessToken);
        tokens = created;
        LogUserCreated(logger, userId);
    }

    // The sub claim of a JWT, read without validation: the token comes straight from the Users service.
    internal static Guid ReadSubject(string accessToken)
    {
        var parts = accessToken.Split('.');
        if (parts.Length != 3)
        {
            throw new InvalidOperationException("The access token is not a JWT.");
        }

        using var payload = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[1]));
        return payload.RootElement.TryGetProperty("sub", out var sub) && Guid.TryParse(sub.GetString(), out var id)
            ? id
            : throw new InvalidOperationException("The access token has no user id.");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "The dev bot plays as user {UserId}.")]
    private static partial void LogUserCreated(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The refresh token of user {UserId} was refused; the dev bot continues as a new user and leaves the games of the old one.")]
    private static partial void LogRefreshRefused(ILogger logger, Guid userId);
}
