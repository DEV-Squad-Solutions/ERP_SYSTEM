using Microsoft.EntityFrameworkCore;
using static MiniErp.Application.Features.Invoices.InvoiceErrors;
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
        out decimal weight) =>
        InvoiceLineValues.TryGetEffective(request, out count, out weight);

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

        if (invoice.InvoiceType is not
            (InvoiceType.SalesReturn or InvoiceType.PurchaseReturn) &&
            lines.Any(line =>
                line.SourceInvoiceLineId.HasValue ||
                line.ReturnUnitCost.HasValue))
        {
            return Failure(
                ReturnCostFieldsNotAllowed());
        }

        if (invoice.InvoiceType == InvoiceType.PurchaseReturn &&
            lines.Any(line => line.ReturnUnitCost.HasValue))
        {
            return Failure(
                ReturnCostFieldsNotAllowed());
        }

        if (lines.Any(line => line.ReturnUnitCost < 0m))
        {
            return Failure(
                ReturnUnitCostInvalid());
        }

        var preparedReturnSourcesResult =
            await PrepareReturnSourcesAsync(
                invoice,
                lines,
                currentInvoiceId,
                cancellationToken);
        if (preparedReturnSourcesResult.IsFailure)
        {
            return Failure(preparedReturnSourcesResult.Error);
        }

        var preparedReturnSources = preparedReturnSourcesResult.Value;

        if (!Enum.IsDefined(typeof(PaymentTerm), invoice.PaymentTerm))
        {
            return Failure(
                PaymentTermInvalid(nameof(InvoiceRequest.PaymentTerm)));
        }

        if (lines.Where(line => line.ItemId.HasValue)
            .GroupBy(line => line.ItemId)
            .Any(group => group.Count() > 1))
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
            .Where(line => line.ItemId.HasValue)
            .Select(line => line.ItemId!.Value)
            .Distinct()
            .ToArray();
        var items = itemIds.Length > 0
            ? await dbContext.Items
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
                .ToListAsync(cancellationToken)
            : [];
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
                Currency: partner.Currency,
                ItemUnitIds: items.ToDictionary(
                    item => item.Id,
                    item => item.ItemUnitId),
                ReturnSourceLines: preparedReturnSources.Lines,
                ReturnDiscountAmount:
                    preparedReturnSources.DiscountAmount));
    }

    private async Task<Result<PreparedReturnSources>>
        PrepareReturnSourcesAsync(
            Invoice invoice,
            IReadOnlyList<InvoiceLineRequest> lines,
            int? currentInvoiceId,
            CancellationToken cancellationToken)
    {
        if (invoice.InvoiceType is not
            (InvoiceType.SalesReturn or InvoiceType.PurchaseReturn))
        {
            return Result<PreparedReturnSources>.Success(
                PreparedReturnSources.Empty);
        }

        var linkedLines = lines
            .Where(line => line.SourceInvoiceLineId.HasValue)
            .ToArray();
        if (linkedLines.Length == 0)
        {
            return Result<PreparedReturnSources>.Success(
                PreparedReturnSources.Empty);
        }

        if (linkedLines.Length != lines.Count ||
            linkedLines.Any(line => !line.ItemId.HasValue))
        {
            return Result<PreparedReturnSources>.Failure(
                ReturnLinesMustUseOneSourceInvoice());
        }

        var linkedLineIds = linkedLines
            .Select(line => line.SourceInvoiceLineId!.Value)
            .Distinct()
            .ToArray();
        var sourceType = invoice.InvoiceType == InvoiceType.SalesReturn
            ? InvoiceType.Sales
            : InvoiceType.Purchase;
        var returnType = invoice.InvoiceType;
        var sourceLines = await dbContext.InvoiceLines
            .AsNoTracking()
            .Where(source =>
                source.CompanyId == companyId &&
                linkedLineIds.Contains(source.Id) &&
                source.ItemId.HasValue &&
                source.Invoice.CompanyId == companyId &&
                source.Invoice.InvoiceType == sourceType &&
                source.Invoice.ContentType == InvoiceContentType.Items &&
                source.Invoice.BusinessPartnerId ==
                    invoice.BusinessPartnerId &&
                source.Invoice.StoreId == invoice.StoreId &&
                source.Invoice.InvoiceDate <= invoice.InvoiceDate)
            .Select(source => new
            {
                source.Id,
                source.InvoiceId,
                ItemId = source.ItemId!.Value,
                SourceQuantity = source.Quantity,
                UnitPrice = source.Price,
                SourceInvoiceDiscount = source.Invoice.DiscountAmount,
                ReturnedQuantity = dbContext.InvoiceLines
                    .Where(returnLine =>
                        returnLine.CompanyId == companyId &&
                        returnLine.SourceInvoiceLineId == source.Id &&
                        returnLine.Invoice.InvoiceType == returnType &&
                        (!currentInvoiceId.HasValue ||
                         returnLine.InvoiceId != currentInvoiceId.Value))
                    .Sum(returnLine =>
                        (decimal?)returnLine.Quantity) ?? 0m
            })
            .ToListAsync(cancellationToken);

        if (sourceLines.Count != linkedLineIds.Length ||
            sourceLines.Select(source => source.InvoiceId)
                .Distinct()
                .Count() != 1)
        {
            return Result<PreparedReturnSources>.Failure(
                InvalidReturnSource());
        }

        var sourceById = sourceLines.ToDictionary(source => source.Id);
        var requestedQuantities = new Dictionary<int, decimal>();
        foreach (var line in linkedLines)
        {
            if (!sourceById.TryGetValue(
                    line.SourceInvoiceLineId!.Value,
                    out var source) ||
                source.ItemId != line.ItemId)
            {
                return Result<PreparedReturnSources>.Failure(
                    InvalidReturnSource());
            }

            if (!TryGetEffectiveLineValues(
                    line,
                    out var count,
                    out var weight) ||
                !InvoiceAmountRules.TryCalculate(
                    count,
                    weight,
                    source.UnitPrice,
                    out var requestedQuantity,
                    out _))
            {
                return Result<PreparedReturnSources>.Failure(
                    InvalidCalculatedAmounts(
                        InvoiceCalculationErrorKind.LineQuantityOrTotal));
            }

            var availableQuantity =
                source.SourceQuantity - source.ReturnedQuantity;
            if (requestedQuantity > availableQuantity)
            {
                return Result<PreparedReturnSources>.Failure(
                    ReturnQuantityExceedsAvailable(
                        source.Id,
                        availableQuantity));
            }

            requestedQuantities[source.Id] = requestedQuantity;
        }

        var sourceInvoiceId = sourceLines[0].InvoiceId;
        var sourceDiscount = sourceLines[0].SourceInvoiceDiscount;
        var sourceInvoiceLineCount = await dbContext.InvoiceLines
            .AsNoTracking()
            .CountAsync(
                sourceLine =>
                    sourceLine.CompanyId == companyId &&
                    sourceLine.InvoiceId == sourceInvoiceId,
                cancellationToken);
        var isFullOriginalInvoiceReturn =
            linkedLineIds.Length == sourceInvoiceLineCount &&
            sourceLines.All(source =>
                source.ReturnedQuantity == 0m &&
                requestedQuantities.TryGetValue(
                    source.Id,
                    out var requestedQuantity) &&
                requestedQuantity == source.SourceQuantity);
        var discountAmount = isFullOriginalInvoiceReturn
            ? sourceDiscount
            : 0m;
        var preparedLines = sourceLines.ToDictionary(
            source => source.Id,
            source => new PreparedReturnSourceLine(
                SourceInvoiceLineId: source.Id,
                SourceInvoiceId: source.InvoiceId,
                UnitPrice: source.UnitPrice));

        return Result<PreparedReturnSources>.Success(
            new PreparedReturnSources(
                Lines: preparedLines,
                DiscountAmount: discountAmount));
    }

    private static Error? ValidateAmounts(
        Invoice invoice,
        decimal? requestedWBTotal)
    {
        if (!InvoiceAmountRules.IsValidQuantity(invoice.WBWeight) ||
            !InvoiceAmountRules.IsValidQuantity(
                invoice.WBScaleDifference) ||
            !InvoiceAmountRules.IsValidQuantity(invoice.WBDiscount) ||
            !InvoiceAmountRules.IsValidQuantity(invoice.WBTotal))
        {
            return InvalidWBTotal();
        }

        if (requestedWBTotal.HasValue &&
            !InvoiceAmountRules.IsValidQuantity(requestedWBTotal.Value))
        {
            return InvalidWBTotal(
                fieldName: nameof(InvoiceRequest.WBTotal));
        }

        if (invoice.ContentType == InvoiceContentType.Items &&
            requestedWBTotal is > 0m)
        {
            var totalItemQuantity = decimal.Round(
                invoice.Lines.Sum(line => line.Quantity),
                InvoiceAmountRules.QuantityScale,
                MidpointRounding.AwayFromZero);
            if (requestedWBTotal.Value != totalItemQuantity)
            {
                return WBTotalDoesNotMatchItemQuantity(
                    requestedWBTotal.Value,
                    totalItemQuantity,
                    fieldName: nameof(InvoiceRequest.WBTotal));
            }
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
            if (cashboxId.HasValue)
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

        var expectedDirection = InvoiceMovementRules.GetPaymentDirection(
            invoice.InvoiceType);
        var expectedEffect = InvoiceMovementRules.GetPaymentPartnerEffect(
            invoice.InvoiceType);

        var movementType = currentVoucher?.CashMovementTypeId is int currentTypeId
            ? await dbContext.CashMovementTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(candidate =>
                    candidate.CompanyId == companyId &&
                    candidate.Id == currentTypeId &&
                    candidate.Direction == expectedDirection &&
                    candidate.PartnerEffect == expectedEffect,
                    cancellationToken)
            : null;

        var defaultMovementTypes = dbContext.CashMovementTypes
            .AsNoTracking()
            .Where(candidate =>
                candidate.CompanyId == companyId &&
                candidate.Direction == expectedDirection &&
                candidate.PartnerEffect == expectedEffect &&
                candidate.IsActive);

        defaultMovementTypes = invoice.InvoiceType switch
        {
            InvoiceType.Sales => defaultMovementTypes.Where(
                candidate => candidate.IsDefaultForSales),
            InvoiceType.Purchase => defaultMovementTypes.Where(
                candidate => candidate.IsDefaultForPurchase),
            InvoiceType.SalesReturn => defaultMovementTypes.Where(
                candidate => candidate.IsDefaultForSalesReturn),
            InvoiceType.PurchaseReturn => defaultMovementTypes.Where(
                candidate => candidate.IsDefaultForPurchaseReturn),
            _ => defaultMovementTypes.Where(candidate => false)
        };

        movementType ??= await defaultMovementTypes
            .FirstOrDefaultAsync(cancellationToken);

        if (movementType is null)
        {
            return Result<PaymentPreparation?>.Failure(
                DefaultCashMovementTypeNotFound(invoice.InvoiceType));
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
        if (currentVoucher?.CashboxId is int currentCashboxId)
        {
            affectedCashboxIds.Add(currentCashboxId);
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
                            (voucher.CashMovementTypeId.HasValue ||
                             voucher.CashboxTransferId.HasValue) &&
                            (!excludedVoucherId.HasValue ||
                             voucher.Id != excludedVoucherId.Value))
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
        CancellationToken cancellationToken) =>
        await invoiceInventoryService.ValidateStockAsync(
            storeId: invoice.StoreId,
            invoiceDate: invoice.InvoiceDate,
            invoiceType: invoice.InvoiceType,
            lines: lines,
            currentInvoiceId: currentInvoiceId,
            currentInvoiceNumber: currentInvoiceNumber,
            cancellationToken: cancellationToken);

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
