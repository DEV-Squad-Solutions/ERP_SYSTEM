using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Errors;
using MiniErp.Application.Common.Results;

namespace MiniErp.Api.Extensions;

public static class ResultExtensions
{
    public static IActionResult ToActionResult<T>(
        this ControllerBase controller,
        Result<T> result) =>
        result.IsSuccess
            ? controller.Ok(result.Value)
            : controller.ToProblem(result.Errors);

    public static IActionResult ToActionResult(
        this ControllerBase controller,
        Result result) =>
        result.IsSuccess
            ? controller.NoContent()
            : controller.ToProblem(result.Errors);

    public static IActionResult ToProblem(
        this ControllerBase controller,
        Error error)
        => controller.ToProblem([error]);

    public static IActionResult ToProblem(
        this ControllerBase controller,
        IEnumerable<Error> errors)
    {
        var response = ApiErrorResponseFactory.FromErrors(
            controller.HttpContext,
            errors);
        return ApiErrorResponseFactory.ToObjectResult(response);
    }
}
