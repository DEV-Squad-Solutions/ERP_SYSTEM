using Microsoft.AspNetCore.Mvc;
using MiniErp.Application.Common.Results;

namespace MiniErp.Api.Extensions;

public static class ResultExtensions
{
    public static IActionResult ToActionResult<T>(
        this ControllerBase controller,
        Result<T> result) =>
        result.IsSuccess
            ? controller.Ok(result.Value)
            : controller.ToProblem(result.Error);

    public static IActionResult ToActionResult(
        this ControllerBase controller,
        Result result) =>
        result.IsSuccess
            ? controller.NoContent()
            : controller.ToProblem(result.Error);

    public static IActionResult ToProblem(
        this ControllerBase controller,
        Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = error.Code,
            Detail = error.Description
        };

        problemDetails.Extensions["errorType"] = error.Type.ToString();

        return new ObjectResult(problemDetails)
        {
            StatusCode = statusCode
        };
    }
}
