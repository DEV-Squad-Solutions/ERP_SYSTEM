using Microsoft.EntityFrameworkCore;
using static MiniErp.Application.Features.Invoices.InvoiceErrors;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Inventory;
using MiniErp.Application.Features.Invoices;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Entities.Invoicing;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Enums;

namespace MiniErp.Infrastructure.Services.Invoices;

public sealed partial class InvoiceService
{
    private static bool TryGetEffectiveLineValues(
        InvoiceLineRequest request,
        out int count,
        out decimal weight)
    {
        if (request.Count.GetValueOrDefault() <= 0 &&
            request.Weight.GetValueOrDefault() <= 0m &&
            request.Quantity.HasValue)
        {
            count = 1;
            weight = request.Quantity.Value;
            return weight > 0m &&
                InvoiceAmountRules.IsValidQuantity(weight);
        }

        if (!request.Count.HasValue || !request.Weight.HasValue)
        {
            count = 0;
            weight = 0m;
            return false;
        }

        count = request.Count.Value;
        weight = request.Weight.Value;
        return count > 0 &&
            weight > 0m &&
            InvoiceAmountRules.IsValidQuantity(weight);
    }

    private static Error? ValidateFilters(InvoiceFilterRequest filters)
    {
        if (filters.InvoiceNumber?.Trim().Length >
            InvoiceRequest.InvoiceNumberMaximumLength)
        {
            return InvoiceNumberFilterInvalid();
        }

        if (filters.InvoiceType.HasValue &&
            !Enum.IsDefined(
                typeof(InvoiceType),
                filters.InvoiceType.Value))
        {
            return InvoiceTypeInvalid(nameof(InvoiceFilterRequest.InvoiceType));
        }

        if (filters.PaymentTerm.HasValue &&
            !Enum.IsDefined(
                typeof(PaymentTerm),
                filters.PaymentTerm.Value))
        {
            return PaymentTermInvalid(nameof(InvoiceFilterRequest.PaymentTerm));
        }

        if (filters.PriceStatus.HasValue &&
            !Enum.IsDefined(
                typeof(InvoicePriceStatus),
                filters.PriceStatus.Value))
        {
            return InvalidFilter(InvoiceFilterErrorKind.PriceStatus);
        }

        if (filters.BusinessPartnerId is <= 0)
        {
            return InvalidFilter(InvoiceFilterErrorKind.BusinessPartnerId);
        }

        if (filters.CountryId is <= 0)
        {
            return InvalidFilter(InvoiceFilterErrorKind.CountryId);
        }

        if (filters.StoreId is <= 0)
        {
            return InvalidFilter(InvoiceFilterErrorKind.StoreId);
        }

        if (filters.DriverId is <= 0)
        {
            return InvalidFilter(InvoiceFilterErrorKind.DriverId);
        }

        if (filters.FromDate > filters.ToDate)
        {
            return InvalidFilter(InvoiceFilterErrorKind.DateRange);
        }

        return null;
    }

    private async Task<Result<PreparedInvoice>> PrepareAsync(
        Invoice invoice,
        IReadOnlyList<InvoiceLineRequest> lines,
        IReadOnlyList<InvoiceContainerLineRequest> containerLines,
        int? currentInvoiceId,
        string? currentInvoiceNumber,
        CancellationToken cancellationToken)
    {
        static Result<PreparedInvoice> Failure(Error error) =>
            Result<PreparedInvoice>.Failure(error);

        if (currentInvoiceId is null)
        {
            if (string.IsNullOrWhiteSpace(invoice.InvoiceNumber) ||
                invoice.InvoiceNumber.Length >
                InvoiceRequest.InvoiceNumberMaximumLength)
            {
                return Failure(
                    InvoiceNumberInvalid());
            }
        }

        if (!Enum.IsDefined(typeof(InvoiceType), invoice.InvoiceType))
        {
            return Failure(
                InvoiceTypeInvalid(nameof(InvoiceRequest.InvoiceType)));
        }

        if (!Enum.IsDefined(
                typeof(InvoiceContentType),
                invoice.ContentType))
        {
            return Failure(
                ContentTypeInvalid());
        }

        if (invoice.ContentType == InvoiceContentType.Containers &&
            lines.Count > 0)
        {
            return Failure(
                ItemLinesNotAllowedForContainerInvoice());
        }

        if (invoice.ContentType == InvoiceContentType.Containers &&
            containerLines.Count == 0)
        {
            return Failure(
                ContainerLinesRequired());
        }

        if (invoice.ContentType == InvoiceContentType.Items &&
            lines.Count == 0)
        {
            return Failure(
                ItemLinesRequired());
        }

        if (invoice.InvoiceType != InvoiceType.SalesReturn &&
            lines.Any(line =>
                line.SourceInvoiceLineId.HasValue ||
                line.ReturnUnitCost.HasValue))
        {
            return Failure(
                ReturnCostFieldsNotAllowed());
        }

        if (lines.Any(line => line.ReturnUnitCost < 0m))
        {
            return Failure(
                ReturnUnitCostInvalid());
        }

        if (invoice.InvoiceType == InvoiceType.SalesReturn)
        {
            var linkedLineIds = lines
                .Where(line => line.SourceInvoiceLineId.HasValue)
                .Select(line => line.SourceInvoiceLineId!.Value)
                .Distinct()
                .ToArray();
            if (linkedLineIds.Length > 0)
            {
                var validSources = await dbContext.InvoiceLines
                    .AsNoTracking()
                    .Where(source =>
                        source.CompanyId == companyId &&
                        linkedLineIds.Contains(source.Id) &&
                        source.Invoice.CompanyId == companyId &&
                        source.Invoice.InvoiceType == InvoiceType.Sales &&
                        source.Invoice.StoreId == invoice.StoreId)
                    .Select(source => new
                    {
                        source.Id,
                        source.ItemId
                    })
                    .ToListAsync(cancellationToken);
                var validById = validSources.ToDictionary(
                    source => source.Id,
                    source => source.ItemId);

                var invalidLinkedLines = lines.Where(line =>
                        line.SourceInvoiceLineId.HasValue &&
                        (!validById.TryGetValue(
                             line.SourceInvoiceLineId.Value,
                             out var sourceItemId) ||
                         sourceItemId != line.ItemId))
                    .Select(line => line.ItemId)
                    .ToArray();
                if (invalidLinkedLines.Length > 0)
                {
                    return Failure(
                        InvalidSalesReturnSource());
                }
            }
        }

        if (!Enum.IsDefined(typeof(PaymentTerm), invoice.PaymentTerm))
        {
            return Failure(
                PaymentTermInvalid(nameof(InvoiceRequest.PaymentTerm)));
        }

        if (lines.GroupBy(line => line.ItemId).Any(group => group.Count() > 1))
        {
            return Failure(
                DuplicateItemIds());
        }

        if (containerLines
            .GroupBy(line => line.ContainerId)
            .Any(group => group.Count() > 1))
        {
            return Failure(
                DuplicateContainerIds());
        }

        foreach (var line in lines)
        {
            if (!TryGetEffectiveLineValues(
                    line,
                    out var count,
                    out var weight))
            {
                return Failure(
                    InvalidCalculatedAmounts(InvoiceCalculationErrorKind.LineQuantityOrTotal));
            }

            if (!InvoiceAmountRules.TryCalculate(
                    count,
                    weight,
                    line.Price,
                    out _,
                    out _))
            {
                return Failure(
                    InvalidCalculatedAmounts(InvoiceCalculationErrorKind.LineQuantityOrTotal));
            }
        }

        var partner = await dbContext.BusinessPartners
            .AsNoTracking()
            .Where(candidate =>
                candidate.CompanyId == companyId &&
                candidate.Id == invoice.BusinessPartnerId)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.Currency,
                candidate.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (partner is null)
        {
            return Failure(
                BusinessPartnerNotFound(invoice.BusinessPartnerId));
        }

        if (!partner.IsActive)
        {
            return Failure(
                BusinessPartnerInactive());
        }

        var store = await dbContext.Stores
            .AsNoTracking()
            .Where(candidate =>
                candidate.CompanyId == companyId &&
                candidate.Id == invoice.StoreId)
            .Select(candidate => new
            {
                candidate.IsActive,
                candidate.IsContainerStore
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (store is null)
        {
            return Failure(
                StoreNotFound(invoice.StoreId));
        }

        if (!store.IsActive)
        {
            return Failure(
                StoreInactive());
        }

        if (invoice.ContentType == InvoiceContentType.Items &&
            store.IsContainerStore)
        {
            return Failure(
                ContainerStoreNotAllowed());
        }

        if (invoice.ContentType == InvoiceContentType.Containers &&
            !store.IsContainerStore)
        {
            return Failure(
                ContainerStoreRequired(InvoiceContainerStoreRequirement.ContainerInvoice));
        }

        if (invoice.ContentType == InvoiceContentType.Containers &&
            !invoice.ContainerStoreId.HasValue)
        {
            invoice.ContainerStoreId = invoice.StoreId;
        }

        Store? containerStore = null;
        if (invoice.ContainerStoreId is int containerStoreId)
        {
            containerStore = await dbContext.Stores
                .AsNoTracking()
                .FirstOrDefaultAsync(candidate =>
                    candidate.CompanyId == companyId &&
                    candidate.Id == containerStoreId,
                    cancellationToken);
            if (containerStore is null)
            {
                return Failure(
                    ContainerStoreNotFound(containerStoreId));
            }

            if (!containerStore.IsActive)
            {
                return Failure(
                    ContainerStoreInactive());
            }

            if (!containerStore.IsContainerStore ||
                containerStore.BusinessPartnerId != partner.Id)
            {
                return Failure(
                    ContainerStorePartnerMismatch());
            }
        }

        if (invoice.CountryId is int countryId)
        {
            var countryExists = await dbContext.Countries
                .AsNoTracking()
                .AnyAsync(
                    candidate =>
                        candidate.Id == countryId &&
                        candidate.IsActive,
                    cancellationToken);
            if (!countryExists)
            {
                return Failure(
                    CountryNotFound(countryId));
            }
        }

        if (invoice.ItemsCategoryId is int itemsCategoryId)
        {
            var category = await dbContext.ItemsCategories
                .AsNoTracking()
                .Where(candidate =>
                    candidate.CompanyId == companyId &&
                    candidate.Id == itemsCategoryId)
                .Select(candidate => new
                {
                    candidate.IsActive
                })
                .FirstOrDefaultAsync(cancellationToken);
            if (category is null)
            {
                return Failure(
                    ItemsCategoryNotFound(itemsCategoryId));
            }

            if (!category.IsActive)
            {
                var isExistingSelection =
                    currentInvoiceId.HasValue &&
                    await dbContext.Invoices
                        .AsNoTracking()
                        .AnyAsync(
                            candidate =>
                                candidate.CompanyId == companyId &&
                                candidate.Id == currentInvoiceId.Value &&
                                candidate.ItemsCategoryId ==
                                itemsCategoryId,
                            cancellationToken);
                if (!isExistingSelection)
                {
                    return Failure(
                        ItemsCategoryInactive());
                }
            }
        }

        NormalizeDriverValues(invoice);

        if (invoice.ActualDriverId.HasValue &&
            !invoice.DriverId.HasValue)
        {
            return Failure(
                MainDriverRequired());
        }

        if (invoice.UsesExternalDriver &&
            invoice.ActualDriverId.HasValue)
        {
            return Failure(
                ExternalDriverWithActualDriver());
        }

        if (invoice.UsesExternalDriver)
        {
            if (string.IsNullOrWhiteSpace(invoice.ExternalDriverName))
            {
                return Failure(
                    ExternalDriverNameRequired());
            }
        }

        if (invoice.DriverId is int driverId)
        {
            var driver = await dbContext.Drivers
                .AsNoTracking()
                .Where(candidate =>
                    candidate.CompanyId == companyId &&
                    candidate.Id == driverId)
                .Select(candidate => new
                {
                    candidate.IsActive
                })
                .FirstOrDefaultAsync(cancellationToken);
            if (driver is null)
            {
                return Failure(
                    DriverNotFound(driverId));
            }

            if (!driver.IsActive)
            {
                return Failure(
                    DriverInactive());
            }
        }

        if (invoice.ActualDriverId is int actualDriverId)
        {
            var actualDriver = await dbContext.Drivers
                .AsNoTracking()
                .Where(candidate =>
                    candidate.CompanyId == companyId &&
                    candidate.Id == actualDriverId)
                .Select(candidate => new
                {
                    candidate.IsActive
                })
                .FirstOrDefaultAsync(cancellationToken);
            if (actualDriver is null)
            {
                return Failure(
                    ActualDriverNotFound(actualDriverId));
            }

            if (!actualDriver.IsActive)
            {
                return Failure(
                    ActualDriverInactive());
            }
        }

        var itemIds = lines
            .Select(line => line.ItemId)
            .Distinct()
            .ToArray();
        var items = await dbContext.Items
            .AsNoTracking()
            .Where(item =>
                item.CompanyId == companyId &&
                itemIds.Contains(item.Id))
            .Select(item => new
            {
                item.Id,
                item.ItemUnitId,
                item.IsActive,
                ItemUnitIsActive = item.ItemUnit.IsActive
            })
            .ToListAsync(cancellationToken);
        var itemsById = items.ToDictionary(item => item.Id);
        var missingItemIds = itemIds
            .Except(itemsById.Keys)
            .ToArray();
        if (missingItemIds.Length > 0)
        {
            return Failure(
                ItemNotFound(missingItemIds));
        }

        var inactiveItemIds = items
            .Where(item => !item.IsActive)
            .Select(item => item.Id)
            .ToArray();
        if (inactiveItemIds.Length > 0)
        {
            return Failure(
                ItemInactive(inactiveItemIds));
        }

        var inactiveUnitItemIds = items
            .Where(item => !item.ItemUnitIsActive)
            .Select(item => item.Id)
            .ToArray();
        if (inactiveUnitItemIds.Length > 0)
        {
            return Failure(
                ItemUnitInactive(inactiveUnitItemIds));
        }

        if (containerLines.Count > 0)
        {
            if (invoice.InvoiceType is not (InvoiceType.Sales or
                InvoiceType.SalesReturn))
            {
                return Failure(
                    ContainerLinesNotAllowed());
            }

            if (containerStore is null)
            {
                return Failure(
                    ContainerStoreRequired(InvoiceContainerStoreRequirement.ContainerLines));
            }

            var containerIds = containerLines
                .Select(line => line.ContainerId)
                .Distinct()
                .ToArray();
            var assignedIds = await dbContext.StoreContainers
                .AsNoTracking()
                .Where(assignment =>
                    assignment.CompanyId == companyId &&
                    assignment.StoreId == containerStore.Id &&
                    assignment.IsActive &&
                    containerIds.Contains(assignment.ContainerId) &&
                    assignment.Container.IsActive)
                .Select(assignment => assignment.ContainerId)
                .ToListAsync(cancellationToken);
            var missingContainerIds = containerIds
                .Except(assignedIds)
                .ToArray();
            if (missingContainerIds.Length > 0)
            {
                return Failure(
                    ContainerNotAssigned(missingContainerIds));
            }
        }

        var stockError = await ValidateStockAsync(
            invoice,
            lines,
            currentInvoiceId,
            currentInvoiceNumber,
            cancellationToken);
        if (stockError is not null)
        {
            return Failure(stockError);
        }

        return Result<PreparedInvoice>.Success(
            new PreparedInvoice(
                partner.Currency,
                items.ToDictionary(
                    item => item.Id,
                    item => item.ItemUnitId)));
    }

    private static Error? ValidateAmounts(Invoice invoice)
    {
        if (!InvoiceAmountRules.IsValidQuantity(invoice.WBWeight) ||
            !InvoiceAmountRules.IsValidQuantity(
                invoice.WBScaleDifference) ||
            !InvoiceAmountRules.IsValidQuantity(invoice.WBDiscount) ||
            !InvoiceAmountRules.IsValidQuantity(invoice.WBTotal))
        {
            return InvalidWBTotal();
        }

        if (invoice.DiscountAmount < 0m ||
            !InvoiceAmountRules.IsValidMoney(invoice.DiscountAmount) ||
            invoice.DiscountAmount > invoice.Subtotal)
        {
            return InvalidDiscountAmount();
        }

        if (!InvoiceAmountRules.IsValidMoney(invoice.Subtotal) ||
            !InvoiceAmountRules.IsValidMoney(invoice.Total))
        {
            return InvalidCalculatedAmounts(InvoiceCalculationErrorKind.Totals);
        }

        if (invoice.PaidAmount < 0m ||
            !InvoiceAmountRules.IsValidMoney(invoice.PaidAmount) ||
            invoice.PaidAmount > invoice.Total)
        {
            return InvalidPaidAmount();
        }

        return null;
    }

    private async Task<Result<PaymentPreparation?>> PreparePaymentAsync(
        Invoice invoice,
        int? cashboxId,
        int? cashMovementTypeId,
        decimal? requestedCashboxExchangeRate,
        int? currentInvoiceId,
        CancellationToken cancellationToken)
    {
        var currentVoucher = currentInvoiceId is int invoiceId
            ? await dbContext.CashVouchers
                .FirstOrDefaultAsync(voucher =>
                    voucher.CompanyId == companyId &&
                    voucher.InvoiceId == invoiceId,
                    cancellationToken)
            : null;

        if (invoice.PaymentTerm == PaymentTerm.Cash &&
            invoice.PaidAmount != invoice.Total)
        {
            return Result<PaymentPreparation?>.Failure(
                CashInvoiceMustBeFullyPaid());
        }

        if (invoice.PaymentTerm == PaymentTerm.Credit &&
            invoice.Total > 0m &&
            invoice.PaidAmount >= invoice.Total)
        {
            return Result<PaymentPreparation?>.Failure(
                CreditInvoiceCannotBeFullyPaid());
        }

        if (invoice.PaidAmount <= 0m)
        {
            if (cashboxId.HasValue || cashMovementTypeId.HasValue)
            {
                return Result<PaymentPreparation?>.Failure(
                    PaymentReferencesNotAllowed());
            }

            var balanceError = await ValidateFinalCashboxBalanceAsync(
                currentVoucher,
                proposedCashboxId: null,
                proposedDirection: null,
                proposedAmount: null,
                cancellationToken);
            return balanceError is null
                ? Result<PaymentPreparation?>.Success(null)
                : Result<PaymentPreparation?>.Failure(balanceError);
        }

        if (!cashboxId.HasValue)
        {
            return Result<PaymentPreparation?>.Failure(
                CashboxRequiredForPayment());
        }

        if (!cashMovementTypeId.HasValue)
        {
            return Result<PaymentPreparation?>.Failure(
                CashMovementTypeRequiredForPayment());
        }

        var cashbox = await dbContext.Cashboxes
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate =>
                candidate.CompanyId == companyId &&
                candidate.Id == cashboxId.Value,
                cancellationToken);
        if (cashbox is null)
        {
            return Result<PaymentPreparation?>.Failure(
                CashboxNotFound(cashboxId.Value));
        }

        if (!cashbox.IsActive &&
            (currentVoucher is null ||
             currentVoucher.CashboxId != cashbox.Id))
        {
            return Result<PaymentPreparation?>.Failure(
                CashboxInactive());
        }

        var movementType = await dbContext.CashMovementTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate =>
                candidate.CompanyId == companyId &&
                candidate.Id == cashMovementTypeId.Value,
                cancellationToken);
        if (movementType is null)
        {
            return Result<PaymentPreparation?>.Failure(
                CashMovementTypeNotFound(cashMovementTypeId.Value));
        }

        if (!movementType.IsActive &&
            (currentVoucher is null ||
             currentVoucher.CashMovementTypeId != movementType.Id))
        {
            return Result<PaymentPreparation?>.Failure(
                CashMovementTypeInactive());
        }

        var expectedDirection = InvoiceMovementRules.GetPaymentDirection(
            invoice.InvoiceType);
        if (movementType.Direction != expectedDirection)
        {
            return Result<PaymentPreparation?>.Failure(
                CashMovementTypeDirectionMismatch());
        }

        var expectedEffect = InvoiceMovementRules.GetPaymentPartnerEffect(
            invoice.InvoiceType);
        if (movementType.PartnerEffect != expectedEffect)
        {
            return Result<PaymentPreparation?>.Failure(
                CashMovementTypePartnerEffectMismatch());
        }

        var exchangeRateResult = await exchangeRateResolver.ResolveAsync(
            cashbox.Currency,
            invoice.InvoiceDate,
            requestedCashboxExchangeRate,
            cancellationToken);
        if (exchangeRateResult.IsFailure)
        {
            return Result<PaymentPreparation?>.Failure(
                exchangeRateResult.Error);
        }

        var cashboxAmount = ExchangeRateRules.ConvertFromBase(
            invoice.BasePaidAmountAtInvoiceRate,
            exchangeRateResult.Value.Rate);
        if (cashboxAmount <= 0m)
        {
            return Result<PaymentPreparation?>.Failure(
                CashboxAmountTooSmall());
        }

        var finalBalanceError = await ValidateFinalCashboxBalanceAsync(
            currentVoucher,
            cashbox.Id,
            expectedDirection,
            cashboxAmount,
            cancellationToken);
        if (finalBalanceError is not null)
        {
            return Result<PaymentPreparation?>.Failure(
                finalBalanceError);
        }

        return Result<PaymentPreparation?>.Success(
            new PaymentPreparation(
                cashbox.Id,
                movementType.Id,
                cashbox.Currency,
                exchangeRateResult.Value.ExchangeRateId,
                exchangeRateResult.Value.Rate,
                cashboxAmount));
    }

    private async Task<Error?> ValidateFinalCashboxBalanceAsync(
        CashVoucher? currentVoucher,
        int? proposedCashboxId,
        CashDirection? proposedDirection,
        decimal? proposedAmount,
        CancellationToken cancellationToken)
    {
        var affectedCashboxIds = new HashSet<int>();
        if (currentVoucher is not null)
        {
            affectedCashboxIds.Add(currentVoucher.CashboxId);
        }

        if (proposedCashboxId.HasValue)
        {
            affectedCashboxIds.Add(proposedCashboxId.Value);
        }

        foreach (var cashboxId in affectedCashboxIds)
        {
            var excludedVoucherId = currentVoucher?.Id;
            var balance = await dbContext.Cashboxes
                .AsNoTracking()
                .Where(cashbox =>
                    cashbox.CompanyId == companyId &&
                    cashbox.Id == cashboxId)
                .Select(cashbox =>
                    cashbox.OpeningBalance +
                    (cashbox.Vouchers
                        .Where(voucher =>
                            !excludedVoucherId.HasValue ||
                            voucher.Id != excludedVoucherId.Value)
                        .Sum(voucher =>
                            (decimal?)(voucher.Direction ==
                                CashDirection.Receipt
                                ? voucher.Amount
                                : -voucher.Amount)) ?? 0m))
                .SingleAsync(cancellationToken);

            if (proposedCashboxId == cashboxId &&
                proposedDirection.HasValue &&
                proposedAmount.HasValue)
            {
                balance += proposedDirection == CashDirection.Receipt
                    ? proposedAmount.Value
                    : -proposedAmount.Value;
            }

            if (balance < 0m)
            {
                return InsufficientCashboxBalance(cashboxId);
            }
        }

        return null;
    }

    private async Task<Error?> ValidatePaymentRemovalAsync(
        int invoiceId,
        CancellationToken cancellationToken)
    {
        var currentVoucher = await dbContext.CashVouchers
            .FirstOrDefaultAsync(voucher =>
                voucher.CompanyId == companyId &&
                voucher.InvoiceId == invoiceId,
                cancellationToken);

        return await ValidateFinalCashboxBalanceAsync(
            currentVoucher,
            proposedCashboxId: null,
            proposedDirection: null,
            proposedAmount: null,
            cancellationToken);
    }

    private async Task<Error?> ValidateStockAsync(
        Invoice invoice,
        IReadOnlyList<InvoiceLineRequest> lines,
        int? currentInvoiceId,
        string? currentInvoiceNumber,
        CancellationToken cancellationToken)
    {
        var hasCurrentInvoiceId = currentInvoiceId.HasValue;
        var hasCurrentInvoiceNumber =
            !string.IsNullOrWhiteSpace(currentInvoiceNumber);
        if (hasCurrentInvoiceId != hasCurrentInvoiceNumber)
        {
            return InvalidCurrentInvoiceReference();
        }

        var stockLines = new List<InventoryStockLine>(lines.Count);
        foreach (var line in lines)
        {
            if (!TryGetEffectiveLineValues(
                    line,
                    out var count,
                    out var weight))
            {
                return InvalidCalculatedAmounts(InvoiceCalculationErrorKind.Quantity);
            }

            if (!InvoiceAmountRules.TryCalculate(
                    count,
                    weight,
                    0m,
                    out var quantity,
                    out _))
            {
                return InvalidCalculatedAmounts(InvoiceCalculationErrorKind.Quantity);
            }

            stockLines.Add(new InventoryStockLine(line.ItemId, quantity));
        }

        var replacedMovement = currentInvoiceId is int invoiceId
            ? new InventoryMovementReference(
                InvoiceItemMovementTypes,
                invoiceId,
                currentInvoiceNumber!)
            : null;

        return await inventoryStockService.ValidateTimelineAsync(
            new InventoryStockProposal(
                invoice.StoreId,
                invoice.InvoiceDate,
                InvoiceMovementRules.IsInbound(invoice.InvoiceType),
                stockLines,
                replacedMovement,
                currentInvoiceId.HasValue
                    ? $"تعديل الفاتورة {currentInvoiceNumber}"
                    : "إضافة الفاتورة",
                nameof(InvoiceRequest.Lines)),
            cancellationToken);
    }

    private async Task<Error?> ValidateStockLegacyAsync(
        Invoice invoice,
        IReadOnlyList<InvoiceLineRequest> lines,
        int? currentInvoiceId,
        string? currentInvoiceNumber,
        CancellationToken cancellationToken)
    {
        // 1. Add operations have no current invoice reference. Updates must have
        // both reference values so their existing movements can be identified.
        var hasCurrentInvoiceId = currentInvoiceId.HasValue;
        var hasCurrentInvoiceNumber =
            !string.IsNullOrWhiteSpace(currentInvoiceNumber);
        if (hasCurrentInvoiceId != hasCurrentInvoiceNumber)
        {
            return InvalidCurrentInvoiceReference();
        }

        var isUpdate = hasCurrentInvoiceId;
        var currentReferenceNumber = currentInvoiceNumber ?? string.Empty;

        // 2. Calculate and aggregate the requested quantity for each item.
        var isInbound = InvoiceMovementRules.IsInbound(invoice.InvoiceType);
        var requestedByItem = new Dictionary<int, decimal>();
        foreach (var line in lines)
        {
            if (!TryGetEffectiveLineValues(
                    line,
                    out var count,
                    out var weight))
            {
                return InvalidCalculatedAmounts(InvoiceCalculationErrorKind.Quantity);
            }

            if (!InvoiceAmountRules.TryCalculate(
                    count,
                    weight,
                    0m,
                    out var quantity,
                    out _))
            {
                return InvalidCalculatedAmounts(InvoiceCalculationErrorKind.Quantity);
            }

            requestedByItem[line.ItemId] =
                requestedByItem.GetValueOrDefault(line.ItemId) + quantity;
        }

        // 3. A new inbound invoice only adds stock, so it cannot create a
        // shortage. Updates must still validate the resulting state.
        if (isInbound &&
            currentInvoiceId is null)
        {
            return null;
        }

        // 4. Load the current invoice movement locations without filtering by
        // the requested store, because an update may move the invoice.
        var currentMovements = new List<(int StoreId, int ItemId)>();
        if (currentInvoiceId is int invoiceId)
        {
            var currentMovementRows = await dbContext.ItemMovements
                .AsNoTracking()
                .Where(movement =>
                    movement.CompanyId == companyId &&
                    movement.ReferenceId == invoiceId &&
                    movement.ReferenceNumber == currentReferenceNumber)
                .Select(movement => new
                {
                    movement.StoreId,
                    movement.ItemId
                })
                .ToListAsync(cancellationToken);

            currentMovements = currentMovementRows
                .Select(movement => (movement.StoreId, movement.ItemId))
                .ToList();
        }

        // 5. Validate only exact old and requested store/item combinations.
        var affectedStockKeys = currentMovements.ToHashSet();
        foreach (var itemId in requestedByItem.Keys)
        {
            affectedStockKeys.Add((invoice.StoreId, itemId));
        }

        // 6. Load all movements that contribute to the affected stock timelines.
        var storeIdArray = affectedStockKeys
            .Select(key => key.StoreId)
            .Distinct()
            .ToArray();
        var itemIdArray = affectedStockKeys
            .Select(key => key.ItemId)
            .Distinct()
            .ToArray();
        var itemNames = await dbContext.Items
            .AsNoTracking()
            .Where(item =>
                item.CompanyId == companyId &&
                itemIdArray.Contains(item.Id))
            .Select(item => new
            {
                item.Id,
                item.Name
            })
            .ToDictionaryAsync(
                item => item.Id,
                item => item.Name,
                cancellationToken);
        var movementQuery = dbContext.ItemMovements
            .AsNoTracking()
            .Where(movement =>
                movement.CompanyId == companyId &&
                storeIdArray.Contains(movement.StoreId) &&
                itemIdArray.Contains(movement.ItemId));

        // 7. Remove the current invoice movements from the stored state.
        // The proposed invoice values will be added back below.
        if (currentInvoiceId is int excludedInvoiceId)
        {
            movementQuery = movementQuery.Where(movement =>
                movement.ReferenceId != excludedInvoiceId ||
                movement.ReferenceNumber != currentReferenceNumber);
        }

        var movements = await movementQuery
            .Select(movement => new
            {
                movement.Id,
                movement.StoreId,
                movement.ItemId,
                movement.MovementDate,
                movement.QuantityIn,
                movement.QuantityOut
            })
            .ToListAsync(cancellationToken);

        // 8. Load the opening balances for every affected store and item.
        var openingBalances = await dbContext.StockOpeningBalanceLines
            .AsNoTracking()
            .Where(line =>
                line.CompanyId == companyId &&
                line.StockOpeningBalance.CompanyId == companyId &&
                storeIdArray.Contains(line.StockOpeningBalance.StoreId) &&
                itemIdArray.Contains(line.ItemId))
            .Select(group => new
            {
                group.Id,
                StoreId = group.StockOpeningBalance.StoreId,
                group.ItemId,
                Date = group.StockOpeningBalance.DocumentDate,
                Quantity = group.Quantity
            })
            .ToListAsync(cancellationToken);

        var operationDescription = isUpdate
            ? $"تعديل الفاتورة {currentReferenceNumber}"
            : "إضافة الفاتورة";

        // 9. Build and validate only the exact affected store/item timelines.
        foreach (var (storeId, itemId) in affectedStockKeys)
        {
            var itemName = itemNames.GetValueOrDefault(itemId) ??
                itemId.ToString();

            // The proposed movement belongs only to the requested store.
            // Removed items and items in the old store have quantity zero.
            var requestedQuantity =
                storeId == invoice.StoreId
                    ? requestedByItem.GetValueOrDefault(itemId)
                    : 0m;
            var events = new List<(
                DateOnly Date,
                int Priority,
                int Id,
                decimal QuantityIn,
                decimal QuantityOut,
                bool IsCurrentInvoice)>();

            // Opening balances are processed first on their document date.
            events.AddRange(
                openingBalances
                    .Where(line =>
                        line.StoreId == storeId &&
                        line.ItemId == itemId)
                    .Select(line =>
                        (
                            line.Date,
                            Priority: 0,
                            line.Id,
                            QuantityIn: line.Quantity,
                            QuantityOut: 0m,
                            IsCurrentInvoice: false)));

            // Existing inbound movements precede existing outbound movements
            // when they share the same date.
            foreach (var movement in movements.Where(
                         movement =>
                             movement.StoreId == storeId &&
                             movement.ItemId == itemId))
            {
                if (movement.QuantityIn > 0m)
                {
                    events.Add(
                        (
                            movement.MovementDate,
                            Priority: 1,
                            movement.Id,
                            QuantityIn: movement.QuantityIn,
                            QuantityOut: 0m,
                            IsCurrentInvoice: false));
                }

                if (movement.QuantityOut > 0m)
                {
                    events.Add(
                        (
                            movement.MovementDate,
                            Priority: 2,
                            movement.Id,
                            QuantityIn: 0m,
                            QuantityOut: movement.QuantityOut,
                            IsCurrentInvoice: false));
                }
            }

            // Add the non-zero proposed movement only to the requested store.
            // int.MaxValue places it after stored movements of the same direction.
            if (storeId == invoice.StoreId &&
                requestedQuantity > 0m)
            {
                events.Add(
                    (
                        invoice.InvoiceDate,
                        Priority: isInbound ? 1 : 2,
                        Id: int.MaxValue,
                        QuantityIn: isInbound
                            ? requestedQuantity
                            : 0m,
                        QuantityOut: isInbound
                            ? 0m
                            : requestedQuantity,
                        IsCurrentInvoice: true));
            }

            // 10. Process the complete timeline chronologically:
            // opening balance, inbound movements, then outbound movements.
            var balance = 0m;
            foreach (var stockEvent in events
                         .OrderBy(stockEvent => stockEvent.Date)
                         .ThenBy(stockEvent => stockEvent.Priority)
                         .ThenBy(stockEvent => stockEvent.Id))
            {
                var availableBeforeMovement = balance;
                balance +=
                    stockEvent.QuantityIn -
                    stockEvent.QuantityOut;
                if (balance >= 0m)
                {
                    continue;
                }

                // The proposed outbound movement itself exceeds available stock.
                if (stockEvent.IsCurrentInvoice &&
                    stockEvent.QuantityOut > 0m)
                {
                    return InventoryErrors.InsufficientStockAtDate(itemName, itemId, storeId, stockEvent.Date, availableBeforeMovement, stockEvent.QuantityOut, nameof(InvoiceRequest.Lines));
                }

                // Removing, moving, reducing, or re-dating the old invoice
                // causes a later stored movement to make the balance negative.
                return InventoryErrors.HistoricalStockConflict(operationDescription, invoice.InvoiceDate, itemName, itemId, storeId, stockEvent.Date, availableBeforeMovement, stockEvent.QuantityOut, nameof(InvoiceRequest.Lines));
            }
        }

        return null;
    }

    private static void NormalizeDriverValues(Invoice invoice)
    {
        if (invoice.ActualDriverId == invoice.DriverId)
        {
            invoice.ActualDriverId = null;
        }

        if (!invoice.UsesExternalDriver)
        {
            invoice.ExternalDriverName = null;
        }
    }
}
