using MiniErp.Application.Common.Results;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.Statements;

public static class StatementErrors
{
    public static Error CashboxNotFound(int id) =>
        Error.NotFound(
            "Statements.CashboxNotFound",
            $"لم يتم العثور على صندوق النقدية رقم {id}.");

    public static Error PartnerNotFound(int id) =>
        Error.NotFound(
            "Statements.PartnerNotFound",
            $"لم يتم العثور على العميل أو المورد رقم {id}.");

    public static Error DriverNotFound(int id) =>
        Error.NotFound(
            "Statements.DriverNotFound",
            $"لم يتم العثور على السائق رقم {id}.");

    public static Error ContainerStorePartnerNotFound(int id) =>
        Error.NotFound(
            "Statements.ContainerStorePartnerNotFound",
            $"لم يتم العثور على عميل مخزن العبوات رقم {id}.");

    public static Error ContainerStoreNotFound(int businessPartnerId) =>
        Error.NotFound(
            "Statements.ContainerStoreNotFound",
            $"لا يوجد مخزن عبوات نشط للعميل رقم {businessPartnerId}.");

    public static Error EmployeeNotFound(int id) =>
        Error.NotFound(
            "Statements.EmployeeNotFound",
            $"لم يتم العثور على الموظف رقم {id}.");

    public static Error FiscalYearNotFound(int? id) =>
        Error.NotFound(
            "Statements.FiscalYearNotFound",
            id.HasValue
                ? $"لم يتم العثور على السنة المالية رقم {id.Value} أو لا تغطي الفترة المطلوبة."
                : "لم يتم العثور على سنة مالية تغطي الفترة المطلوبة.");

    public static Error InvalidStatementType(FinancialStatementType type) =>
        Error.Validation(
            "Statements.InvalidStatementType",
            $"نوع القائمة المالية {type} غير مدعوم.");
}
