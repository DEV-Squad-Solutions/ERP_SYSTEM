namespace MiniErp.Application.Common.Results;

public enum ErrorType
{
    None,
    Failure,
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,
    BadGateway,
    GatewayTimeout
}
