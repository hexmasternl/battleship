using System.Net.Http.Json;
using HexMaster.Battleship.Users.Abstractions.DataTransferObjects;

namespace HexMaster.Battleship.DevBot.Client;

// The anonymous token endpoints under /users/tokens on the gateway. No bearer token is sent.
public interface IUsersClient
{
    Task<TokenPairResponse> CreateTokensAsync(string displayName, CancellationToken cancellationToken);

    // Null when the refresh token is refused (expired, already used or unknown).
    Task<TokenPairResponse?> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
}

public sealed class UsersClient(HttpClient httpClient) : IUsersClient
{
    public async Task<TokenPairResponse> CreateTokensAsync(string displayName, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync("users/tokens", new CreateTokensRequest(displayName), BotJson.Options, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<TokenPairResponse>(BotJson.Options, cancellationToken)
            ?? throw new InvalidOperationException("The token response had no body.");
    }

    public async Task<TokenPairResponse?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync("users/tokens/refresh", new RefreshTokensRequest(refreshToken), BotJson.Options, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<TokenPairResponse>(BotJson.Options, cancellationToken);
    }
}
