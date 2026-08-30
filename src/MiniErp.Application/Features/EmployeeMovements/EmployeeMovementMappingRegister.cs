using Mapster;
using MiniErp.Domain.Entities.Employees;

namespace MiniErp.Application.Features.EmployeeMovements;

public sealed class EmployeeMovementMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<EmployeeMovement, EmployeeMovementResponse>()
            .Map(
                response => response.EmployeeCode,
                movement => movement.Employee.Code)
            .Map(
                response => response.EmployeeName,
                movement => movement.Employee.Name)
            .Map(
                response => response.Amount,
                movement => movement.Debit + movement.Credit)
            .Map(
                response => response.CashVoucherNumber,
                movement => movement.CashVoucher == null
                    ? null
                    : movement.CashVoucher.VoucherNumber);
    }
}
