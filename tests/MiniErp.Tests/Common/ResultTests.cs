using MiniErp.Application.Common.Results;

namespace MiniErp.Tests.Common;

public sealed class ResultTests
{
    [Fact]
    public void Failure_WithMultipleErrors_ExposesPrimaryAndAllErrors()
    {
        var errors = new[]
        {
            Error.Conflict(
                code: "Countries.HasInvoices",
                description: "لا يمكن حذف الدولة لارتباطها بفواتير."),
            Error.Conflict(
                code: "Countries.HasCurrentInvoices",
                description: "توجد فواتير حالية مرتبطة بالدولة.")
        };

        var result = Result.Failure(errors);

        Assert.True(result.IsFailure);
        Assert.Equal(errors, result.Errors);
        Assert.Equal(errors[0], result.Error);
    }

    [Fact]
    public void GenericFailure_WithMultipleErrors_ExposesPrimaryAndAllErrors()
    {
        var errors = new[]
        {
            Error.NotFound(
                code: "Countries.NotFound",
                description: "الدولة غير موجودة."),
            Error.Conflict(
                code: "Countries.Inactive",
                description: "الدولة غير نشطة.")
        };

        var result = Result<string>.Failure(errors);

        Assert.True(result.IsFailure);
        Assert.Equal(errors, result.Errors);
        Assert.Equal(errors[0], result.Error);
    }

    [Fact]
    public void Success_ExposesEmptyErrorsAndNonePrimaryError()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Errors);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_RejectsEmptyErrorsAndErrorNone()
    {
        Assert.Throws<ArgumentException>(() => Result.Failure([]));
        Assert.Throws<ArgumentException>(() => Result.Failure([Error.None]));
        Assert.Throws<ArgumentException>(
            () => new InspectableResult(
                isSuccess: true,
                errors:
                [
                    Error.Conflict(
                        code: "Countries.Conflict",
                        description: "يوجد تعارض في البيانات.")
                ]));
    }

    private sealed class InspectableResult : Result
    {
        public InspectableResult(
            bool isSuccess,
            IEnumerable<Error> errors)
            : base(isSuccess, errors)
        {
        }
    }
}
