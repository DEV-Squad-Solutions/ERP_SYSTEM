using MiniErp.Application.Common.Results;

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
}
