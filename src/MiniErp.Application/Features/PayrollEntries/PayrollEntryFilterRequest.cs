namespace MiniErp.Application.Features.PayrollEntries
{
    public sealed record PayrollEntryFilterRequest(
        int? CompanyId = null,
        int? EmployeeId = null,
        DateOnly? StartDate = null,
        DateOnly? EndDate = null,
        string? Search = null);
   
}

