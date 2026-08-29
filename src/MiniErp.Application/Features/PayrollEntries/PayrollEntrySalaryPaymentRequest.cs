namespace MiniErp.Application.Features.PayrollEntries;

public sealed record PayrollEntrySalaryPaymentRequest(
    DateOnly PostingDate,
    string? Notes = null);

public sealed record BulkPayrollEntrySalaryPaymentRequest(
    List<IndividualPayrollEntrySalaryPaymentRequest>? Entries = null,
    List<int>? PayrollEntryIds = null,
    DateOnly? DefaultPostingDate = null,
    string? Notes = null);

public sealed record IndividualPayrollEntrySalaryPaymentRequest(
    int PayrollEntryId,
    DateOnly? PostingDate = null,
    string? Notes = null);
