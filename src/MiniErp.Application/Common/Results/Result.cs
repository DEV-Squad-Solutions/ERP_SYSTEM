namespace MiniErp.Application.Common.Results;

public class Result
{
    protected Result(bool isSuccess, IEnumerable<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        var materializedErrors = errors.ToArray();
        if (materializedErrors.Any(error => error is null))
        {
            throw new ArgumentException(
                "A result cannot contain a null error.",
                nameof(errors));
        }

        if (materializedErrors.Any(error => error == Error.None))
        {
            throw new ArgumentException(
                "A result cannot contain Error.None.",
                nameof(errors));
        }

        if (isSuccess && materializedErrors.Length > 0)
        {
            throw new ArgumentException(
                "A successful result cannot contain errors.",
                nameof(errors));
        }

        if (!isSuccess && materializedErrors.Length == 0)
        {
            throw new ArgumentException(
                "A failed result must contain at least one error.",
                nameof(errors));
        }

        IsSuccess = isSuccess;
        Errors = materializedErrors;
        Error = isSuccess ? Error.None : materializedErrors[0];
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public IReadOnlyList<Error> Errors { get; }

    public static Result Success() => new(true, Array.Empty<Error>());

    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(false, [error]);
    }

    public static Result Failure(IEnumerable<Error> errors) =>
        new(false, errors);
}
