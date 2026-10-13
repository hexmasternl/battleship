using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace Microsoft.Extensions.Hosting;

// Describes the bearer access tokens of AddBattleshipAuthentication() in the OpenAPI document, so
// the API reference can send one. The scheme is added to the components only when the API
// registered the Bearer authentication scheme, and the requirement only to operations whose
// endpoint requires authorization and does not allow anonymous access.
// An API without authentication (Events) has no scheme provider, and gets neither.
internal sealed class BearerSecuritySchemeTransformer(IServiceProvider services)
    : IOpenApiDocumentTransformer, IOpenApiOperationTransformer
{
    public const string SchemeName = JwtBearerDefaults.AuthenticationScheme;

    public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        if (!await HasBearerSchemeAsync())
        {
            return;
        }

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "An access token from POST /users/tokens.",
        };
    }

    public async Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        if (!metadata.OfType<IAuthorizeData>().Any()
            || metadata.OfType<IAllowAnonymous>().Any()
            || !await HasBearerSchemeAsync())
        {
            return;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SchemeName, context.Document)] = [],
        });
    }

    private async Task<bool> HasBearerSchemeAsync() =>
        services.GetService<IAuthenticationSchemeProvider>() is { } schemes
        && await schemes.GetSchemeAsync(SchemeName) is not null;
}
