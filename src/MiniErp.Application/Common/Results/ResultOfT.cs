namespace MiniErp.Application.Common.Results;

public sealed class Result<T> : Result
{
    private readonly T? value;

    private Result(T value)
        : base(true, Error.None)
    {
        this.value = value;
    }

    private Result(Error error)
        : base(false, error)
    {
    }

    public T Value => IsSuccess
        ? value!
        : throw new InvalidOperationException(
            "The value of a failed result cannot be accessed.");

    public static Result<T> Success(T value) => new(value);

    public new static Result<T> Failure(Error error) => new(error);
}
