using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Invoices;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Entities.Invoicing;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Enums;

namespace MiniErp.Infrastructure.Services.Invoices;

public sealed partial class InvoiceService
{
    private static Error? ValidateFilters(InvoiceFilterRequest filters)
    {
        if (filters.InvoiceNumber?.Trim().Length >
            InvoiceRequest.InvoiceNumberMaximumLength)
        {
            return Error.Validation(
                "Invoices.InvoiceNumberFilterInvalid",
                "رقم الفاتورة في البحث يجب ألا يتجاوز 100 حرف.",
                nameof(InvoiceFilterRequest.InvoiceNumber));
        }

        if (filters.InvoiceType.HasValue &&
            !Enum.IsDefined(
                typeof(InvoiceType),
                filters.InvoiceType.Value))
        {
            return Error.Validation(
                "Invoices.InvoiceTypeInvalid",
                "نوع الفاتورة غير مدعوم.",
                nameof(InvoiceFilterRequest.InvoiceType));
        }

        if (filters.PaymentTerm.HasValue &&
            !Enum.IsDefined(
                typeof(PaymentTerm),
                filters.PaymentTerm.Value))
        {
            return Error.Validation(
                "Invoices.PaymentTermInvalid",
                "شرط السداد غير مدعوم.",
                nameof(InvoiceFilterRequest.PaymentTerm));
        }

        if (filters.PriceStatus.HasValue &&
            !Enum.IsDefined(
                typeof(InvoicePriceStatus),
                filters.PriceStatus.Value))
        {
            return InvalidFilter(
                nameof(InvoiceFilterRequest.PriceStatus),
                "حالة تسعير الأصناف غير مدعومة.");
        }

        if (filters.BusinessPartnerId is <= 0)
        {
            return InvalidFilter(
                nameof(InvoiceFilterRequest.BusinessPartnerId),
                "رقم الطرف يجب أن يكون أكبر من صفر.");
        }

        if (filters.CountryId is <= 0)
        {
            return InvalidFilter(
                nameof(InvoiceFilterRequest.CountryId),
                "رقم الدولة يجب أن يكون أكبر من صفر.");
        }

        if (filters.StoreId is <= 0)
        {
            return InvalidFilter(
                nameof(InvoiceFilterRequest.StoreId),
                "رقم المخزن يجب أن يكون أكبر من صفر.");
        }

        if (filters.DriverId is <= 0)
        {
            return InvalidFilter(
                nameof(InvoiceFilterRequest.DriverId),
                "رقم السائق يجب أن يكون أكبر من صفر.");
        }

        if (filters.FromDate > filters.ToDate)
        {
            return InvalidFilter(
                nameof(InvoiceFilterRequest.ToDate),
                "تاريخ النهاية يجب ألا يسبق تاريخ البداية.");
        }

        return null;
    }

    private static Error InvalidFilter(string target, string description) =>
        Error.Validation(
            "Invoices.InvalidFilter",
            description,
            target);

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
                    Error.Validation(
                        "Invoices.InvoiceNumberInvalid",
                        "رقم الفاتورة مطلوب ويجب ألا يتجاوز 100 حرف.",
                        nameof(InvoiceRequest.InvoiceNumber)));
            }
        }

        if (!Enum.IsDefined(typeof(InvoiceType), invoice.InvoiceType))
        {
            return Failure(
                Error.Validation(
                    "Invoices.InvoiceTypeInvalid",
                    "نوع الفاتورة غير مدعوم.",
                    nameof(InvoiceRequest.InvoiceType)));
        }

        if (!Enum.IsDefined(
                typeof(InvoiceContentType),
                invoice.ContentType))
        {
            return Failure(
                Error.Validation(
                    "Invoices.ContentTypeInvalid",
                    "محتوى الفاتورة غير مدعوم.",
                    nameof(InvoiceRequest.ContentType)));
        }

        if (invoice.ContentType == InvoiceContentType.Containers &&
            lines.Count > 0)
        {
            return Failure(
                Error.Validation(
                    "Invoices.ItemLinesNotAllowedForContainerInvoice",
                    "لا يجوز إضافة سطور أصناف إلى فاتورة محتواها عبوات.",
                    nameof(InvoiceRequest.Lines)));
        }

        if (invoice.ContentType == InvoiceContentType.Containers &&
            containerLines.Count == 0)
        {
            return Failure(
                Error.Validation(
                    "Invoices.ContainerLinesRequired",
                    "يجب إضافة سطر عبوة واحد على الأقل لفاتورة العبوات.",
                    nameof(InvoiceRequest.ContainerLines)));
        }

        if (invoice.ContentType == InvoiceContentType.Items &&
            lines.Count == 0)
        {
            return Failure(
                Error.Validation(
                    "Invoices.ItemLinesRequired",
                    "يجب إضافة سطر صنف واحد على الأقل لفاتورة الأصناف.",
                    nameof(InvoiceRequest.Lines)));
        }

        if (invoice.InvoiceType != InvoiceType.SalesReturn &&
            lines.Any(line =>
                line.SourceInvoiceLineId.HasValue ||
                line.ReturnUnitCost.HasValue))
        {
            return Failure(
                Error.Validation(
                    "Invoices.ReturnCostFieldsNotAllowed",
                    "حقول تكلفة المرتجع مسموحة فقط في فاتورة مرتجع البيع.",
                    nameof(InvoiceLineRequest.ReturnUnitCost)));
        }

        if (lines.Any(line => line.ReturnUnitCost < 0m))
        {
            return Failure(
                Error.Validation(
                    "Invoices.ReturnUnitCostInvalid",
                    "يجب ألا تقل تكلفة وحدة المرتجع عن صفر.",
                    nameof(InvoiceLineRequest.ReturnUnitCost)));
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
                        Error.Validation(
                            "Invoices.InvalidSalesReturnSource",
                            "يجب أن يشير مرتجع البيع إلى سطر بيع أصلي لنفس الشركة والمخزن والصنف.",
                            nameof(InvoiceLineRequest.SourceInvoiceLineId)));
                }
            }
        }

        if (!Enum.IsDefined(typeof(PaymentTerm), invoice.PaymentTerm))
        {
            return Failure(
                Error.Validation(
                    "Invoices.PaymentTermInvalid",
                    "طريقة الدفع غير مدعومة.",
                    nameof(InvoiceRequest.PaymentTerm)));
        }

        if (lines.GroupBy(line => line.ItemId).Any(group => group.Count() > 1))
        {
            return Failure(
                Error.Validation(
                    "Invoices.DuplicateItemIds",
                    "لا يجوز تكرار الصنف في سطور الفاتورة.",
                    nameof(InvoiceRequest.Lines)));
        }

        if (containerLines
            .GroupBy(line => line.ContainerId)
            .Any(group => group.Count() > 1))
        {
            return Failure(
                Error.Validation(
                    "Invoices.DuplicateContainerIds",
                    "لا يجوز تكرار العبوة في سطور الفاتورة.",
                    nameof(InvoiceRequest.ContainerLines)));
        }

        foreach (var line in lines)
        {
            if (!InvoiceAmountRules.TryCalculate(
                    line.Count,
                    line.Weight,
                    line.Price,
                    out _,
                    out _))
            {
                return Failure(
                    Error.Validation(
                        "Invoices.InvalidCalculatedAmounts",
                        "نتيجة الكمية أو الإجمالي تتجاوز الدقة الرقمية المسموحة.",
                        nameof(InvoiceRequest.Lines)));
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
                Error.NotFound(
                    "Invoices.BusinessPartnerNotFound",
                    "لم يتم العثور على العميل أو المورد المحدد.",
                    nameof(InvoiceRequest.BusinessPartnerId)));
        }

        if (!partner.IsActive)
        {
            return Failure(
                Error.Conflict(
                    "Invoices.BusinessPartnerInactive",
                    "لا يمكن استخدام عميل أو مورد غير نشط.",
                    nameof(InvoiceRequest.BusinessPartnerId)));
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
                Error.NotFound(
                    "Invoices.StoreNotFound",
                    "لم يتم العثور على مخزن المنتجات المحدد.",
                    nameof(InvoiceRequest.StoreId)));
        }

        if (!store.IsActive)
        {
            return Failure(
                Error.Conflict(
                    "Invoices.StoreInactive",
                    "لا يمكن استخدام مخزن منتجات غير نشط.",
                    nameof(InvoiceRequest.StoreId)));
        }

        if (invoice.ContentType == InvoiceContentType.Items &&
            store.IsContainerStore)
        {
            return Failure(
                Error.Conflict(
                    "Invoices.ContainerStoreNotAllowed",
                    "يجب اختيار مخزن منتجات وليس مخزن عبوات.",
                    nameof(InvoiceRequest.StoreId)));
        }

        if (invoice.ContentType == InvoiceContentType.Containers &&
            !store.IsContainerStore)
        {
            return Failure(
                Error.Conflict(
                    "Invoices.ContainerStoreRequired",
                    "يجب اختيار مخزن عبوات في فاتورة محتواها عبوات.",
                    nameof(InvoiceRequest.StoreId)));
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
                    Error.NotFound(
                        "Invoices.ContainerStoreNotFound",
                        "لم يتم العثور على مخزن العبوات المحدد.",
                        nameof(InvoiceRequest.ContainerStoreId)));
            }

            if (!containerStore.IsActive)
            {
                return Failure(
                    Error.Conflict(
                        "Invoices.ContainerStoreInactive",
                        "لا يمكن استخدام مخزن عبوات غير نشط.",
                        nameof(InvoiceRequest.ContainerStoreId)));
            }

            if (!containerStore.IsContainerStore ||
                containerStore.BusinessPartnerId != partner.Id)
            {
                return Failure(
                    Error.Conflict(
                        "Invoices.ContainerStorePartnerMismatch",
                        "مخزن العبوات يجب أن يكون مخزن العبوات النشط للعميل أو المورد المحدد.",
                        nameof(InvoiceRequest.ContainerStoreId)));
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
                    Error.NotFound(
                        "Invoices.CountryNotFound",
                        "لم يتم العثور على الدولة المحددة أو أنها غير نشطة.",
                        nameof(InvoiceRequest.CountryId)));
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
                    Error.NotFound(
                        "Invoices.ItemsCategoryNotFound",
                        "لم يتم العثور على تصنيف الأصناف المحدد.",
                        nameof(InvoiceRequest.ItemsCategoryId)));
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
                        Error.Conflict(
                            "Invoices.ItemsCategoryInactive",
                            "لا يمكن استخدام تصنيف أصناف غير نشط.",
                            nameof(InvoiceRequest.ItemsCategoryId)));
                }
            }
        }

        NormalizeDriverValues(invoice);

        if (invoice.ActualDriverId.HasValue &&
            !invoice.DriverId.HasValue)
        {
            return Failure(
                Error.Validation(
                    "Invoices.MainDriverRequired",
                    "يجب تحديد السائق الرئيسي قبل تحديد السائق الفعلي.",
                    nameof(InvoiceRequest.DriverId)));
        }

        if (invoice.UsesExternalDriver &&
            invoice.ActualDriverId.HasValue)
        {
            return Failure(
                Error.Validation(
                    "Invoices.ExternalDriverWithActualDriver",
                    "لا يجوز اختيار سائق فعلي داخلي مع السائق الخارجي.",
                    nameof(InvoiceRequest.ActualDriverId)));
        }

        if (invoice.UsesExternalDriver)
        {
            if (string.IsNullOrWhiteSpace(invoice.ExternalDriverName))
            {
                return Failure(
                    Error.Validation(
                        "Invoices.ExternalDriverNameRequired",
                        "اسم السائق الخارجي مطلوب.",
                        nameof(InvoiceRequest.ExternalDriverName)));
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
                    Error.NotFound(
                        "Invoices.DriverNotFound",
                        "لم يتم العثور على السائق الرئيسي المحدد.",
                        nameof(InvoiceRequest.DriverId)));
            }

            if (!driver.IsActive)
            {
                return Failure(
                    Error.Conflict(
                        "Invoices.DriverInactive",
                        "لا يمكن استخدام سائق رئيسي غير نشط.",
                        nameof(InvoiceRequest.DriverId)));
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
                    Error.NotFound(
                        "Invoices.ActualDriverNotFound",
                        "لم يتم العثور على السائق الفعلي المحدد.",
                        nameof(InvoiceRequest.ActualDriverId)));
            }

            if (!actualDriver.IsActive)
            {
                return Failure(
                    Error.Conflict(
                        "Invoices.ActualDriverInactive",
                        "لا يمكن استخدام سائق فعلي غير نشط.",
                        nameof(InvoiceRequest.ActualDriverId)));
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
                Error.NotFound(
                    "Invoices.ItemNotFound",
                    $"لم يتم العثور على الأصناف: {string.Join(", ", missingItemIds)}.",
                    nameof(InvoiceLineRequest.ItemId)));
        }

        var inactiveItemIds = items
            .Where(item => !item.IsActive)
            .Select(item => item.Id)
            .ToArray();
        if (inactiveItemIds.Length > 0)
        {
            return Failure(
                Error.Conflict(
                    "Invoices.ItemInactive",
                    $"لا يمكن استخدام الأصناف غير النشطة: {string.Join(", ", inactiveItemIds)}.",
                    nameof(InvoiceLineRequest.ItemId)));
        }

        var inactiveUnitItemIds = items
            .Where(item => !item.ItemUnitIsActive)
            .Select(item => item.Id)
            .ToArray();
        if (inactiveUnitItemIds.Length > 0)
        {
            return Failure(
                Error.Conflict(
                    "Invoices.ItemUnitInactive",
                    $"وحدات الأصناف غير النشطة: {string.Join(", ", inactiveUnitItemIds)}.",
                    nameof(InvoiceLineRequest.ItemId)));
        }

        if (containerLines.Count > 0)
        {
            if (invoice.InvoiceType is not (InvoiceType.Sales or
                InvoiceType.SalesReturn))
            {
                return Failure(
                    Error.Conflict(
                        "Invoices.ContainerLinesNotAllowed",
                        "لا يسمح بسطر العبوة إلا في فواتير البيع ومرتجع البيع.",
                        nameof(InvoiceRequest.ContainerLines)));
            }

            if (containerStore is null)
            {
                return Failure(
                    Error.Validation(
                        "Invoices.ContainerStoreRequired",
                        "مخزن العبوات مطلوب عند إضافة سطور العبوات.",
                        nameof(InvoiceRequest.ContainerStoreId)));
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
                    Error.NotFound(
                        "Invoices.ContainerNotAssigned",
                        $"العبوات غير النشطة أو غير المرتبطة بمخزن العميل: {string.Join(", ", missingContainerIds)}.",
                        nameof(InvoiceContainerLineRequest.ContainerId)));
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
            return Error.Validation(
                "Invoices.InvalidWBTotal",
                "يجب أن تكون قيم الميزان غير سالبة وألا يتجاوز مجموع فرق الميزان وخصم الميزان وزن الميزان.",
                nameof(InvoiceRequest.WBWeight));
        }

        if (invoice.DiscountAmount < 0m ||
            !InvoiceAmountRules.IsValidMoney(invoice.DiscountAmount) ||
            invoice.DiscountAmount > invoice.Subtotal)
        {
            return Error.Validation(
                "Invoices.InvalidDiscountAmount",
                "قيمة الخصم يجب ألا تكون سالبة ولا يمكن أن تتجاوز إجمالي سطور الفاتورة.",
                nameof(InvoiceRequest.DiscountAmount));
        }

        if (!InvoiceAmountRules.IsValidMoney(invoice.Subtotal) ||
            !InvoiceAmountRules.IsValidMoney(invoice.Total))
        {
            return Error.Validation(
                "Invoices.InvalidCalculatedAmounts",
                "نتيجة المبالغ تتجاوز الدقة الرقمية المسموحة.",
                nameof(InvoiceRequest.Lines));
        }

        if (invoice.PaidAmount < 0m ||
            !InvoiceAmountRules.IsValidMoney(invoice.PaidAmount) ||
            invoice.PaidAmount > invoice.Total)
        {
            return Error.Validation(
                "Invoices.InvalidPaidAmount",
                "المبلغ المدفوع يجب ألا يكون سالبًا ولا يمكن أن يتجاوز صافي الفاتورة.",
                nameof(InvoiceRequest.PaidAmount));
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
                Error.Validation(
                    "Invoices.CashboxAmountTooSmall",
                    "قيمة الدفعة بعد التحويل أقل من أصغر قيمة يمكن تسجيلها في صندوق النقدية.",
                    nameof(InvoiceRequest.PaidAmount)));
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
            return Error.Validation(
                "Invoices.InvalidCurrentInvoiceReference",
                "يجب توفير رقم تعريف الفاتورة ورقمها معًا عند التعديل.");
        }

        var stockLines = new List<InventoryStockLine>(lines.Count);
        foreach (var line in lines)
        {
            if (!InvoiceAmountRules.TryCalculate(
                    line.Count,
                    line.Weight,
                    0m,
                    out var quantity,
                    out _))
            {
                return Error.Validation(
                    "Invoices.InvalidCalculatedAmounts",
                    "نتيجة الكمية تتجاوز الدقة الرقمية المسموحة.",
                    nameof(InvoiceRequest.Lines));
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
            return Error.Validation(
                "Invoices.InvalidCurrentInvoiceReference",
                "يجب توفير رقم تعريف الفاتورة ورقمها معًا عند التعديل.");
        }

        var isUpdate = hasCurrentInvoiceId;
        var currentReferenceNumber = currentInvoiceNumber ?? string.Empty;

        // 2. Calculate and aggregate the requested quantity for each item.
        var isInbound = InvoiceMovementRules.IsInbound(invoice.InvoiceType);
        var requestedByItem = new Dictionary<int, decimal>();
        foreach (var line in lines)
        {
            if (!InvoiceAmountRules.TryCalculate(
                    line.Count,
                    line.Weight,
                    0m,
                    out var quantity,
                    out _))
            {
                return Error.Validation(
                    "Invoices.InvalidCalculatedAmounts",
                    "نتيجة الكمية تتجاوز الدقة الرقمية المسموحة.",
                    nameof(InvoiceRequest.Lines));
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
                    return Error.Conflict(
                        "Inventory.InsufficientStock",
                        $"الكمية المتاحة للصنف {itemName} (رقم {itemId}) في المخزن " +
                        $"{storeId} بتاريخ " +
                        $"{stockEvent.Date:yyyy-MM-dd} هي " +
                        $"{availableBeforeMovement}، ولا يمكن صرف " +
                        $"{stockEvent.QuantityOut}.",
                        nameof(InvoiceRequest.Lines));
                }

                // Removing, moving, reducing, or re-dating the old invoice
                // causes a later stored movement to make the balance negative.
                return Error.Conflict(
                    "Inventory.HistoricalStockConflict",
                    $"{operationDescription} بتاريخ " +
                    $"{invoice.InvoiceDate:yyyy-MM-dd} سيؤدي إلى " +
                    $"عجز في رصيد الصنف {itemName} (رقم {itemId}) في المخزن {storeId} " +
                    $"بتاريخ {stockEvent.Date:yyyy-MM-dd}. الرصيد قبل " +
                    $"حركة الصرف هو {availableBeforeMovement}، وكمية " +
                    $"الحركة هي {stockEvent.QuantityOut}.",
                    nameof(InvoiceRequest.Lines));
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
