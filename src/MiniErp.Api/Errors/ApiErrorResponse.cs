namespace MiniErp.Api.Errors;

public sealed record ApiErrorResponse
{
    public required string Type { get; init; }

    public required string Title { get; init; }

    public required int Status { get; init; }

    public required string Detail { get; init; }

    public required string Instance { get; init; }

    public required string ErrorCode { get; init; }

    public required string ErrorType { get; init; }

    public required IReadOnlyDictionary<string, string[]> Errors { get; init; }

    public required string TraceId { get; init; }
}
