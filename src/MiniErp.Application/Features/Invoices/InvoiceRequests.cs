using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.Invoices;

public sealed record InvoiceLineRequest(
    int ItemId,
    int Count,
    decimal Weight,
    decimal Price,
    string? Notes,
    int? SourceInvoiceLineId = null,
    decimal? ReturnUnitCost = null);

public sealed record InvoiceContainerLineRequest(
    int ContainerId,
    int OutgoingUnits,
    int IncomingUnits);

public sealed record InvoiceRequest(
    string InvoiceNumber,
    InvoiceType InvoiceType,
    PaymentTerm PaymentTerm,
    DateOnly InvoiceDate,
    DateOnly? DueDate,
    int BusinessPartnerId,
    int StoreId,
    int? ContainerStoreId,
    int? CountryId,
    int? DriverId,
    int? ActualDriverId,
    bool UsesExternalDriver,
    string? ExternalDriverName,
    string? VehicleNumber,
    string? ExportInvoiceCode,
    decimal DiscountAmount,
    decimal PaidAmount,
    string? Notes,
    IReadOnlyList<InvoiceLineRequest> Lines,
    IReadOnlyList<InvoiceContainerLineRequest> ContainerLines,
    string? PartnerInvoiceNo = null,
    int? CashboxId = null,
    int? CashMovementTypeId = null,
    InvoiceContentType ContentType = InvoiceContentType.Items)
{
    public const int InvoiceNumberMaximumLength = 100;

    public const int PartnerInvoiceNoMaximumLength = 100;

    public const int ExportInvoiceCodeMaximumLength = 100;

    public const int ExternalDriverNameMaximumLength = 200;

    public const int VehicleNumberMaximumLength = 100;

    public const int NotesMaximumLength = 1_000;

    public const int MaximumLineCount = 100;

    public const int MaximumContainerLineCount = 100;
}

public sealed record InvoiceUpdateRequest(
    InvoiceType InvoiceType,
    PaymentTerm PaymentTerm,
    DateOnly InvoiceDate,
    DateOnly? DueDate,
    int BusinessPartnerId,
    int StoreId,
    int? ContainerStoreId,
    int? CountryId,
    int? DriverId,
    int? ActualDriverId,
    bool UsesExternalDriver,
    string? ExternalDriverName,
    string? VehicleNumber,
    string? ExportInvoiceCode,
    decimal DiscountAmount,
    decimal PaidAmount,
    string? Notes,
    IReadOnlyList<InvoiceLineRequest> Lines,
    IReadOnlyList<InvoiceContainerLineRequest> ContainerLines,
    byte[]? RowVersion,
    string? PartnerInvoiceNo = null,
    int? CashboxId = null,
    int? CashMovementTypeId = null,
    InvoiceContentType ContentType = InvoiceContentType.Items);
