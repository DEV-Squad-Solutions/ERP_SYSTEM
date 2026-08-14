using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Common.Models;

public static class PaginationErrors
{
    public static Error Invalid() =>
        Error.Validation(
            "Pagination.Invalid",
            $"يجب أن يكون رقم الصفحة أكبر من صفر، وأن يكون حجم الصفحة بين 1 و{PaginationRequest.MaxPageSize}.");
}
