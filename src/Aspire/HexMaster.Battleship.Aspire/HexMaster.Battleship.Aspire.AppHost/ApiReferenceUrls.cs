namespace HexMaster.Battleship.Aspire.AppHost;

/// <summary>
/// Adds an "API reference" link to an API resource on the Aspire dashboard. It opens the Scalar
/// reference the API serves in Development (see <c>ApiReferenceExtensions</c> in ServiceDefaults)
/// on the API's own http endpoint, so it never goes through the gateway.
/// </summary>
internal static class ApiReferenceUrls
{
    public const string DisplayText = "API reference";

    public static IResourceBuilder<ProjectResource> WithApiReferenceUrl(this IResourceBuilder<ProjectResource> api) =>
        api.WithUrlForEndpoint("http", _ => new ResourceUrlAnnotation
        {
            Url = AspireConstants.ApiReferencePath,
            DisplayText = DisplayText,
        });
}
