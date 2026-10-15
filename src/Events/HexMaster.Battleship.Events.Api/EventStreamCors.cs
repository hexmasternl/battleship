namespace HexMaster.Battleship.Events.Api;

// The browser opens GET /events through the gateway, which forwards the browser's origin unchanged
// (the Angular dev-server proxy is not used: it would buffer the stream), so the stream allows the
// frontend origins listed under Events:AllowedOrigins.
public static class EventStreamCors
{
    public const string PolicyName = "event-stream";
    public const string ConfigurationKey = "Events:AllowedOrigins";

    public static readonly string[] DefaultOrigins = ["http://localhost:4200"];

    public static IServiceCollection AddEventStreamCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection(ConfigurationKey).Get<string[]>() is { Length: > 0 } configured
            ? configured
            : DefaultOrigins;

        services.AddCors(options => options.AddPolicy(PolicyName, policy => policy
            .WithOrigins(origins)
            .WithMethods(HttpMethods.Get)
            // EventSource sends Last-Event-ID when it reconnects.
            .WithHeaders("Last-Event-ID", "Cache-Control")));

        return services;
    }
}
