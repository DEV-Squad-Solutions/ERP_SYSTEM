using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Errors;
using MiniErp.Api.Extensions;
using MiniErp.Application.Common.Results;

namespace MiniErp.Tests.Common;

public sealed class ResultExtensionsTests
{
    [Fact]
    public void ToActionResult_MapsAllGeneralErrorsWithoutChangingPrimaryMetadata()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/Countries/1";
        var controller = new TestController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = context
            }
        };
        var result = Result.Failure(
            [
                Error.Conflict(
                    code: "Countries.HasInvoices",
                    description: "لا يمكن حذف الدولة لارتباطها بفواتير."),
                Error.Conflict(
                    code: "Countries.HasCurrentInvoices",
                    description: "توجد فواتير حالية مرتبطة بالدولة.")
            ]);

        var actionResult = controller.ToActionResult(result);
        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        var response = Assert.IsType<ApiErrorResponse>(objectResult.Value);

        Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
        Assert.Equal("Countries.HasInvoices", response.ErrorCode);
        Assert.Equal("Conflict", response.ErrorType);
        Assert.Equal(
            [
                "لا يمكن حذف الدولة لارتباطها بفواتير.",
                "توجد فواتير حالية مرتبطة بالدولة."
            ],
            response.Errors["General"]);
    }

    private sealed class TestController : ControllerBase
    {
    }
}
