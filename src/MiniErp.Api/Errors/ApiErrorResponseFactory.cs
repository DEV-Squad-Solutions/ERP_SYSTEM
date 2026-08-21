using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using MiniErp.Application.Common.Results;

namespace MiniErp.Api.Errors;

public static class ApiErrorResponseFactory
{
    private const string ProblemContentType = "application/problem+json";

    public static ApiErrorResponse FromError(
        HttpContext httpContext,
        Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return FromErrors(httpContext, [error]);
    }

    public static ApiErrorResponse FromError(
        HttpContext httpContext,
        IEnumerable<Error> errors) =>
        FromErrors(httpContext, errors);

    public static ApiErrorResponse FromError(
        HttpContext httpContext,
        Error error,
        IEnumerable<string> messages)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(messages);

        var mappedErrors = new List<Error> { error };
        mappedErrors.AddRange(
            messages
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Select(message => new Error(
                    Code: error.Code,
                    Description: message,
                    Type: error.Type,
                    FieldName: error.FieldName)));

        return FromErrors(httpContext, mappedErrors);
    }

    public static ApiErrorResponse FromErrors(
        HttpContext httpContext,
        IEnumerable<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        var materializedErrors = errors
            .Where(error => error is not null && error != Error.None)
            .ToArray();
        if (materializedErrors.Length == 0)
        {
            throw new ArgumentException(
                "At least one mapped error is required.",
                nameof(errors));
        }

        var primaryError = materializedErrors[0];
        var statusCode = primaryError.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.BadGateway => StatusCodes.Status502BadGateway,
            ErrorType.GatewayTimeout => StatusCodes.Status504GatewayTimeout,
            _ => StatusCodes.Status500InternalServerError
        };

        var title = primaryError.Type switch
        {
            ErrorType.Validation => "بيانات الطلب غير صحيحة.",
            ErrorType.Unauthorized => "يجب تسجيل الدخول أولًا.",
            ErrorType.Forbidden => "غير مسموح بتنفيذ هذا الإجراء.",
            ErrorType.NotFound => "العنصر المطلوب غير موجود.",
            ErrorType.Conflict => "يوجد تعارض في البيانات.",
            ErrorType.BadGateway => "مزود الخدمة الخارجي غير متاح.",
            ErrorType.GatewayTimeout => "انتهت مهلة مزود الخدمة الخارجي.",
            _ => "حدث خطأ أثناء معالجة الطلب."
        };

        return Create(
            httpContext,
            statusCode,
            title,
            primaryError.Description,
            primaryError.Code,
            primaryError.Type.ToString(),
            MapErrors(materializedErrors));
    }

    public static ApiErrorResponse FromError(
        HttpContext httpContext,
        Error error,
        params string[] messages) =>
        FromError(httpContext, error, (IEnumerable<string>)messages);

    public static ApiErrorResponse Validation(
        HttpContext httpContext,
        IEnumerable<KeyValuePair<string, string[]>> errors) =>
        Create(
            httpContext,
            StatusCodes.Status400BadRequest,
            "فشل التحقق من صحة البيانات.",
            "يرجى مراجعة الحقول غير الصحيحة والمحاولة مرة أخرى.",
            "Validation.Failed",
            ErrorType.Validation.ToString(),
            LocalizeValidationErrors(errors));

    public static ApiErrorResponse Validation(
        HttpContext httpContext,
        ModelStateDictionary modelState)
    {
        var errors = modelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value!.Errors
                    .Select(error =>
                        string.IsNullOrWhiteSpace(error.ErrorMessage)
                            ? "القيمة المرسلة غير صحيحة أو لا تطابق نوع الحقل المطلوب."
                            : error.ErrorMessage)
                    .ToArray(),
                StringComparer.Ordinal);

        return Validation(httpContext, errors);
    }

    public static ApiErrorResponse Unexpected(HttpContext httpContext) =>
        Create(
            httpContext,
            StatusCodes.Status500InternalServerError,
            "حدث خطأ غير متوقع.",
            "حدث خطأ غير متوقع أثناء معالجة الطلب.",
            "Server.UnexpectedError",
            ErrorType.Failure.ToString());

    public static ApiErrorResponse DatabaseUnavailable(HttpContext httpContext) =>
        Create(
            httpContext,
            StatusCodes.Status503ServiceUnavailable,
            "الخدمة غير جاهزة مؤقتاً.",
            "الاتصال بقاعدة البيانات غير متاح حالياً. يرجى المحاولة مرة أخرى.",
            "Database.Unavailable",
            ErrorType.Failure.ToString());

    public static ApiErrorResponse FromStatusCode(
        HttpContext httpContext,
        int statusCode)
    {
        var definition = statusCode switch
        {
            StatusCodes.Status400BadRequest => (
                "بيانات الطلب غير صحيحة.",
                "تعذر معالجة الطلب المرسل.",
                "Request.BadRequest",
                ErrorType.Validation.ToString()),
            StatusCodes.Status401Unauthorized => (
                "يجب تسجيل الدخول أولًا.",
                "رمز الوصول مفقود أو غير صالح أو منتهي الصلاحية.",
                "Authentication.Unauthorized",
                ErrorType.Unauthorized.ToString()),
            StatusCodes.Status403Forbidden => (
                "غير مسموح بتنفيذ هذا الإجراء.",
                "ليس لديك الصلاحية المطلوبة لتنفيذ هذا الإجراء.",
                "Authorization.Forbidden",
                ErrorType.Forbidden.ToString()),
            StatusCodes.Status404NotFound => (
                "المسار المطلوب غير موجود.",
                "لم يتم العثور على مسار الطلب المطلوب.",
                "Request.RouteNotFound",
                ErrorType.NotFound.ToString()),
            StatusCodes.Status405MethodNotAllowed => (
                "طريقة الطلب غير مسموحة.",
                "طريقة HTTP المستخدمة غير مدعومة لهذا المسار.",
                "Request.MethodNotAllowed",
                ErrorType.Failure.ToString()),
            StatusCodes.Status409Conflict => (
                "يوجد تعارض في البيانات.",
                "تعذر إكمال الطلب بسبب تعارض في البيانات.",
                "Request.Conflict",
                ErrorType.Conflict.ToString()),
            StatusCodes.Status415UnsupportedMediaType => (
                "نوع محتوى الطلب غير مدعوم.",
                "يجب إرسال محتوى الطلب بصيغة JSON مدعومة.",
                "Request.UnsupportedMediaType",
                ErrorType.Validation.ToString()),
            _ => (
                "تعذر إكمال الطلب.",
                "حدث خطأ أثناء معالجة الطلب.",
                $"Http.Status{statusCode}",
                ErrorType.Failure.ToString())
        };

        return Create(
            httpContext,
            statusCode,
            definition.Item1,
            definition.Item2,
            definition.Item3,
            definition.Item4);
    }

    public static ObjectResult ToObjectResult(ApiErrorResponse response)
    {
        var result = new ObjectResult(response)
        {
            StatusCode = response.Status
        };
        result.ContentTypes.Add(ProblemContentType);
        return result;
    }

    public static async Task WriteAsync(
        HttpContext httpContext,
        ApiErrorResponse response,
        CancellationToken cancellationToken = default)
    {
        httpContext.Response.StatusCode = response.Status;
        await httpContext.Response.WriteAsJsonAsync(
            response,
            options: null,
            contentType: ProblemContentType,
            cancellationToken);
    }

    private static ApiErrorResponse Create(
        HttpContext httpContext,
        int statusCode,
        string title,
        string detail,
        string errorCode,
        string errorType,
        IReadOnlyDictionary<string, string[]>? errors = null) =>
        new()
        {
            Type = GetProblemType(statusCode),
            Title = title,
            Status = statusCode,
            Detail = detail,
            Instance = GetInstance(httpContext),
            ErrorCode = errorCode,
            ErrorType = errorType,
            Errors = errors ?? new Dictionary<string, string[]>
            {
                ["General"] = [detail]
            },
            TraceId = Activity.Current?.Id ?? httpContext.TraceIdentifier
        };

    private static IReadOnlyDictionary<string, string[]> LocalizeValidationErrors(
        IEnumerable<KeyValuePair<string, string[]>> errors) =>
        errors.ToDictionary(
            entry => entry.Key,
            entry => entry.Value
                .Select(LocalizeValidationMessage)
                .ToArray(),
            StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, string[]> MapErrors(
        IEnumerable<Error> errors) =>
        errors
            .GroupBy(
                error => string.IsNullOrWhiteSpace(error.FieldName)
                    ? "General"
                    : error.FieldName!,
                StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(error => error.Description)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);

    private static string LocalizeValidationMessage(string message) =>
        message.Any(character =>
            character is >= 'A' and <= 'Z' or >= 'a' and <= 'z')
                ? "القيمة المرسلة غير صحيحة أو لا تطابق نوع الحقل المطلوب."
                : message;

    private static string GetInstance(HttpContext httpContext) =>
        $"{httpContext.Request.PathBase}{httpContext.Request.Path}";

    private static string GetProblemType(int statusCode) =>
        statusCode switch
        {
            StatusCodes.Status400BadRequest =>
                "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            StatusCodes.Status401Unauthorized =>
                "https://tools.ietf.org/html/rfc9110#section-15.5.2",
            StatusCodes.Status403Forbidden =>
                "https://tools.ietf.org/html/rfc9110#section-15.5.4",
            StatusCodes.Status404NotFound =>
                "https://tools.ietf.org/html/rfc9110#section-15.5.5",
            StatusCodes.Status405MethodNotAllowed =>
                "https://tools.ietf.org/html/rfc9110#section-15.5.6",
            StatusCodes.Status409Conflict =>
                "https://tools.ietf.org/html/rfc9110#section-15.5.10",
            StatusCodes.Status415UnsupportedMediaType =>
                "https://tools.ietf.org/html/rfc9110#section-15.5.16",
            StatusCodes.Status502BadGateway =>
                "https://tools.ietf.org/html/rfc9110#section-15.6.3",
            StatusCodes.Status504GatewayTimeout =>
                "https://tools.ietf.org/html/rfc9110#section-15.6.5",
            StatusCodes.Status500InternalServerError =>
                "https://tools.ietf.org/html/rfc9110#section-15.6.1",
            StatusCodes.Status503ServiceUnavailable =>
                "https://tools.ietf.org/html/rfc9110#section-15.6.4",
            _ => "about:blank"
        };
}
