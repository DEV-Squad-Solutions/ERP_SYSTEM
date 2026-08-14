namespace MiniErp.Application.Common.Results;

public sealed record Error(
    string Code,
    string Description,
    ErrorType Type,
    string? FieldName = null)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.None);

    public static Error Failure(
        string code,
        string description,
        string? fieldName = null) =>
        new(code, description, ErrorType.Failure, fieldName);

    public static Error Validation(
        string code,
        string description,
        string? fieldName = null) =>
        new(code, description, ErrorType.Validation, fieldName);

    public static Error NotFound(
        string code,
        string description,
        string? fieldName = null) =>
        new(code, description, ErrorType.NotFound, fieldName);

    public static Error Conflict(
        string code,
        string description,
        string? fieldName = null) =>
        new(code, description, ErrorType.Conflict, fieldName);

    public static Error Unauthorized(
        string code,
        string description,
        string? fieldName = null) =>
        new(code, description, ErrorType.Unauthorized, fieldName);

    public static Error Forbidden(
        string code,
        string description,
        string? fieldName = null) =>
        new(code, description, ErrorType.Forbidden, fieldName);

    public static Error BadGateway(
        string code,
        string description,
        string? fieldName = null) =>
        new(code, description, ErrorType.BadGateway, fieldName);

    public static Error GatewayTimeout(
        string code,
        string description,
        string? fieldName = null) =>
        new(code, description, ErrorType.GatewayTimeout, fieldName);
}
