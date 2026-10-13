using HexMaster.Battleship.Aspire;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Scalar.AspNetCore;

namespace Microsoft.Extensions.Hosting;

// How every Battleship API documents itself: the built-in OpenAPI document (/openapi/v1.json) and
// a Scalar API reference on AspireConstants.ApiReferencePath. Both are mapped only in Development;
// in any other environment neither path exists.
public static class ApiReferenceExtensions
{
    public static TBuilder AddApiReference<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddOpenApi();

        return builder;
    }

    public static WebApplication MapApiReference(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return app;
        }

        app.MapOpenApi();
        app.MapScalarApiReference(AspireConstants.ApiReferencePath, options => options
            .WithTitle(app.Environment.ApplicationName));

        return app;
    }
}
