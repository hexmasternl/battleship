using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HexMaster.Battleship.DevBot.Tests.Factories;

// An HttpMessageHandler that answers from a callback and records every request with its body.
internal sealed class StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public List<(HttpRequestMessage Request, string? Body)> Requests { get; } = [];

    public static HttpResponseMessage JsonResponse(HttpStatusCode status, object body) =>
        new(status) { Content = JsonContent.Create(body, body.GetType(), options: Json) };

    public static HttpResponseMessage Problem(HttpStatusCode status, string? slug, string detail = "Refused.")
    {
        var body = new Dictionary<string, object?>
        {
            ["type"] = slug is null ? null : $"https://battleship.hexmaster.nl/problems/{slug}",
            ["title"] = "Problem",
            ["detail"] = detail,
            ["status"] = (int)status,
        };

        var response = JsonResponse(status, body);
        response.Content.Headers.ContentType = new("application/problem+json");
        return response;
    }

    public HttpClient CreateClient(string baseAddress = "http://gateway/") => new(this) { BaseAddress = new Uri(baseAddress) };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request, body));
        return respond(request);
    }
}
