using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Features.Invoices;
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
        IReadOnlyList<InvoiceLineRequest> lines,
        IReadOnlyList<InvoiceContainerLineRequest> containerLines,
        PreparedInvoice preparation,
        CancellationToken cancellationToken)
    {
        var itemMovementType =
            InvoiceMovementRules.GetItemMovementType(invoice.InvoiceType);
        var inbound = InvoiceMovementRules.IsInbound(invoice.InvoiceType);

        foreach (var requestLine in lines)
        {
            if (!InvoiceAmountRules.TryCalculate(
                    requestLine.Count,
                    requestLine.Weight,
                    0m,
                    out var quantity,
                    out _))
            {
                throw new InvalidOperationException(
                    "The invoice line quantity cannot be calculated.");
            }

            dbContext.ItemMovements.Add(
                new ItemMovement
                {
                    CompanyId = companyId,
                    StoreId = invoice.StoreId,
                    ItemId = requestLine.ItemId,
                    ItemUnitId = preparation.ItemUnitIds[requestLine.ItemId],
                    MovementType = itemMovementType,
                    ReferenceId = invoice.Id,
                    ReferenceNumber = invoice.InvoiceNumber,
                    MovementDate = invoice.InvoiceDate,
                    QuantityIn = inbound ? quantity : 0m,
                    QuantityOut = inbound ? 0m : quantity,
                    Description = $"Invoice {invoice.InvoiceNumber}"
                });
        }

        if (invoice.ContainerStoreId.HasValue)
        {
            foreach (var requestLine in containerLines)
            {
                dbContext.ContainerMovements.Add(
                    new ContainerMovement
                    {
                        CompanyId = companyId,
                        BusinessPartnerId = invoice.BusinessPartnerId,
                        ContainerStoreId = invoice.ContainerStoreId.Value,
                        ContainerId = requestLine.ContainerId,
                        InvoiceId = invoice.Id,
                        InvoiceNumber = invoice.InvoiceNumber,
                        MovementDate = invoice.InvoiceDate,
                        OutgoingUnits = requestLine.OutgoingUnits,
                        IncomingUnits = requestLine.IncomingUnits,
                        Description = $"Invoice {invoice.InvoiceNumber}"
                    });
            }
        }

        if (InvoiceMovementRules.ShouldCreatePartnerMovement(
                invoice.PaymentTerm,
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
}
