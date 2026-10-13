using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace HexMaster.Battleship.Users.Endpoints;

// Shallow edge validation (ADR 0005): runs the data annotations of the request record and answers
// 400 with validation problem details before the handler is called. The domain model stays the
// authority on invariants.
internal sealed class DataAnnotationsValidationFilter<TRequest> : IEndpointFilter where TRequest : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();
        if (request is null)
        {
            return await next(context);
        }

        List<ValidationResult> results = [];
        if (Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true))
        {
            return await next(context);
        }

        var errors = results
            .SelectMany(result => result.MemberNames.DefaultIfEmpty(string.Empty), (result, member) => (member, result.ErrorMessage))
            .GroupBy(error => ToCamelCase(error.member), error => error.ErrorMessage ?? "The value is invalid.")
            .ToDictionary(group => group.Key, group => group.ToArray());

        return TypedResults.ValidationProblem(errors);
    }

    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
