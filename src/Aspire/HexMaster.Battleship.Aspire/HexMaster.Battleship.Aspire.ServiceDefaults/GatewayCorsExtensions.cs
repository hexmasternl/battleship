using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting;

// The browser calls the module APIs from the frontend's origin, which is never the API's own: locally
// the YARP gateway runs on a different port than the frontend, and in Azure the environment's route
// configuration is called from the frontend's custom hostname. So every module API answers CORS
// requests (preflights included) for the origins listed in Gateway:AllowedOrigins, which the AppHost
// sets to the frontend endpoint of the current run and the deployment to the frontend hostname. The
// policy exists only when at least one origin is configured; without it the API accepts no
// cross-origin request, in every environment.
public static class GatewayCorsExtensions
{
    public const string PolicyName = "gateway";
    public const string AllowedOriginsKey = "Gateway:AllowedOrigins";

    public static TBuilder AddGatewayCors<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var origins = ConfiguredOrigins(builder.Configuration);
        if (origins.Length == 0)
        {
            return builder;
        }

        builder.Services.AddCors(options => options.AddPolicy(PolicyName, policy => policy
            .WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod()));

        return builder;
    }

    public static WebApplication UseGatewayCors(this WebApplication app)
    {
        if (ConfiguredOrigins(app.Configuration).Length > 0)
        {
            app.UseCors(PolicyName);
        }

        return app;
    }

    /// <summary>The configured origins, comma or semicolon separated, without trailing slashes.</summary>
    private static string[] ConfiguredOrigins(IConfiguration configuration) =>
        (configuration[AllowedOriginsKey] ?? string.Empty)
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(origin => origin.TrimEnd('/'))
            .ToArray();
}
