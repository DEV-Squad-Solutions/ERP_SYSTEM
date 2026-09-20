using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Invoices;
using MiniErp.Domain.Entities.BusinessPartners;
using MiniErp.Domain.Entities.Invoicing;
using MiniErp.Domain.Enums;
using static MiniErp.Application.Features.Invoices.InvoiceErrors;

namespace MiniErp.Infrastructure.Services.Invoices;

public sealed partial class InvoiceService
{
    private async Task<List<Invoice>> LoadActiveLinkedReturnsAsync(
        IReadOnlyCollection<int> sourceLineIds,
        CancellationToken cancellationToken)
    {
        if (sourceLineIds.Count == 0)
        {
            return [];
        }

        return await dbContext.Invoices
            .AsSplitQuery()
            .Include(invoice => invoice.Lines)
            .Where(invoice =>
                invoice.CompanyId == companyId &&
                (invoice.InvoiceType == InvoiceType.SalesReturn ||
                 invoice.InvoiceType == InvoiceType.PurchaseReturn) &&
                invoice.Lines.Any(line =>
                    line.SourceInvoiceLineId.HasValue &&
                    sourceLineIds.Contains(line.SourceInvoiceLineId.Value)))
            .OrderBy(invoice => invoice.Id)
            .ToListAsync(cancellationToken);
    }

    private static Error? ValidateLinkedReturnSourceUpdate(
        Invoice invoice,
        InvoiceUpdateRequest request,
        IReadOnlyCollection<Invoice> linkedReturns)
    {
        if (invoice.InvoiceType != request.InvoiceType)
        {
            return LinkedReturnSourceHeaderCannotChange(
                nameof(InvoiceUpdateRequest.InvoiceType),
                "نوع الفاتورة");
        }

        if (invoice.ContentType != request.ContentType)
        {
            return LinkedReturnSourceHeaderCannotChange(
                nameof(InvoiceUpdateRequest.ContentType),
                "محتوى الفاتورة");
        }

        if (invoice.BusinessPartnerId != request.BusinessPartnerId)
        {
            return LinkedReturnSourceHeaderCannotChange(
                nameof(InvoiceUpdateRequest.BusinessPartnerId),
                "العميل أو المورد");
        }

        if (invoice.StoreId != request.StoreId)
        {
            return LinkedReturnSourceHeaderCannotChange(
                nameof(InvoiceUpdateRequest.StoreId),
                "المخزن");
        }

        var firstReturnDate = linkedReturns.Min(candidate => candidate.InvoiceDate);
        if (request.InvoiceDate > firstReturnDate)
        {
            return LinkedReturnSourceDateCannotMoveAfterReturn(firstReturnDate);
        }

        var returnedQuantities = linkedReturns
            .SelectMany(returnInvoice => returnInvoice.Lines)
            .Where(line => line.SourceInvoiceLineId.HasValue)
            .GroupBy(line => line.SourceInvoiceLineId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(line => line.Quantity));
        var currentLinesById = invoice.Lines
            .Where(line => !line.IsDeleted)
            .ToDictionary(line => line.Id);
        var incomingByItem = request.Lines
            .Where(line => line.ItemId.HasValue)
            .GroupBy(line => line.ItemId!.Value)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var (sourceLineId, returnedQuantity) in returnedQuantities)
        {
            if (!currentLinesById.TryGetValue(sourceLineId, out var sourceLine) ||
                !sourceLine.ItemId.HasValue)
            {
                return LinkedReturnSourceLineCannotRemove(sourceLineId);
            }

            if (!incomingByItem.TryGetValue(
                    sourceLine.ItemId.Value,
                    out var incomingLine))
            {
                return LinkedReturnSourceLineCannotRemove(sourceLineId);
            }

            if (!TryGetEffectiveLineValues(
                    incomingLine,
                    out var count,
                    out var weight) ||
                !InvoiceAmountRules.TryCalculate(
                    count,
                    weight,
                    incomingLine.Price,
                    out var requestedQuantity,
                    out _))
            {
                // The regular invoice preparation returns the more specific
                // amount error. Do not mask it with a linked-return error.
                continue;
            }

            if (requestedQuantity < returnedQuantity)
            {
                return LinkedReturnSourceQuantityTooSmall(
                    sourceLineId,
                    returnedQuantity);
            }
        }

        return null;
    }

    private async Task<Result<IReadOnlyList<int>>>
        SynchronizeLinkedReturnFinancialsAsync(
        Invoice sourceInvoice,
        IReadOnlyList<Invoice> linkedReturns,
        CancellationToken cancellationToken)
    {
        static Result<IReadOnlyList<int>> Failure(Error error) =>
            Result<IReadOnlyList<int>>.Failure(error);

        var sourceLines = sourceInvoice.Lines
            .Where(line => !line.IsDeleted)
            .ToDictionary(line => line.Id);
        var sourceLineCount = sourceLines.Count;
        var changedReturnIds = new List<int>();

        foreach (var returnInvoice in linkedReturns)
        {
            var targetPrices = new Dictionary<InvoiceLine, decimal>();
            foreach (var returnLine in returnInvoice.Lines.Where(
                         line =>
                             !line.IsDeleted &&
                             line.SourceInvoiceLineId.HasValue))
            {
                if (!sourceLines.TryGetValue(
                        returnLine.SourceInvoiceLineId!.Value,
                        out var sourceLine))
                {
                    return Failure(
                        LinkedReturnUpdateConflict(
                            returnInvoice.InvoiceNumber,
                            "أحد سطور المصدر المرتبطة لم يعد موجودًا."));
                }

                targetPrices[returnLine] = sourceLine.Price;
            }

            var returnLines = returnInvoice.Lines
                .Where(line =>
                    !line.IsDeleted &&
                    line.SourceInvoiceLineId.HasValue)
                .ToArray();
            var isFullOriginalInvoiceReturn =
                linkedReturns.Count == 1 &&
                returnLines.Length == sourceLineCount &&
                sourceLines.Values.All(sourceLine =>
                    returnLines.Any(returnLine =>
                        returnLine.SourceInvoiceLineId == sourceLine.Id &&
                        returnLine.Quantity == sourceLine.Quantity));
            var targetDiscountAmount = isFullOriginalInvoiceReturn
                ? sourceInvoice.DiscountAmount
                : 0m;

            var targetSubtotal = 0m;
            foreach (var returnLine in returnInvoice.Lines.Where(
                         line => !line.IsDeleted))
            {
                var targetPrice = targetPrices.TryGetValue(
                    returnLine,
                    out var sourcePrice)
                    ? sourcePrice
                    : returnLine.Price;
                if (!InvoiceAmountRules.TryCalculate(
                        returnLine.Count,
                        returnLine.Weight,
                        targetPrice,
                        out _,
                        out var targetLineTotal))
                {
                    return Failure(
                        LinkedReturnUpdateConflict(
                            returnInvoice.InvoiceNumber,
                            "قيم أحد سطور المرتجع لا يمكن حسابها بعد تحديث المصدر."));
                }

                targetSubtotal += targetLineTotal;
            }

            var targetTotal = decimal.Round(
                targetSubtotal - targetDiscountAmount,
                InvoiceAmountRules.MoneyScale,
                MidpointRounding.AwayFromZero);
            var financialValuesChanged =
                targetDiscountAmount != returnInvoice.DiscountAmount ||
                targetTotal != returnInvoice.Total ||
                targetPrices.Any(pair => pair.Key.Price != pair.Value);
            if (!financialValuesChanged)
            {
                continue;
            }

            // Guard the linked return before changing any tracked financial
            // value. Any later payment/amount failure is rolled back and the
            // change tracker is cleared by UpdateAsync.
            if (fiscalYearPeriodGuard is not null)
            {
                var fiscalYearResult =
                    await fiscalYearPeriodGuard.EnsureOpenAsync(
                        returnInvoice.InvoiceDate,
                        nameof(InvoiceRequest.InvoiceDate),
                        cancellationToken);
                if (fiscalYearResult.IsFailure)
                {
                    return Result<IReadOnlyList<int>>.Failure(
                        fiscalYearResult.Errors);
                }
            }

            // The return keeps its quantity, ReturnUnitCost and movement
            // identity; only the source-driven sales price is synchronized.
            foreach (var (returnLine, targetPrice) in targetPrices)
            {
                returnLine.Price = targetPrice;
            }

            returnInvoice.DiscountAmount = targetDiscountAmount;
            returnInvoice.CalculateTotal();

            var exchangeRateId = dbContext.Entry(returnInvoice)
                .Property<int?>(nameof(Invoice.ExchangeRateId))
                .CurrentValue;
            returnInvoice.ApplyExchangeRate(
                exchangeRateId,
                returnInvoice.ExchangeRate);

            var amountError = ValidateAmounts(
                returnInvoice,
                requestedWBTotal: null);
            if (amountError is not null)
            {
                return Failure(
                    LinkedReturnUpdateConflict(
                        returnInvoice.InvoiceNumber,
                        amountError.Description));
            }

            var currentVoucher = await dbContext.CashVouchers
                .FirstOrDefaultAsync(
                    voucher =>
                        voucher.CompanyId == companyId &&
                        voucher.InvoiceId == returnInvoice.Id,
                    cancellationToken);
            var paymentPreparation = await PreparePaymentAsync(
                returnInvoice,
                currentVoucher?.CashboxId,
                currentVoucher?.ExchangeRate,
                returnInvoice.Id,
                cancellationToken);
            if (paymentPreparation.IsFailure)
            {
                return Failure(
                    LinkedReturnUpdateConflict(
                        returnInvoice.InvoiceNumber,
                        paymentPreparation.Error.Description));
            }

            await SynchronizeInvoicePartnerMovementAsync(
                returnInvoice,
                cancellationToken);
            returnInvoice.Touch(timeProvider.GetUtcNow().UtcDateTime);
            dbContext.Entry(returnInvoice)
                .Property(invoice => invoice.LastModifiedAt)
                .IsModified = true;
            changedReturnIds.Add(returnInvoice.Id);
        }

        return Result<IReadOnlyList<int>>.Success(changedReturnIds);
    }

    private async Task SynchronizeInvoicePartnerMovementAsync(
        Invoice invoice,
        CancellationToken cancellationToken)
    {
        var movement = await dbContext.BusinessPartnerMovements
            .FirstOrDefaultAsync(candidate =>
                candidate.CompanyId == companyId &&
                candidate.InvoiceId == invoice.Id,
                cancellationToken);

        if (!InvoiceMovementRules.ShouldCreatePartnerMovement(invoice.Total))
        {
            if (movement is not null)
            {
                dbContext.BusinessPartnerMovements.Remove(movement);
            }

            return;
        }

        var movementType = InvoiceMovementRules.GetPartnerMovementType(
            invoice.InvoiceType);
        var (debit, credit) = InvoiceMovementRules.GetPartnerAmounts(
            invoice.InvoiceType,
            invoice.Total);
        if (movement is null)
        {
            movement = new BusinessPartnerMovement
            {
                CompanyId = companyId,
                BusinessPartnerId = invoice.BusinessPartnerId,
                InvoiceId = invoice.Id,
                MovementType = movementType,
                MovementDate = invoice.InvoiceDate,
                Currency = invoice.Currency,
                Debit = debit,
                Credit = credit,
                Description = $"Invoice {invoice.InvoiceNumber}"
            };
            movement.ApplyExchangeRate(invoice.ExchangeRate);
            dbContext.BusinessPartnerMovements.Add(movement);
            return;
        }

        movement.BusinessPartnerId = invoice.BusinessPartnerId;
        movement.MovementType = movementType;
        movement.MovementDate = invoice.InvoiceDate;
        movement.Currency = invoice.Currency;
        movement.Debit = debit;
        movement.Credit = credit;
        movement.Description = $"Invoice {invoice.InvoiceNumber}";
        movement.ApplyExchangeRate(invoice.ExchangeRate);
    }
}
