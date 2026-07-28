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
        var response = ApiErrorResponseFactory.FromError(
            controller.HttpContext,
            error);
        return ApiErrorResponseFactory.ToObjectResult(response);
    }
}
