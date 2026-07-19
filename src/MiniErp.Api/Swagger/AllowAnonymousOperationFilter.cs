using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class AllowAnonymousOperationFilter : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        var allowsAnonymous = context.MethodInfo
            .GetCustomAttributes(inherit: true)
            .OfType<AllowAnonymousAttribute>()
            .Any();

        if (allowsAnonymous)
        {
            operation.Security = [];
        }
    }
}
