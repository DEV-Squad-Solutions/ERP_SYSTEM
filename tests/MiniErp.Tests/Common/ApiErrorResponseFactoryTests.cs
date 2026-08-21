using Microsoft.AspNetCore.Http;
using MiniErp.Api.Errors;
using MiniErp.Application.Common.Results;

namespace MiniErp.Tests.Common;

public sealed class ApiErrorResponseFactoryTests
{
    [Fact]
    public void FromErrors_GroupsSafeMessagesByGeneralAndFieldName()
    {
        var response = ApiErrorResponseFactory.FromErrors(
            CreateContext(),
            [
                Error.Conflict(
                    code: "Countries.HasInvoices",
                    description: "لا يمكن حذف الدولة لارتباطها بفواتير حالية أو تاريخية."),
                Error.Conflict(
                    code: "Countries.HasCurrentInvoices",
                    description: "توجد فواتير حالية مرتبطة بالدولة."),
                Error.Conflict(
                    code: "Countries.CodeExists",
                    description: "كود الدولة مستخدم بالفعل.",
                    fieldName: "Code")
            ]);

        Assert.Equal(StatusCodes.Status409Conflict, response.Status);
        Assert.Equal("Countries.HasInvoices", response.ErrorCode);
        Assert.Equal(ErrorType.Conflict.ToString(), response.ErrorType);
        Assert.Equal(
            [
                "لا يمكن حذف الدولة لارتباطها بفواتير حالية أو تاريخية.",
                "توجد فواتير حالية مرتبطة بالدولة."
            ],
            response.Errors["General"]);
        Assert.Equal(["كود الدولة مستخدم بالفعل."], response.Errors["Code"]);
    }

    [Fact]
    public void FromError_WithMappedMessages_AddsThemToPrimaryErrorBucket()
    {
        var response = ApiErrorResponseFactory.FromError(
            CreateContext(),
            Error.Conflict(
                code: "Countries.HasInvoices",
                description: "لا يمكن حذف الدولة لارتباطها بفواتير."),
            messages:
            [
                "توجد فواتير حالية مرتبطة بالدولة.",
                "توجد فواتير تاريخية مرتبطة بالدولة."
            ]);

        Assert.Equal(
            [
                "لا يمكن حذف الدولة لارتباطها بفواتير.",
                "توجد فواتير حالية مرتبطة بالدولة.",
                "توجد فواتير تاريخية مرتبطة بالدولة."
            ],
            response.Errors["General"]);
        Assert.Equal("Countries.HasInvoices", response.ErrorCode);
        Assert.Equal(StatusCodes.Status409Conflict, response.Status);
    }

    [Fact]
    public void FromError_WithSingleBusinessError_PreservesSingleErrorResponse()
    {
        var response = ApiErrorResponseFactory.FromError(
            CreateContext(),
            Error.Conflict(
                code: "Countries.CodeAlreadyExists",
                description: "كود الدولة مستخدم بالفعل."));

        Assert.Equal(
            ["كود الدولة مستخدم بالفعل."],
            response.Errors["General"]);
        Assert.Equal(StatusCodes.Status409Conflict, response.Status);
    }

    [Fact]
    public void Unexpected_DoesNotExposeExceptionMessages()
    {
        var response = ApiErrorResponseFactory.Unexpected(CreateContext());

        Assert.Equal(
            ["حدث خطأ غير متوقع أثناء معالجة الطلب."],
            response.Errors["General"]);
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/Countries";
        return context;
    }
}
