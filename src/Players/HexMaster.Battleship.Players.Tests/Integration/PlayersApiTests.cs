using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HexMaster.Battleship.Players.Abstractions.DataTransferObjects;
using HexMaster.Battleship.Players.Data.TableStorage.Repositories;
using HexMaster.Battleship.Players.Tests.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace HexMaster.Battleship.Players.Tests.Integration;

// End to end through the HTTP pipeline, the real handlers and the Azure Table Storage repositories.
public class PlayersApiTests : IClassFixture<PlayersApiFactory>
{
    private readonly PlayersApiFactory _factory;

    public PlayersApiTests(PlayersApiFactory factory) => _factory = factory;

    [AzuriteFact]
    public async Task CreateTokens_Returns201AndStoresTheUser()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/users/tokens", new CreateTokensRequest("Admiral Nelson"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var tokens = await response.Content.ReadFromJsonAsync<TokenPairResponse>();
        Assert.NotNull(tokens);
        Assert.NotEmpty(tokens.AccessToken);
        Assert.NotEmpty(tokens.RefreshToken);
        Assert.True(tokens.AccessTokenExpiresAt > DateTimeOffset.UtcNow);
        Assert.True(tokens.RefreshTokenExpiresAt > tokens.AccessTokenExpiresAt);

        var user = await new UserTableRepository(Azurite.CreateClient()).GetAsync(SubjectOf(tokens.AccessToken), CancellationToken.None);
        Assert.NotNull(user);
        Assert.Equal("Admiral Nelson", user.DisplayName);
    }

    [AzuriteFact]
    public async Task CreateTokens_WithoutABody_GeneratesADisplayName()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsync("/users/tokens", content: null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var tokens = await response.Content.ReadFromJsonAsync<TokenPairResponse>();
        var user = await new UserTableRepository(Azurite.CreateClient()).GetAsync(SubjectOf(tokens!.AccessToken), CancellationToken.None);
        Assert.False(string.IsNullOrWhiteSpace(user!.DisplayName));
    }

    [AzuriteFact]
    public async Task Refresh_Returns200AndTheOldTokenIsRefusedOnReuse()
    {
        using var client = _factory.CreateClient();
        var created = await CreateTokensAsync(client);

        var refreshed = await client.PostAsJsonAsync("/users/tokens/refresh", new RefreshTokensRequest(created.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var tokens = await refreshed.Content.ReadFromJsonAsync<TokenPairResponse>();
        Assert.NotEqual(created.RefreshToken, tokens!.RefreshToken);
        Assert.Equal(SubjectOf(created.AccessToken), SubjectOf(tokens.AccessToken));

        var reused = await client.PostAsJsonAsync("/users/tokens/refresh", new RefreshTokensRequest(created.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
    }

    [AzuriteFact]
    public async Task Refresh_UnknownToken_Returns401ProblemDetails()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/users/tokens/refresh", new RefreshTokensRequest("never-issued"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(401, problem!.Status);
    }

    [AzuriteFact]
    public async Task CurrentUser_WithAnAccessToken_Returns200()
    {
        using var client = _factory.CreateClient();
        var created = await CreateTokensAsync(client, "Captain Haddock");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/users/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", created.AccessToken);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.Equal(SubjectOf(created.AccessToken), user!.Id);
        Assert.Equal("Captain Haddock", user.DisplayName);
    }

    [AzuriteFact]
    public async Task CurrentUser_WithoutAnAccessToken_Returns401()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.WwwAuthenticate, header => header.Scheme == "Bearer");
    }

    [Fact]
    public void MissingSigningKey_FailsStartup()
    {
        // An empty value also overrides a key a developer keeps in the Players API user secrets.
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting("Players:Tokens:SigningKey", string.Empty));

        // The host reports every failed startup validation together, wrapped in one AggregateException.
        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        var failures = exception is AggregateException aggregate ? aggregate.Flatten().InnerExceptions : [exception];
        Assert.NotEmpty(failures);
        Assert.All(failures, failure =>
        {
            Assert.IsType<OptionsValidationException>(failure);
            Assert.Contains("Players:Tokens:SigningKey", failure.Message);
        });
    }

    private static async Task<TokenPairResponse> CreateTokensAsync(HttpClient client, string? displayName = null)
    {
        var response = await client.PostAsJsonAsync("/users/tokens", new CreateTokensRequest(displayName));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TokenPairResponse>())!;
    }

    private static Guid SubjectOf(string accessToken) => Guid.Parse(new JsonWebToken(accessToken).Subject);
}
