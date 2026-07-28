using Microsoft.EntityFrameworkCore;
using MiniErp.Domain.Entities.BusinessPartners;
using MiniErp.Domain.Entities.Containers;
using MiniErp.Domain.Entities.Invoicing;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Entities.Logistics;

namespace MiniErp.Infrastructure.Services.Invoices;

public sealed partial class InvoiceService
{
    private async Task SaveSideEffectsAsync(
        Invoice invoice,
        CancellationToken cancellationToken)
    {
        var itemMovementType =
            InvoiceMovementRules.GetItemMovementType(invoice.InvoiceType);
        var inbound = InvoiceMovementRules.IsInbound(invoice.InvoiceType);

        foreach (var line in invoice.Lines.Where(line => !line.IsDeleted))
        {
            dbContext.ItemMovements.Add(
                new ItemMovement
                {
                    CompanyId = companyId,
                    StoreId = invoice.StoreId,
                    ItemId = line.ItemId,
                    ItemUnitId = line.ItemUnitId,
                    MovementType = itemMovementType,
                    ReferenceId = invoice.Id,
                    ReferenceNumber = invoice.InvoiceNumber,
                    MovementDate = invoice.InvoiceDate,
                    QuantityIn = inbound ? line.Quantity : 0m,
                    QuantityOut = inbound ? 0m : line.Quantity,
                    Description = $"Invoice {invoice.InvoiceNumber}"
                });
        }

        if (invoice.ContainerStoreId.HasValue)
        {
            foreach (var line in invoice.ContainerLines.Where(
                         line => !line.IsDeleted))
            {
                dbContext.ContainerMovements.Add(
                    new ContainerMovement
                    {
                        CompanyId = companyId,
                        BusinessPartnerId = invoice.BusinessPartnerId,
                        ContainerStoreId = invoice.ContainerStoreId.Value,
                        ContainerId = line.ContainerId,
                        InvoiceId = invoice.Id,
                        InvoiceNumber = invoice.InvoiceNumber,
                        MovementDate = invoice.InvoiceDate,
                        OutgoingUnits = line.OutgoingUnits,
                        IncomingUnits = line.IncomingUnits,
                        Description = $"Invoice {invoice.InvoiceNumber}"
                    });
            }
        }

        if (InvoiceMovementRules.ShouldCreatePartnerMovement(
                invoice.RemainingAmount))
        {
            var partnerMovementType =
                InvoiceMovementRules.GetPartnerMovementType(
                    invoice.InvoiceType);
            var (debit, credit) = InvoiceMovementRules.GetPartnerAmounts(
                    invoice.InvoiceType,
                    invoice.RemainingAmount);

            dbContext.BusinessPartnerMovements.Add(
                new BusinessPartnerMovement
                {
                    CompanyId = companyId,
                    BusinessPartnerId = invoice.BusinessPartnerId,
                    InvoiceId = invoice.Id,
                    MovementType = partnerMovementType,
                    MovementDate = invoice.InvoiceDate,
                    Currency = invoice.Currency,
                    Debit = debit,
                    Credit = credit,
                    Description = $"Invoice {invoice.InvoiceNumber}"
                });
        }

        if (invoice.DriverId.HasValue)
        {
            dbContext.DriverTrips.Add(
                new DriverTrip
                {
                    CompanyId = companyId,
                    DriverId = invoice.DriverId.Value,
                    ActualDriverId = invoice.UsesExternalDriver
                        ? null
                        : invoice.ActualDriverId,
                    InvoiceId = invoice.Id,
                    BusinessPartnerId = invoice.BusinessPartnerId,
                    InvoiceNumber = invoice.InvoiceNumber,
                    ExportInvoiceCode = invoice.ExportInvoiceCode,
                    TripDate = invoice.InvoiceDate
                });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task RemoveSideEffectsAsync(
        Invoice invoice,
        CancellationToken cancellationToken)
    {
        var itemMovements = await dbContext.ItemMovements
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.ReferenceId == invoice.Id &&
                movement.ReferenceNumber == invoice.InvoiceNumber)
            .ToListAsync(cancellationToken);
        var containerMovements = await dbContext.ContainerMovements
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.InvoiceId == invoice.Id)
            .ToListAsync(cancellationToken);
        var partnerMovements = await dbContext.BusinessPartnerMovements
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.InvoiceId == invoice.Id)
            .ToListAsync(cancellationToken);
        var driverTrips = await dbContext.DriverTrips
            .Where(trip =>
                trip.CompanyId == companyId &&
                trip.InvoiceId == invoice.Id)
            .ToListAsync(cancellationToken);

        dbContext.ItemMovements.RemoveRange(itemMovements);
        dbContext.ContainerMovements.RemoveRange(containerMovements);
        dbContext.BusinessPartnerMovements.RemoveRange(partnerMovements);
        dbContext.DriverTrips.RemoveRange(driverTrips);
    }

    private async Task<bool> HasCashVoucherTripReferencesAsync(
        int invoiceId,
        CancellationToken cancellationToken)
    {
        var tripIds = dbContext.DriverTrips
            .IgnoreQueryFilters()
            .Where(trip =>
                trip.CompanyId == companyId &&
                trip.InvoiceId == invoiceId)
            .Select(trip => trip.Id);

        return await dbContext.CashVouchers
            .IgnoreQueryFilters()
            .AnyAsync(
                voucher =>
                    voucher.CompanyId == companyId &&
                    voucher.DriverTripId.HasValue &&
                    tripIds.Contains(voucher.DriverTripId.Value),
                cancellationToken);
    }
}
