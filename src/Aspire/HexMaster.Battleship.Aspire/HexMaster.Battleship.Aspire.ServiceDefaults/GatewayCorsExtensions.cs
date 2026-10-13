using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting;

// During local development the browser calls the YARP gateway on a different port than the one
// the frontend is served from, so every module API must answer CORS requests (preflights
// included, which the gateway forwards) for the frontend's origin. The AppHost sets
// Gateway:AllowedOrigins to the frontend endpoint of the current run. Outside Development the
// policy is not registered, so the APIs accept no cross-origin requests.
public static class GatewayCorsExtensions
{
    public const string PolicyName = "gateway";
    public const string AllowedOriginsKey = "Gateway:AllowedOrigins";

    public static TBuilder AddGatewayCors<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        if (!builder.Environment.IsDevelopment())
        {
            return builder;
        }

        var origins = (builder.Configuration[AllowedOriginsKey] ?? string.Empty)
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(origin => origin.TrimEnd('/'))
            .ToArray();

        builder.Services.AddCors(options => options.AddPolicy(PolicyName, policy => policy
            .WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod()));

        return builder;
    }

    public static WebApplication UseGatewayCors(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseCors(PolicyName);
        }

        return app;
    }
}
