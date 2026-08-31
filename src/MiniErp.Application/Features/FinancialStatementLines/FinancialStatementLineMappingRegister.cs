using Mapster;
using MiniErp.Domain.Entities.Accounting;

namespace MiniErp.Application.Features.FinancialStatementLines;

public sealed class FinancialStatementLineMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<FinancialStatementLineRequest, FinancialStatementLine>()
            .Ignore(line => line.Id)
            .Ignore(line => line.CompanyId)
            .Ignore(line => line.Company)
            .Ignore(line => line.FiscalYear)
            .Ignore(line => line.ParentLine)
            .Ignore(line => line.Children)
            .Ignore(line => line.AccountMappings)
            .Ignore(line => line.RowVersion)
            .Map(line => line.Code, request => request.Code.Trim())
            .Map(line => line.Name, request => request.Name.Trim());

        config.ForType<FinancialStatementLineUpdateRequest, FinancialStatementLine>()
            .Ignore(line => line.Id)
            .Ignore(line => line.CompanyId)
            .Ignore(line => line.Company)
            .Ignore(line => line.FiscalYear)
            .Ignore(line => line.ParentLine)
            .Ignore(line => line.Children)
            .Ignore(line => line.AccountMappings)
            .Ignore(line => line.RowVersion)
            .Map(line => line.Code, request => request.Code.Trim())
            .Map(line => line.Name, request => request.Name.Trim());

        config.ForType<FinancialStatementLine, FinancialStatementLineResponse>()
            .Map(response => response.FiscalYearName,
                line => line.FiscalYear.Name)
            .Map(response => response.ParentLineCode,
                line => line.ParentLine == null ? null : line.ParentLine.Code)
            .Map(response => response.ParentLineName,
                line => line.ParentLine == null ? null : line.ParentLine.Name);

        config.ForType<FinancialStatementLine, FinancialStatementLineSelectResponse>();
    }
}
