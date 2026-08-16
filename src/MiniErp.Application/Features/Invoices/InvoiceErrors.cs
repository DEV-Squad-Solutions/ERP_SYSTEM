using MiniErp.Application.Common.Results;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.Invoices;

public enum InvoiceFilterErrorKind
{
    PriceStatus,
    BusinessPartnerId,
    CountryId,
    StoreId,
    DriverId,
    DateRange
}

public enum InvoiceCalculationErrorKind
{
    LineQuantityOrTotal,
    Totals,
    Quantity
}

public enum InvoiceContainerStoreRequirement
{
    ContainerInvoice,
    ContainerLines
}

public static class InvoiceErrors
{
    public static Error InvoiceNumberFilterInvalid() =>
        Error.Validation(
            "Invoices.InvoiceNumberFilterInvalid",
            "رقم الفاتورة في البحث طويل. الحد الأقصى 100 حرف.",
            nameof(InvoiceFilterRequest.InvoiceNumber));

    public static Error InvoiceTypeInvalid(string fieldName) =>
        Error.Validation(
            "Invoices.InvoiceTypeInvalid",
            "نوع الفاتورة غير صحيح.",
            fieldName);

    public static Error PaymentTermInvalid(string fieldName) =>
        Error.Validation(
            "Invoices.PaymentTermInvalid",
            "شرط السداد غير صحيح.",
            fieldName);

    public static Error InvalidFilter(InvoiceFilterErrorKind kind)
    {
        var (fieldName, description) = kind switch
        {
            InvoiceFilterErrorKind.PriceStatus => (
                nameof(InvoiceFilterRequest.PriceStatus),
                "طريقة تسعير الأصناف غير صحيحة."),
            InvoiceFilterErrorKind.BusinessPartnerId => (
                nameof(InvoiceFilterRequest.BusinessPartnerId),
                "أدخل رقم طرف أكبر من صفر."),
            InvoiceFilterErrorKind.CountryId => (
                nameof(InvoiceFilterRequest.CountryId),
                "أدخل رقم دولة أكبر من صفر."),
            InvoiceFilterErrorKind.StoreId => (
                nameof(InvoiceFilterRequest.StoreId),
                "أدخل رقم مخزن أكبر من صفر."),
            InvoiceFilterErrorKind.DriverId => (
                nameof(InvoiceFilterRequest.DriverId),
                "أدخل رقم سائق أكبر من صفر."),
            InvoiceFilterErrorKind.DateRange => (
                nameof(InvoiceFilterRequest.ToDate),
                "تاريخ النهاية يجب أن يكون بعد تاريخ البداية."),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

        return Error.Validation("Invoices.InvalidFilter", description, fieldName);
    }

    public static Error InvoiceNumberInvalid() =>
        Error.Validation(
            "Invoices.InvoiceNumberInvalid",
            "أدخل رقم الفاتورة (حتى 100 حرف).",
            nameof(InvoiceRequest.InvoiceNumber));

    public static Error ContentTypeInvalid() =>
        Error.Validation(
            "Invoices.ContentTypeInvalid",
            "نوع محتوى الفاتورة غير صحيح.",
            nameof(InvoiceRequest.ContentType));

    public static Error ItemLinesNotAllowedForContainerInvoice() =>
        Error.Validation(
            "Invoices.ItemLinesNotAllowedForContainerInvoice",
            "هذه الفاتورة للعبوات. احذف سطور الأصناف.",
            nameof(InvoiceRequest.Lines));

    public static Error ContainerLinesRequired() =>
        Error.Validation(
            "Invoices.ContainerLinesRequired",
            "أضف عبوة واحدة على الأقل.",
            nameof(InvoiceRequest.ContainerLines));

    public static Error ItemLinesRequired() =>
        Error.Validation(
            "Invoices.ItemLinesRequired",
            "أضف صنفًا واحدًا على الأقل.",
            nameof(InvoiceRequest.Lines));

    public static Error ReturnCostFieldsNotAllowed() =>
        Error.Validation(
            "Invoices.ReturnCostFieldsNotAllowed",
            "تكلفة وحدة المرتجع تستخدم فقط مع مرتجع البيع. ربط الفاتورة الأصلية متاح لمرتجع البيع ومرتجع الشراء.",
            nameof(InvoiceLineRequest.ReturnUnitCost));

    public static Error ReturnUnitCostInvalid() =>
        Error.Validation(
            "Invoices.ReturnUnitCostInvalid",
            "تكلفة وحدة المرتجع يجب أن تكون صفرًا أو أكثر.",
            nameof(InvoiceLineRequest.ReturnUnitCost));

    public static Error InvalidSalesReturnSource() =>
        Error.Validation(
            "Invoices.InvalidSalesReturnSource",
            "اختر سطر بيع أصلي من نفس الشركة والمخزن والصنف.",
            nameof(InvoiceLineRequest.SourceInvoiceLineId));

    public static Error InvalidReturnSource() =>
        Error.Validation(
            "Invoices.InvalidReturnSource",
            "الفاتورة الأصلية المختارة لا تناسب هذا المرتجع. اختر فاتورة لنفس الطرف والمخزن والصنف، ويجب ألا يكون تاريخها بعد تاريخ المرتجع.",
            nameof(InvoiceLineRequest.SourceInvoiceLineId));

    public static Error ReturnQuantityExceedsAvailable(
        int sourceInvoiceLineId,
        decimal availableQuantity) =>
        Error.Conflict(
            "Invoices.ReturnQuantityExceedsAvailable",
            $"الكمية المطلوبة أكبر من الكمية المتاحة للمرتجع. المتاح من السطر {sourceInvoiceLineId} هو {availableQuantity}.",
            nameof(InvoiceLineRequest.SourceInvoiceLineId));

    public static Error ReturnLinesMustUseOneSourceInvoice() =>
        Error.Validation(
            "Invoices.ReturnLinesMustUseOneSourceInvoice",
            "اختر كل أصناف المرتجع من فاتورة أصلية واحدة. لا تخلط سطوراً مرتبطة وغير مرتبطة في نفس المرتجع.",
            nameof(InvoiceLineRequest.SourceInvoiceLineId));

    public static Error ReturnSourcePartnerInvalid() =>
        Error.Validation(
            "Invoices.ReturnSourcePartnerInvalid",
            "اختر العميل أو المورد أولاً.",
            nameof(InvoiceReturnSourceFilterRequest.BusinessPartnerId));

    public static Error ReturnSourceStoreInvalid() =>
        Error.Validation(
            "Invoices.ReturnSourceStoreInvalid",
            "اختر المخزن أولاً.",
            nameof(InvoiceReturnSourceFilterRequest.StoreId));

    public static Error ReturnSourceTypeInvalid() =>
        Error.Validation(
            "Invoices.ReturnSourceTypeInvalid",
            "نوع المرتجع يجب أن يكون مرتجع بيع أو مرتجع شراء.",
            nameof(InvoiceReturnSourceFilterRequest.ReturnType));

    public static Error ReturnSourceDateRequired() =>
        Error.Validation(
            "Invoices.ReturnSourceDateRequired",
            "اختر تاريخ المرتجع أولاً.",
            nameof(InvoiceReturnSourceFilterRequest.AsOfDate));

    public static Error ReturnSourceSearchInvalid() =>
        Error.Validation(
            "Invoices.ReturnSourceSearchInvalid",
            "نص البحث طويل. الحد الأقصى 100 حرف.",
            nameof(InvoiceReturnSourceFilterRequest.Search));

    public static Error CurrentReturnInvoiceInvalid() =>
        Error.Validation(
            "Invoices.CurrentReturnInvoiceInvalid",
            "رقم فاتورة المرتجع الحالية غير صحيح.",
            nameof(InvoiceReturnSourceFilterRequest.CurrentReturnInvoiceId));

    public static Error DuplicateItemIds() =>
        Error.Validation(
            "Invoices.DuplicateItemIds",
            "لا تكرر الصنف في الفاتورة.",
            nameof(InvoiceRequest.Lines));

    public static Error DuplicateContainerIds() =>
        Error.Validation(
            "Invoices.DuplicateContainerIds",
            "لا تكرر العبوة في الفاتورة.",
            nameof(InvoiceRequest.ContainerLines));

    public static Error InvalidCalculatedAmounts(InvoiceCalculationErrorKind kind) =>
        Error.Validation(
            "Invoices.InvalidCalculatedAmounts",
            kind switch
            {
                InvoiceCalculationErrorKind.LineQuantityOrTotal =>
                    "قيمة الكمية أو الإجمالي أكبر من الدقة المسموحة.",
                InvoiceCalculationErrorKind.Totals =>
                    "قيمة المبالغ أكبر من الدقة المسموحة.",
                InvoiceCalculationErrorKind.Quantity =>
                    "قيمة الكمية أكبر من الدقة المسموحة.",
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
            },
            nameof(InvoiceRequest.Lines));

    public static Error BusinessPartnerNotFound(int id) =>
        Error.NotFound(
            "Invoices.BusinessPartnerNotFound",
            $"العميل أو المورد رقم {id} غير موجود.",
            nameof(InvoiceRequest.BusinessPartnerId));

    public static Error BusinessPartnerInactive() =>
        Error.Conflict(
            "Invoices.BusinessPartnerInactive",
            "العميل أو المورد غير نشط.",
            nameof(InvoiceRequest.BusinessPartnerId));

    public static Error ContainerStoreRequired(
        InvoiceContainerStoreRequirement requirement) =>
        requirement == InvoiceContainerStoreRequirement.ContainerInvoice
            ? Error.Conflict(
                "Invoices.ContainerStoreRequired",
                "اختر مخزن عبوات لهذه الفاتورة.",
                nameof(InvoiceRequest.StoreId))
            : Error.Validation(
                "Invoices.ContainerStoreRequired",
                "اختر مخزن العبوات عند إضافة سطر عبوة.",
                nameof(InvoiceRequest.ContainerStoreId));

    public static Error ContainerStoreNotFound(int id) =>
        Error.NotFound(
            "Invoices.ContainerStoreNotFound",
            $"مخزن العبوات رقم {id} غير موجود.",
            nameof(InvoiceRequest.ContainerStoreId));

    public static Error ContainerStoreInactive() =>
        Error.Conflict(
            "Invoices.ContainerStoreInactive",
            "مخزن العبوات غير نشط.",
            nameof(InvoiceRequest.ContainerStoreId));

    public static Error ContainerStorePartnerMismatch() =>
        Error.Conflict(
            "Invoices.ContainerStorePartnerMismatch",
            "اختر مخزن العبوات التابع لهذا العميل أو المورد.",
            nameof(InvoiceRequest.ContainerStoreId));

    public static Error CountryNotFound(int id) =>
        Error.NotFound(
            "Invoices.CountryNotFound",
            $"الدولة رقم {id} غير موجودة أو غير نشطة.",
            nameof(InvoiceRequest.CountryId));

    public static Error ItemsCategoryNotFound(int id) =>
        Error.NotFound(
            "Invoices.ItemsCategoryNotFound",
            $"تصنيف الأصناف رقم {id} غير موجود.",
            nameof(InvoiceRequest.ItemsCategoryId));

    public static Error ItemsCategoryInactive() =>
        Error.Conflict(
            "Invoices.ItemsCategoryInactive",
            "تصنيف الأصناف غير نشط.",
            nameof(InvoiceRequest.ItemsCategoryId));

    public static Error MainDriverRequired() =>
        Error.Validation(
            "Invoices.MainDriverRequired",
            "اختر السائق الرئيسي أولًا.",
            nameof(InvoiceRequest.DriverId));

    public static Error ExternalDriverWithActualDriver() =>
        Error.Validation(
            "Invoices.ExternalDriverWithActualDriver",
            "لا يمكن إرسال السائق الفعلي مع وضع السائق الخارجي.",
            nameof(InvoiceRequest.ActualDriverId));

    public static Error ExternalDriverNameRequired() =>
        Error.Validation(
            "Invoices.ExternalDriverNameRequired",
            "أدخل اسم السائق الخارجي.",
            nameof(InvoiceRequest.ExternalDriverName));

    public static Error DriverNotFound(int id) =>
        Error.NotFound(
            "Invoices.DriverNotFound",
            $"السائق الرئيسي رقم {id} غير موجود.",
            nameof(InvoiceRequest.DriverId));

    public static Error DriverInactive() =>
        Error.Conflict(
            "Invoices.DriverInactive",
            "السائق الرئيسي غير نشط.",
            nameof(InvoiceRequest.DriverId));

    public static Error ContainerLinesNotAllowed() =>
        Error.Conflict(
            "Invoices.ContainerLinesNotAllowed",
            "سطور العبوات متاحة فقط لفواتير البيع ومرتجع البيع.",
            nameof(InvoiceRequest.ContainerLines));

    public static Error ContainerNotAssigned(IEnumerable<int> ids) =>
        Error.NotFound(
            "Invoices.ContainerNotAssigned",
            $"العبوات التالية غير نشطة أو ليست في مخزن العميل: {string.Join(", ", ids)}.",
            nameof(InvoiceContainerLineRequest.ContainerId));

    public static Error InvalidWBTotal(
        string fieldName = nameof(InvoiceRequest.WBWeight)) =>
        Error.Validation(
            "Invoices.InvalidWBTotal",
            "راجع أوزان الميزان. أدخل قيمًا غير سالبة ولا تتجاوز الوزن الكلي.",
            fieldName);

    public static Error WBTotalDoesNotMatchItemQuantity(
        decimal wbTotal,
        decimal totalItemQuantity,
        string fieldName = nameof(InvoiceRequest.WBTotal)) =>
        Error.Validation(
            "Invoices.WBTotalDoesNotMatchItemQuantity",
            $"صافي وزن الميزان ({wbTotal}) يجب أن يساوي مجموع كميات الأصناف ({totalItemQuantity}).",
            fieldName);

    public static Error InvalidDiscountAmount() =>
        Error.Validation(
            "Invoices.InvalidDiscountAmount",
            "الخصم يجب أن يكون صفرًا أو أقل من إجمالي الفاتورة.",
            nameof(InvoiceRequest.DiscountAmount));

    public static Error InvalidPaidAmount() =>
        Error.Validation(
            "Invoices.InvalidPaidAmount",
            "المبلغ المدفوع يجب أن يكون صفرًا أو أقل من صافي الفاتورة.",
            nameof(InvoiceRequest.PaidAmount));

    public static Error CashboxAmountTooSmall() =>
        Error.Validation(
            "Invoices.CashboxAmountTooSmall",
            "المبلغ بعد التحويل أقل من الحد الأدنى للصندوق.",
            nameof(InvoiceRequest.PaidAmount));

    public static Error InvalidCurrentInvoiceReference() =>
        Error.Validation(
            "Invoices.InvalidCurrentInvoiceReference",
            "أدخل رقم تعريف الفاتورة ورقمها معًا.");

    public static Error RowVersionRequired() =>
        Error.Validation(
            "Invoices.RowVersionRequired",
            "إصدار الفاتورة مطلوب للتعديل.",
            nameof(InvoiceUpdateRequest.RowVersion));

    public static Error ItemBalanceStoreInvalid() =>
        Error.Validation(
            "Invoices.ItemBalanceStoreInvalid",
            "رقم المخزن غير صحيح.",
            nameof(InvoiceItemBalanceResponse.StoreId));

    public static Error ItemBalanceItemInvalid() =>
        Error.Validation(
            "Invoices.ItemBalanceItemInvalid",
            "رقم الصنف غير صحيح.",
            nameof(InvoiceItemBalanceResponse.ItemId));

    public static Error ItemBalanceDateRequired() =>
        Error.Validation(
            "Invoices.ItemBalanceDateRequired",
            "اختر تاريخ الفاتورة لحساب الرصيد.",
            nameof(InvoiceItemBalanceResponse.AsOfDate));

    public static Error ItemBalanceInvoiceInvalid() =>
        Error.Validation(
            "Invoices.ItemBalanceInvoiceInvalid",
            "رقم الفاتورة المستبعدة غير صحيح.",
            "InvoiceId");

    public static Error StoreNotFound(int id) =>
        Error.NotFound(
            "Invoices.StoreNotFound",
            $"المخزن رقم {id} غير موجود.",
            nameof(InvoiceRequest.StoreId));

    public static Error StoreInactive() =>
        Error.Conflict(
            "Invoices.StoreInactive",
            "المخزن غير نشط.",
            nameof(InvoiceRequest.StoreId));

    public static Error ContainerStoreNotAllowed() =>
        Error.Conflict(
            "Invoices.ContainerStoreNotAllowed",
            "اختر مخزن المنتجات، وليس مخزن العبوات.",
            nameof(InvoiceRequest.StoreId));

    public static Error ItemNotFound(IEnumerable<int> ids) =>
        Error.NotFound(
            "Invoices.ItemNotFound",
            $"الأصناف التالية غير موجودة: {string.Join(", ", ids)}.",
            nameof(InvoiceLineRequest.ItemId));

    public static Error ItemInactive(IEnumerable<int> ids) =>
        Error.Conflict(
            "Invoices.ItemInactive",
            $"الأصناف التالية غير نشطة: {string.Join(", ", ids)}.",
            nameof(InvoiceLineRequest.ItemId));

    public static Error ItemUnitInactive(IEnumerable<int> ids) =>
        Error.Conflict(
            "Invoices.ItemUnitInactive",
            $"وحدات قياس الأصناف التالية غير نشطة: {string.Join(", ", ids)}.",
            nameof(InvoiceLineRequest.ItemId));

    public static Error InvalidId() =>
        Error.Validation(
            "Invoices.InvalidId",
            "رقم الفاتورة غير صحيح.");

    public static Error NotFound(int id) =>
        Error.NotFound(
            "Invoices.NotFound",
            $"الفاتورة رقم {id} غير موجودة.");

    public static Error Concurrency() =>
        Error.Conflict(
            "Invoices.Concurrency",
            "الفاتورة تغيّرت من مستخدم آخر. أعد تحميلها وحاول مرة أخرى.");

    public static Error DriverTripHasCashVouchers() =>
        Error.Conflict(
            "Invoices.DriverTripHasCashVouchers",
            "لا يمكن تعديل أو حذف الفاتورة لأن رحلة السائق مرتبطة بسندات نقدية.");

    public static Error LinkedSalesReturnsExist() =>
        Error.Conflict(
            "Invoices.LinkedSalesReturnsExist",
            "لا يمكن تعديل أو حذف الفاتورة لوجود مرتجع مرتبط بها.");

    public static Error CashInvoiceMustBeFullyPaid() =>
        Error.Validation(
            "Invoices.CashInvoiceMustBeFullyPaid",
            "الفاتورة النقدية يجب دفعها كاملة.");

    public static Error CreditInvoiceCannotBeFullyPaid() =>
        Error.Validation(
            "Invoices.CreditInvoiceCannotBeFullyPaid",
            "الفاتورة الآجلة لا تُدفع بالكامل. استخدم فاتورة نقدية.");

    public static Error CashboxRequiredForPayment() =>
        Error.Validation(
            "Invoices.CashboxRequiredForPayment",
            "اختر صندوق النقدية لتسجيل الدفعة.",
            nameof(InvoiceRequest.CashboxId));

    public static Error PaymentReferencesNotAllowed() =>
        Error.Validation(
            "Invoices.PaymentReferencesNotAllowed",
            "لا تختر صندوقًا بدون إدخال مبلغ مدفوع.");

    public static Error CashboxNotFound(int id) =>
        Error.NotFound(
            "Invoices.CashboxNotFound",
            $"صندوق النقدية رقم {id} غير موجود.",
            nameof(InvoiceRequest.CashboxId));

    public static Error CashboxInactive() =>
        Error.Conflict(
            "Invoices.CashboxInactive",
            "صندوق النقدية غير نشط.");

    public static Error PaymentCurrencyMismatch() =>
        Error.Conflict(
            "Invoices.PaymentCurrencyMismatch",
            "عملة الصندوق مختلفة عن عملة الفاتورة.",
            nameof(InvoiceRequest.CashboxId));

    public static Error DefaultCashMovementTypeNotFound(
        InvoiceType invoiceType) =>
        Error.Conflict(
            "Invoices.DefaultCashMovementTypeNotFound",
            $"لا توجد حركة افتراضية لفاتورة {GetInvoiceTypeName(invoiceType)}. " +
            "عيّنها من شاشة أنواع القبض والصرف.");

    private static string GetInvoiceTypeName(InvoiceType invoiceType) =>
        invoiceType switch
        {
            InvoiceType.Sales => "البيع",
            InvoiceType.Purchase => "الشراء",
            InvoiceType.SalesReturn => "مرتجع البيع",
            InvoiceType.PurchaseReturn => "مرتجع الشراء",
            _ => "هذا النوع"
        };

    public static Error InsufficientCashboxBalance(int cashboxId) =>
        Error.Conflict(
            "Invoices.InsufficientCashboxBalance",
            $"رصيد صندوق النقدية رقم {cashboxId} لا يكفي لهذه الدفعة.");

}
