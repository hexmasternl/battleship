using System.Net.Http.Json;
using System.Text.Json;

namespace HexMaster.Battleship.Aspire.AppHost.DevBot;

/// <summary>The bot's state as its admin API reports it.</summary>
internal sealed record BotState(string Intelligence, bool AutoJoin, IReadOnlyList<string> Games);

/// <summary>
/// Calls the internal admin API of the DevBot worker on its own HTTP endpoint (never through the
/// gateway) and turns each answer into a command result: 2xx is success, a problem response is a
/// failure with its detail.
/// </summary>
internal sealed class BotAdminClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // One handler for all calls from the AppHost; the endpoint is local, so connection reuse is fine.
    private static readonly HttpMessageHandler SharedHandler = new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(1),
    };

    public static BotAdminClient For(BattleshipBotResource bot) =>
        new(new HttpClient(SharedHandler, disposeHandler: false)
        {
            BaseAddress = new Uri(bot.HttpEndpoint.Url),
            Timeout = TimeSpan.FromSeconds(10),
        });

    public Task<ExecuteCommandResult> SetIntelligenceAsync(string intelligence, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Put, "admin/intelligence", new { intelligence },
            $"The bot now plays {intelligence} from its next shot.", cancellationToken);

    public Task<ExecuteCommandResult> SetAutoJoinAsync(bool enabled, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Put, "admin/auto-join", new { enabled },
            enabled ? "Auto-join is on." : "Auto-join is off; games in progress continue.", cancellationToken);

    public Task<ExecuteCommandResult> JoinGameAsync(string code, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, $"admin/games/{Uri.EscapeDataString(code)}", null,
            $"The bot joined game {code}.", cancellationToken);

    public async Task<BotState?> GetStateAsync(CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync("admin/state", cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<BotState>(Json, cancellationToken)
            : null;
    }

    private async Task<ExecuteCommandResult> SendAsync(
        HttpMethod method, string path, object? body, string successMessage, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return CommandResults.Success(successMessage);
            }

            return CommandResults.Failure(await ReadProblemAsync(response, cancellationToken));
        }
        catch (HttpRequestException exception)
        {
            return CommandResults.Failure($"The bot did not answer: {exception.Message}");
        }
    }

    private static async Task<string> ReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemBody>(Json, cancellationToken);
            if ((problem?.Detail ?? problem?.Title) is { } message)
            {
                return message;
            }
        }
        catch (JsonException)
        {
        }

        return $"The bot answered {(int)response.StatusCode} {response.ReasonPhrase}.";
    }

    private sealed record ProblemBody(string? Title, string? Detail);
}
