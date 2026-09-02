using Mapster;
using MiniErp.Domain.Entities.Accounting;

namespace MiniErp.Application.Features.FiscalYears;

public sealed class FiscalYearMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<FiscalYearRequest, FiscalYear>()
            .Ignore(fiscalYear => fiscalYear.Id)
            .Ignore(fiscalYear => fiscalYear.CompanyId)
            .Ignore(fiscalYear => fiscalYear.Company)
            .Ignore(fiscalYear => fiscalYear.Status)
            .Ignore(fiscalYear => fiscalYear.ClosedOn)
            .Ignore(fiscalYear => fiscalYear.RowVersion)
            .Map(
                fiscalYear => fiscalYear.Name,
                request => request.Name.Trim());

        config.ForType<FiscalYearUpdateRequest, FiscalYear>()
            .Ignore(fiscalYear => fiscalYear.Id)
            .Ignore(fiscalYear => fiscalYear.CompanyId)
            .Ignore(fiscalYear => fiscalYear.Company)
            .Ignore(fiscalYear => fiscalYear.Status)
            .Ignore(fiscalYear => fiscalYear.ClosedOn)
            .Ignore(fiscalYear => fiscalYear.RowVersion)
            .Map(
                fiscalYear => fiscalYear.Name,
                request => request.Name.Trim());
    }
}
