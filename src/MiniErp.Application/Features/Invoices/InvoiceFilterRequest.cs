using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.Invoices;

public enum InvoicePriceStatus
{
    HasMissingPrice = 1,
    AllItemsPriced = 2
}

public sealed record InvoiceFilterRequest(
    string? Search = null,
    string? InvoiceNumber = null,
    InvoiceType? InvoiceType = null,
    int? BusinessPartnerId = null,
    int? CountryId = null,
    int? StoreId = null,
    int? DriverId = null,
    PaymentTerm? PaymentTerm = null,
    InvoicePriceStatus? PriceStatus = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null);
