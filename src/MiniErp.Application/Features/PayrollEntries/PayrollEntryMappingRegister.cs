using Mapster;
using MiniErp.Domain.Entities.Payroll;

namespace MiniErp.Application.Features.PayrollEntries;

public sealed class PayrollEntryMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<PayrollEntry, PayrollEntryResponse>()
            .Map(dest => dest.Bonus, src => src.Bonus)
            .Map(dest => dest.Deduction, src => src.Deduction)
            .Map(dest => dest.AttendanceSummary, src => new AttendanceSummary(
                PresentDays: src.PresentDays,
                AbsentDays: src.AbsentDays,
                TotalPresentDays: src.WorkedDaysbydayunit,
                TotalOvertimeDays: src.Overtimebydayunit,
                TotalDeductionDays: src.Deductionbydayunit
            ));
    }
}
