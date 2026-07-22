using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
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
        foreach (var field in validationProblemDetails.Errors.Keys.ToArray())
        {
            validationProblemDetails.Errors[field] = validationProblemDetails
                .Errors[field]
                .Select(message => message.Any(character =>
                    character is >= 'A' and <= 'Z' or >= 'a' and <= 'z')
                        ? "القيمة المرسلة غير صحيحة أو لا تطابق نوع الحقل المطلوب."
                        : message)
                .ToArray();
        }

        validationProblemDetails.Status = StatusCodes.Status400BadRequest;
        validationProblemDetails.Title = "فشل التحقق من صحة البيانات.";
        validationProblemDetails.Detail =
            "يرجى مراجعة الحقول غير الصحيحة والمحاولة مرة أخرى.";
        validationProblemDetails.Instance = context.HttpContext.Request.Path;

        return Task.FromResult<IActionResult?>(
            new BadRequestObjectResult(validationProblemDetails));
    }
}
