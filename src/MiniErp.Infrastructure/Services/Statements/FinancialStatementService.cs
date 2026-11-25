using Microsoft.EntityFrameworkCore;
using static MiniErp.Application.Features.Statements.StatementErrors;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Statements;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.Statements;

public sealed partial class FinancialStatementService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext,
    ICashboxAccessService? cashboxAccessService = null)
    : IFinancialStatementService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    private sealed class CashboxStatementRaw
    {
        public int JournalEntryLineId { get; init; }
        public int JournalEntryId { get; init; }
        public int? CashVoucherId { get; init; }
        public JournalEntrySourceType? SourceType { get; init; }
        public bool IsOpening { get; init; }
        public DateOnly Date { get; init; }
        public DateTime CreatedOn { get; init; }
        public string DocumentNumber { get; init; } = string.Empty;
        public string? MovementName { get; init; }
        public string? Description { get; init; }
        public string? PartyName { get; init; }
        public string? ExternalPartyName { get; init; }
        public CashPartyType? CashPartyType { get; init; }
        public int? BusinessPartnerId { get; init; }
        public int? DriverId { get; init; }
        public int? DriverTripId { get; init; }
        public int? EmployeeId { get; init; }
        public int? CashMovementTypeId { get; init; }
        public CashMovementClassification? Classification { get; init; }
        public CurrencyCode Currency { get; init; }
        public decimal ExchangeRate { get; init; }
        public decimal ReceiptAmount { get; init; }
        public decimal PaymentAmount { get; init; }
        public decimal BaseReceiptAmount { get; init; }
        public decimal BasePaymentAmount { get; init; }
        public string? ReferenceNumber { get; init; }
    }

    private sealed class PartnerStatementRaw
    {
        public int JournalEntryLineId { get; init; }
        public int JournalEntryId { get; init; }
        public int? CashVoucherId { get; init; }
        public int? CashboxId { get; init; }
        public string? CashboxName { get; init; }
        public CashDirection? CashDirection { get; init; }
        public bool IsOpening { get; init; }
        public int SourceId { get; init; }
        public PartnerStatementSourceType SourceType { get; init; }
        public JournalEntryType EntryType { get; init; }
        public DateOnly Date { get; init; }
        public DateTime CreatedOn { get; init; }
        public string DocumentNumber { get; init; } = string.Empty;
        public BusinessPartnerMovementType? MovementType { get; init; }
        public string? Description { get; init; }
        public decimal Debit { get; init; }
        public decimal Credit { get; init; }
        public decimal ExchangeRate { get; init; }
        public decimal BaseDebit { get; init; }
        public decimal BaseCredit { get; init; }
        public string? ReferenceNumber { get; init; }
        public int? CashMovementTypeId { get; init; }
        public CashMovementClassification? Classification { get; init; }
    }

    private sealed class DriverStatementRaw
    {
        public int JournalEntryLineId { get; init; }
        public int JournalEntryId { get; init; }
        public bool IsCarriedOpening { get; init; }
        public bool IsManualOpening { get; init; }
        public DriverStatementSourceType SourceType { get; init; }
        public DateOnly Date { get; init; }
        public DateTime CreatedOn { get; init; }
        public string DocumentNumber { get; init; } = string.Empty;
        public string? InvoiceNumber { get; init; }
        public int? DriverTripId { get; init; }
        public int? BusinessPartnerId { get; init; }
        public string? BusinessPartnerName { get; init; }
        public string? CountryName { get; init; }
        public string? MovementTypeName { get; init; }
        public string? Description { get; init; }
        public decimal Debit { get; init; }
        public decimal Credit { get; init; }
        public decimal CashPaid { get; init; }
        public decimal CashReceived { get; init; }
        public decimal TripCost { get; init; }
        public string? CashboxName { get; init; }
        public int? CashboxId { get; init; }
        public string? ReferenceNumber { get; init; }
        public CashDirection? Direction { get; init; }
        public int? CashMovementTypeId { get; init; }
        public CashMovementClassification? Classification { get; init; }
    }
}
