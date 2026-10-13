using System.Globalization;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using HexMaster.Battleship.Events.Api.Identity;
using HexMaster.Battleship.Events.Routing;
using HexMaster.Battleship.Events.Streaming;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace HexMaster.Battleship.Events.Api.Endpoints;

// GET /events: the long-lived server-sent events stream a browser tab holds open.
public static class EventStreamEndpoints
{
    public const string LastEventIdHeader = "Last-Event-ID";

    public static IEndpointRouteBuilder MapEventStreamEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/events").WithTags("Events");

        group.MapGet("", OpenStream)
            .WithName("OpenEventStream")
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private static Results<ServerSentEventsResult<JsonElement>, ProblemHttpResult> OpenStream(
        HttpContext httpContext,
        IConnectionIdentityResolver identityResolver,
        IEventRouter router,
        IOptions<StreamOptions> streamOptions,
        TimeProvider timeProvider)
    {
        var playerId = identityResolver.ResolvePlayerId(httpContext);

        if (playerId is null)
        {
            return TypedResults.Problem(
                title: "Player identity required",
                detail: $"Identify the player with the '{QueryStringIdentityResolver.PlayerIdParameter}' query parameter.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        httpContext.Features.GetRequiredFeature<IHttpResponseBodyFeature>().DisableBuffering();

        var lastEventId = ParseLastEventId(httpContext.Request.Headers[LastEventIdHeader]);

        return TypedResults.ServerSentEvents(
            Stream(
                httpContext,
                router,
                playerId,
                lastEventId,
                streamOptions.Value.HeartbeatInterval,
                timeProvider,
                httpContext.RequestAborted));
    }

    private static async IAsyncEnumerable<SseItem<JsonElement>> Stream(
        HttpContext httpContext,
        IEventRouter router,
        string playerId,
        long? lastEventId,
        TimeSpan heartbeatInterval,
        TimeProvider timeProvider,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var connection = router.Connect(playerId, lastEventId);

        // A failed write (client gone) or an aborted request ends the enumeration, and the
        // finally block takes the connection out of routing.
        try
        {
            // Send the headers now, so the client sees an open stream before the first event.
            await httpContext.Response.Body.FlushAsync(cancellationToken);

            await foreach (var item in HeartbeatStream.ReadAsync(
                connection.Reader, heartbeatInterval, timeProvider, cancellationToken))
            {
                yield return item;
            }
        }
        finally
        {
            router.Disconnect(connection);
        }
    }

    // An unparsable id is treated as no id: the client continues with live events.
    private static long? ParseLastEventId(string? value) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;
}
