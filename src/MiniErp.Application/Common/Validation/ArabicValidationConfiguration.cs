using System.Globalization;
using FluentValidation;
using FluentValidation.Resources;

namespace MiniErp.Application.Common.Validation;

public static class ArabicValidationConfiguration
{
    private static readonly IReadOnlyDictionary<string, string> DisplayNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DocumentDate"] = "تاريخ المستند",
            ["DocumentNumber"] = "رقم المستند",
            ["TransferDate"] = "تاريخ التحويل",
            ["SourceStoreId"] = "مخزن المصدر",
            ["DestinationStoreId"] = "مخزن الوجهة",
            ["ItemId"] = "الصنف",
            ["InvoiceNumber"] = "رقم الفاتورة",
            ["Lines"] = "سطور المستند",
            ["Notes"] = "الملاحظات",
            ["Quantity"] = "الكمية",
            ["Count"] = "العدد",
            ["Weight"] = "الوزن",
            ["Price"] = "السعر",
            ["Total"] = "الإجمالي",
            ["RowVersion"] = "إصدار السجل",
            ["Address"] = "العنوان",
            ["Amount"] = "المبلغ",
            ["ArabicName"] = "الاسم بالعربية",
            ["BalanceType"] = "نوع رصيد الشريك",
            ["BusinessPartnerId"] = "العميل أو المورد",
            ["CashboxId"] = "صندوق النقدية",
            ["CashMovementTypeId"] = "نوع الحركة النقدية",
            ["Code"] = "الكود",
            ["CommercialRegister"] = "السجل التجاري",
            ["CompanyId"] = "الشركة",
            ["CompanyIds"] = "الشركات",
            ["ContainerId"] = "العبوة",
            ["ContainerIds"] = "العبوات",
            ["CreditLimit"] = "حد الائتمان",
            ["Currency"] = "العملة",
            ["Description"] = "الوصف",
            ["Direction"] = "الاتجاه",
            ["DriverId"] = "السائق",
            ["DriverTripId"] = "رحلة السائق",
            ["Email"] = "البريد الإلكتروني",
            ["FirstName"] = "الاسم الأول",
            ["IsContainerStore"] = "نوع المخزن",
            ["ItemUnitId"] = "وحدة الصنف",
            ["LastName"] = "اسم العائلة",
            ["LicenseNumber"] = "رقم الرخصة",
            ["ManagerName"] = "اسم المدير",
            ["Name"] = "الاسم",
            ["NationalId"] = "الرقم القومي",
            ["PageNumber"] = "رقم الصفحة",
            ["PageSize"] = "حجم الصفحة",
            ["ForPartner"] = "مخصص للعميل أو المورد",
            ["PartyType"] = "نوع الطرف",
            ["PaymentTerm"] = "شرط السداد",
            ["PartnerInvoiceNo"] = "رقم الفاتورة لدى الشريك",
            ["ItemsCategoryId"] = "تصنيف الأصناف",
            ["DiscountAmount"] = "قيمة الخصم",
            ["PaidAmount"] = "المبلغ المدفوع",
            ["Password"] = "كلمة المرور",
            ["PhoneNumber"] = "رقم الهاتف",
            ["RefreshToken"] = "رمز التحديث",
            ["Roles"] = "الأدوار",
            ["SelectionToken"] = "رمز اختيار الشركة",
            ["StoreId"] = "المخزن",
            ["TaxNumber"] = "الرقم الضريبي",
            ["UserName"] = "اسم المستخدم",
            ["VoucherDate"] = "تاريخ السند",
            ["VoucherNumber"] = "رقم السند",
            ["ExternalPartyName"] = "اسم الطرف الخارجي",
            ["ReferenceNumber"] = "الرقم المرجعي",
            ["Cost"] = "تكلفة الرحلة",
            ["Items"] = "العناصر",
            ["CountDate"] = "تاريخ الجرد",
            ["PhysicalQuantity"] = "الكمية الفعلية",
            ["Reason"] = "السبب"
        };

    public static void Configure()
    {
        ValidatorOptions.Global.LanguageManager = new ArabicLanguageManager
        {
            Culture = CultureInfo.GetCultureInfo("ar")
        };

        ValidatorOptions.Global.DisplayNameResolver = (_, member, _) =>
            member is not null && DisplayNames.TryGetValue(member.Name, out var name)
                ? name
                : member?.Name;
    }

    private sealed class ArabicLanguageManager : LanguageManager
    {
        public ArabicLanguageManager()
        {
            AddTranslation("ar", "NotNullValidator", "حقل {PropertyName} مطلوب.");
            AddTranslation("ar", "NotEmptyValidator", "حقل {PropertyName} مطلوب.");
            AddTranslation("ar", "NullValidator", "يجب ترك حقل {PropertyName} فارغًا.");
            AddTranslation(
                "ar",
                "GreaterThanValidator",
                "يجب أن تكون قيمة {PropertyName} أكبر من {ComparisonValue}.");
            AddTranslation(
                "ar",
                "GreaterThanOrEqualValidator",
                "يجب ألا تقل قيمة {PropertyName} عن {ComparisonValue}.");
            AddTranslation(
                "ar",
                "InclusiveBetweenValidator",
                "يجب أن تكون قيمة {PropertyName} بين {From} و{To}.");
            AddTranslation(
                "ar",
                "MaximumLengthValidator",
                "يجب ألا يتجاوز طول {PropertyName} عدد {MaxLength} حرفًا.");
            AddTranslation(
                "ar",
                "MinimumLengthValidator",
                "يجب ألا يقل طول {PropertyName} عن {MinLength} أحرف.");
            AddTranslation(
                "ar",
                "EmailValidator",
                "صيغة {PropertyName} غير صحيحة.");
            AddTranslation(
                "ar",
                "EnumValidator",
                "قيمة {PropertyName} غير مدعومة.");
            AddTranslation(
                "ar",
                "PrecisionScaleValidator",
                "تنسيق {PropertyName} غير صحيح؛ الحد الأقصى {ExpectedPrecision} رقمًا منها {ExpectedScale} أرقام عشرية.");
            AddTranslation(
                "ar",
                "PredicateValidator",
                "قيمة {PropertyName} غير صحيحة.");
        }
    }
}
