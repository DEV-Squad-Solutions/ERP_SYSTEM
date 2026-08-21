namespace MiniErp.Application.Common.Results;

public sealed class Result<T> : Result
{
    private readonly T? value;

    private Result(T value)
        : base(true, Array.Empty<Error>())
    {
        this.value = value;
    }

    private Result(Error error)
        : base(false, [error])
    {
    }

    private Result(IEnumerable<Error> errors)
        : base(false, errors)
    {
    }

    public T Value => IsSuccess
        ? value!
        : throw new InvalidOperationException(
            "The value of a failed result cannot be accessed.");

    public static Result<T> Success(T value) => new(value);

    public new static Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(error);
    }

    public new static Result<T> Failure(IEnumerable<Error> errors) =>
        new(errors);
}
