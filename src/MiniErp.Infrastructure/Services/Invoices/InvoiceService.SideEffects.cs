using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
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
        await ReconcileItemMovementsAsync(invoice, cancellationToken);

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
        bool removeItemMovements,
        CancellationToken cancellationToken)
    {
        var itemMovements = removeItemMovements
            ? await LoadItemMovementsAsync(invoice.Id, cancellationToken)
            : [];
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

    private async Task ReconcileItemMovementsAsync(
        Invoice invoice,
        CancellationToken cancellationToken)
    {
        var existingMovements = await LoadItemMovementsAsync(
            invoice.Id,
            cancellationToken);
        var activeLines = invoice.Lines
            .Where(line => !line.IsDeleted)
            .ToDictionary(line => line.ItemId);
        var existingItemIds = new HashSet<int>();
        var movementType =
            InvoiceMovementRules.GetItemMovementType(invoice.InvoiceType);
        var inbound = InvoiceMovementRules.IsInbound(invoice.InvoiceType);

        foreach (var movement in existingMovements)
        {
            if (!activeLines.TryGetValue(movement.ItemId, out var line))
            {
                dbContext.ItemMovements.Remove(movement);
                continue;
            }

            existingItemIds.Add(line.ItemId);
            movement.StoreId = invoice.StoreId;
            movement.ItemUnitId = line.ItemUnitId;
            movement.MovementType = movementType;
            movement.ReferenceNumber = invoice.InvoiceNumber;
            movement.MovementDate = invoice.InvoiceDate;
            movement.QuantityIn = inbound ? line.Quantity : 0m;
            movement.QuantityOut = inbound ? 0m : line.Quantity;
            movement.Description = $"Invoice {invoice.InvoiceNumber}";
        }

        foreach (var line in activeLines.Values.Where(line =>
                     !existingItemIds.Contains(line.ItemId)))
        {
            dbContext.ItemMovements.Add(
                new ItemMovement
                {
                    CompanyId = companyId,
                    StoreId = invoice.StoreId,
                    ItemId = line.ItemId,
                    ItemUnitId = line.ItemUnitId,
                    MovementType = movementType,
                    ReferenceId = invoice.Id,
                    ReferenceNumber = invoice.InvoiceNumber,
                    MovementDate = invoice.InvoiceDate,
                    QuantityIn = inbound ? line.Quantity : 0m,
                    QuantityOut = inbound ? 0m : line.Quantity,
                    Description = $"Invoice {invoice.InvoiceNumber}"
                });
        }
    }

    private Task<List<ItemMovement>> LoadItemMovementsAsync(
        int invoiceId,
        CancellationToken cancellationToken)
    {
        var movementTypes = InvoiceItemMovementTypes;
        return dbContext.ItemMovements
            .Where(movement =>
                movement.CompanyId == companyId &&
                movementTypes.Contains(movement.MovementType) &&
                movement.ReferenceId == invoiceId)
            .ToListAsync(cancellationToken);
    }

    private static IReadOnlyCollection<InventoryCostingKey> GetCostingKeys(
        Invoice invoice) =>
        invoice.Lines
            .Where(line => !line.IsDeleted)
            .Select(line => new InventoryCostingKey(
                invoice.StoreId,
                line.ItemId))
            .Distinct()
            .ToArray();

    private static IReadOnlyCollection<InventoryCostingKey> GetCostingKeys(
        IEnumerable<ItemMovement> movements) =>
        movements
            .Select(movement => new InventoryCostingKey(
                movement.StoreId,
                movement.ItemId))
            .Distinct()
            .ToArray();

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
