using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.PartnerItemReports;

public static class PartnerItemReportErrors
{
    public static Error BusinessPartnerRequired() =>
        Error.Validation(
            "PartnerItemReports.BusinessPartnerRequired",
            "يجب اختيار شريك.",
            nameof(PartnerItemReportFilterRequest.BusinessPartnerId));

    public static Error ItemInvalid() =>
        Error.Validation(
            "PartnerItemReports.ItemInvalid",
            "الصنف المحدد غير صالح.",
            nameof(PartnerItemReportFilterRequest.ItemId));

    public static Error CountryInvalid() =>
        Error.Validation(
            "PartnerItemReports.CountryInvalid",
            "الدولة المحددة غير صالحة.",
            nameof(PartnerItemReportFilterRequest.CountryId));

    public static Error InvalidDateRange() =>
        Error.Validation(
            "PartnerItemReports.InvalidDateRange",
            "تاريخ البداية لا يمكن أن يكون بعد تاريخ النهاية.",
            nameof(PartnerItemReportFilterRequest.ToDate));

    public static Error SearchTooLong() =>
        Error.Validation(
            "PartnerItemReports.SearchTooLong",
            "نص البحث طويل جداً.",
            nameof(PartnerItemReportFilterRequest.Search));

    public static Error InvalidMovementType() =>
        Error.Validation(
            "PartnerItemReports.InvalidMovementType",
            "نوع الحركة يجب أن يكون بيعاً أو شراءً.",
            nameof(PartnerItemReportFilterRequest.MovementType));

    public static Error BusinessPartnerNotFound() =>
        Error.NotFound(
            "PartnerItemReports.BusinessPartnerNotFound",
            "الشريك المحدد غير موجود أو لا ينتمي إلى الشركة الحالية.",
            nameof(PartnerItemReportFilterRequest.BusinessPartnerId));

    public static Error ItemNotFound() =>
        Error.NotFound(
            "PartnerItemReports.ItemNotFound",
            "الصنف المحدد غير موجود أو لا ينتمي إلى الشركة الحالية.",
            nameof(PartnerItemReportFilterRequest.ItemId));
}
