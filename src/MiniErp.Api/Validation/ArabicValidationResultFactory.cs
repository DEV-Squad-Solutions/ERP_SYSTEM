using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MiniErp.Api.Errors;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Results;

namespace MiniErp.Api.Validation;

public sealed class ArabicValidationResultFactory
    : IFluentValidationAutoValidationResultFactory
{
    public Task<IActionResult?> CreateActionResult(
        ActionExecutingContext context,
        ValidationProblemDetails validationProblemDetails,
        IDictionary<IValidationContext, ValidationResult> validationResults)
    {
        var response = ApiErrorResponseFactory.Validation(
            context.HttpContext,
            validationProblemDetails.Errors);

        return Task.FromResult<IActionResult?>(
            ApiErrorResponseFactory.ToObjectResult(response));
    }
}
