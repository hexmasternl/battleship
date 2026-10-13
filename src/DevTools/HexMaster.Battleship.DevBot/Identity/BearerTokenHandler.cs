using System.Net;
using System.Net.Http.Headers;

namespace HexMaster.Battleship.DevBot.Identity;

// Adds the bot's access token to every games request. On a 401 it renews the token once and sends
// a copy of the request again; a second 401 is returned to the caller.
public sealed class BearerTokenHandler(TokenStore tokenStore) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await tokenStore.GetAccessTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        response.Dispose();
        var renewed = await tokenStore.RenewAsync(token, cancellationToken);

        using var retry = Copy(request);
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", renewed);
        return await base.SendAsync(retry, cancellationToken);
    }

    // The content object is shared: it is not disposed with the first response and can be read again.
    private static HttpRequestMessage Copy(HttpRequestMessage request)
    {
        var copy = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Content = request.Content,
            Version = request.Version,
            VersionPolicy = request.VersionPolicy,
        };

        foreach (var header in request.Headers)
        {
            copy.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var option in request.Options)
        {
            copy.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);
        }

        return copy;
    }
}
