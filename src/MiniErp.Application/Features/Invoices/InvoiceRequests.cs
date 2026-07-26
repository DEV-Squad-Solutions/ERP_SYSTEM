using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.Invoices;

public sealed record InvoiceLineRequest(
    int ItemId,
    int Count,
    decimal Weight,
    decimal Price,
    string? Notes);

public sealed record InvoiceContainerLineRequest(
    int ContainerId,
    int OutgoingUnits,
    int IncomingUnits);

public sealed record InvoiceRequest(
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
    IReadOnlyList<InvoiceContainerLineRequest> ContainerLines
    )
{
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
    byte[]? RowVersion);
