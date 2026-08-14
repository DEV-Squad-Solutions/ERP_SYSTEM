using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Domain.Entities.BusinessPartners;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Entities.Containers;
using MiniErp.Domain.Entities.Invoicing;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Entities.Logistics;
using MiniErp.Domain.Enums;

namespace MiniErp.Infrastructure.Services.Invoices;

public sealed partial class InvoiceService
{
    private async Task SaveSideEffectsAsync(
        Invoice invoice,
        PaymentPreparation? paymentPreparation,
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
                invoice.Total))
        {
            var partnerMovementType =
                InvoiceMovementRules.GetPartnerMovementType(
                    invoice.InvoiceType);
            var (debit, credit) = InvoiceMovementRules.GetPartnerAmounts(
                invoice.InvoiceType,
                invoice.Total);

            var partnerMovement = new BusinessPartnerMovement
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
            };
            partnerMovement.ApplyExchangeRate(invoice.ExchangeRate);
            dbContext.BusinessPartnerMovements.Add(partnerMovement);
        }

        await SynchronizePaymentVoucherAsync(
            invoice,
            paymentPreparation,
            cancellationToken);

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

        var paymentVouchers = removeItemMovements
            ? await dbContext.CashVouchers
                .Where(voucher =>
                    voucher.CompanyId == companyId &&
                    voucher.InvoiceId == invoice.Id)
                .ToListAsync(cancellationToken)
            : [];
        var paymentVoucherIds = paymentVouchers
            .Select(voucher => voucher.Id)
            .ToArray();
        var invoicePayments = paymentVoucherIds.Length == 0
            ? []
            : await dbContext.InvoicePayments
                .Where(payment =>
                    payment.CompanyId == companyId &&
                    paymentVoucherIds.Contains(payment.CashVoucherId))
                .ToListAsync(cancellationToken);
        var paymentPartnerMovements = paymentVoucherIds.Length == 0
            ? []
            : await dbContext.BusinessPartnerMovements
                .Where(movement =>
                    movement.CompanyId == companyId &&
                    movement.CashVoucherId.HasValue &&
                    paymentVoucherIds.Contains(movement.CashVoucherId.Value))
                .ToListAsync(cancellationToken);

        dbContext.ItemMovements.RemoveRange(itemMovements);
        dbContext.ContainerMovements.RemoveRange(containerMovements);
        dbContext.BusinessPartnerMovements.RemoveRange(partnerMovements);
        dbContext.BusinessPartnerMovements.RemoveRange(paymentPartnerMovements);
        dbContext.InvoicePayments.RemoveRange(invoicePayments);
        dbContext.CashVouchers.RemoveRange(paymentVouchers);
        dbContext.DriverTrips.RemoveRange(driverTrips);
    }

    private async Task SynchronizePaymentVoucherAsync(
        Invoice invoice,
        PaymentPreparation? preparation,
        CancellationToken cancellationToken)
    {
        var voucher = await dbContext.CashVouchers
            .FirstOrDefaultAsync(candidate =>
                candidate.CompanyId == companyId &&
                candidate.InvoiceId == invoice.Id,
                cancellationToken);

        if (invoice.PaidAmount <= 0m)
        {
            if (voucher is not null)
            {
                var removedPayment = await dbContext.InvoicePayments
                    .FirstOrDefaultAsync(payment =>
                        payment.CompanyId == companyId &&
                        payment.CashVoucherId == voucher.Id,
                        cancellationToken);
                if (removedPayment is not null)
                {
                    dbContext.InvoicePayments.Remove(removedPayment);
                }

                var partnerMovement = await dbContext
                    .BusinessPartnerMovements
                    .FirstOrDefaultAsync(movement =>
                        movement.CompanyId == companyId &&
                        movement.CashVoucherId == voucher.Id,
                        cancellationToken);
                if (partnerMovement is not null)
                {
                    dbContext.BusinessPartnerMovements.Remove(partnerMovement);
                }

                dbContext.CashVouchers.Remove(voucher);
            }

            return;
        }

        if (preparation is null)
        {
            return;
        }

        var direction = InvoiceMovementRules.GetPaymentDirection(
            invoice.InvoiceType);
        if (voucher is null)
        {
            var voucherPrefix = direction == CashDirection.Receipt
                ? "RCV"
                : "PAY";
            var voucherNumber = await EntityIdentifierGenerator
                .GenerateUniqueAsync(
                    dbContext,
                    voucherPrefix,
                    companyId,
                    dbContext.CashVouchers
                        .IgnoreQueryFilters()
                        .Where(entity => entity.CompanyId == companyId)
                        .Select(entity => entity.VoucherNumber),
                    cancellationToken);
            voucher = new CashVoucher
            {
                CompanyId = companyId,
                InvoiceId = invoice.Id,
                VoucherNumber = voucherNumber,
                VoucherDate = invoice.InvoiceDate,
                Direction = direction,
                CashboxId = preparation.CashboxId,
                CashMovementTypeId = preparation.CashMovementTypeId,
                PartyType = CashPartyType.Partner,
                BusinessPartnerId = invoice.BusinessPartnerId,
                Amount = preparation.CashboxAmount,
                Currency = preparation.CashboxCurrency,
                ReferenceNumber = invoice.InvoiceNumber,
                Description = $"دفعة الفاتورة {invoice.InvoiceNumber}"
            };
            voucher.ApplyExchangeRate(
                preparation.ExchangeRateId,
                preparation.ExchangeRate);
            voucher.Touch(timeProvider.GetUtcNow().UtcDateTime);
            dbContext.CashVouchers.Add(voucher);
        }
        else
        {
            voucher.VoucherDate = invoice.InvoiceDate;
            voucher.Direction = direction;
            voucher.CashboxId = preparation.CashboxId;
            voucher.CashMovementTypeId =
                preparation.CashMovementTypeId;
            voucher.PartyType = CashPartyType.Partner;
            voucher.BusinessPartnerId = invoice.BusinessPartnerId;
            voucher.DriverId = null;
            voucher.DriverTripId = null;
            voucher.ExternalPartyName = null;
            voucher.Amount = preparation.CashboxAmount;
            voucher.Currency = preparation.CashboxCurrency;
            voucher.ApplyExchangeRate(
                preparation.ExchangeRateId,
                preparation.ExchangeRate);
            voucher.ReferenceNumber = invoice.InvoiceNumber;
            voucher.Description = $"دفعة الفاتورة {invoice.InvoiceNumber}";
            voucher.Notes = null;
            voucher.Touch(timeProvider.GetUtcNow().UtcDateTime);
            dbContext.Entry(voucher)
                .Property(entity => entity.LastModifiedAt)
                .IsModified = true;
        }

        var paymentMovement = await dbContext.BusinessPartnerMovements
            .FirstOrDefaultAsync(movement =>
                movement.CompanyId == companyId &&
                movement.CashVoucherId == voucher.Id,
                cancellationToken);

        var debit = direction == CashDirection.Payment
            ? invoice.PaidAmount
            : 0m;
        var credit = direction == CashDirection.Receipt
            ? invoice.PaidAmount
            : 0m;
        if (paymentMovement is null)
        {
            paymentMovement = new BusinessPartnerMovement
            {
                CompanyId = companyId,
                BusinessPartnerId = invoice.BusinessPartnerId,
                CashVoucher = voucher,
                MovementType = direction == CashDirection.Receipt
                        ? BusinessPartnerMovementType.CashReceipt
                        : BusinessPartnerMovementType.CashPayment,
                MovementDate = invoice.InvoiceDate,
                Currency = invoice.Currency,
                Debit = debit,
                Credit = credit,
                Description = $"دفعة الفاتورة {invoice.InvoiceNumber}"
            };
            paymentMovement.ApplyExchangeRate(invoice.ExchangeRate);
            dbContext.BusinessPartnerMovements.Add(paymentMovement);
        }
        else
        {
            paymentMovement.BusinessPartnerId = invoice.BusinessPartnerId;
            paymentMovement.MovementType =
                direction == CashDirection.Receipt
                    ? BusinessPartnerMovementType.CashReceipt
                    : BusinessPartnerMovementType.CashPayment;
            paymentMovement.MovementDate = invoice.InvoiceDate;
            paymentMovement.Currency = invoice.Currency;
            paymentMovement.Debit = debit;
            paymentMovement.Credit = credit;
            paymentMovement.ApplyExchangeRate(invoice.ExchangeRate);
            paymentMovement.Description =
                $"دفعة الفاتورة {invoice.InvoiceNumber}";
        }

        var invoicePayment = await dbContext.InvoicePayments
            .FirstOrDefaultAsync(
                payment =>
                    payment.CompanyId == companyId &&
                    payment.CashVoucherId == voucher.Id,
                cancellationToken);
        if (invoicePayment is null)
        {
            invoicePayment = new InvoicePayment
            {
                CompanyId = companyId,
                Invoice = invoice,
                CashVoucher = voucher
            };
            dbContext.InvoicePayments.Add(invoicePayment);
        }

        invoicePayment.Apply(
            invoice.Currency,
            invoice.PaidAmount,
            preparation.CashboxCurrency,
            preparation.CashboxAmount,
            invoice.ExchangeRate,
            preparation.ExchangeRate);
    }

    private async Task ReconcileItemMovementsAsync(
        Invoice invoice,
        CancellationToken cancellationToken)
    {
        var existingMovements = await LoadItemMovementsAsync(
            invoice.Id,
            cancellationToken);

        if (invoice.ContentType == InvoiceContentType.Containers)
        {
            dbContext.ItemMovements.RemoveRange(existingMovements);
            return;
        }

        var activeLines = invoice.Lines
            .Where(line => !line.IsDeleted && line.ItemId.HasValue)
            .ToDictionary(line => line.ItemId!.Value);
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

            existingItemIds.Add(line.ItemId!.Value);
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
                     !existingItemIds.Contains(line.ItemId!.Value)))
        {
            dbContext.ItemMovements.Add(
                new ItemMovement
                {
                    CompanyId = companyId,
                    StoreId = invoice.StoreId,
                    ItemId = line.ItemId!.Value,
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
            .Where(line => !line.IsDeleted && line.ItemId.HasValue)
            .Select(line => new InventoryCostingKey(
                invoice.StoreId,
                line.ItemId!.Value))
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

    private sealed record PaymentPreparation(
        int CashboxId,
        int CashMovementTypeId,
        CurrencyCode CashboxCurrency,
        int? ExchangeRateId,
        decimal ExchangeRate,
        decimal CashboxAmount);
}
