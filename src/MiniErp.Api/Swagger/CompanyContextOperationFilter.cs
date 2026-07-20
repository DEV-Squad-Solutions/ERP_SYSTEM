using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using MiniErp.Application.Common.Authentication;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class CompanyContextOperationFilter : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        var controllerType = context.MethodInfo.DeclaringType;
        if (controllerType != typeof(ItemsController) &&
            controllerType != typeof(ItemUnitsController) &&
            controllerType != typeof(StoresController))
        {
            return;
        }

        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = CustomClaimTypes.CompanyHeader,
            In = ParameterLocation.Header,
            Required = false,
            Description = "Active company ID. Optional when the token contains access to exactly one company; required when it contains multiple companies.",
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.Integer,
                Format = "int32"
            }
        });
    }
}
