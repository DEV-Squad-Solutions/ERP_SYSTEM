using FluentValidation;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.CashVouchers;

public sealed class CashVoucherRequestValidator
    : AbstractValidator<CashVoucherRequest>
{
    public CashVoucherRequestValidator()
    {
        RuleFor(request => request.VoucherDate)
            .Must(date => date != default)
            .WithMessage("تاريخ سند النقدية مطلوب.");

        RuleFor(request => request.Direction)
            .IsInEnum();

        RuleFor(request => request.CashboxId)
            .GreaterThan(0);

        RuleFor(request => request.Amount)
            .GreaterThan(0)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true);

        RuleFor(request => request.Description)
            .MaximumLength(CashVoucherRequest.DescriptionMaximumLength);
    }
}

public sealed class CashVoucherUpdateRequestValidator
    : AbstractValidator<CashVoucherUpdateRequest>
{
    public CashVoucherUpdateRequestValidator()
    {
        RuleFor(request => request.VoucherDate)
            .Must(date => date != default)
            .WithMessage("تاريخ سند النقدية مطلوب.");

        RuleFor(request => request.Direction)
            .IsInEnum();

        RuleFor(request => request.CashboxId)
            .NotNull()
            .GreaterThan(0)
            .WithMessage("اختر صندوق النقدية لاستكمال السند.");

        RuleFor(request => request.CashMovementTypeId)
            .GreaterThan(0)
            .When(request => request.CashMovementTypeId.HasValue);

        RuleFor(request => request.AccountId)
            .GreaterThan(0)
            .When(request => request.AccountId.HasValue);

        RuleFor(request => request.EmployeeId)
            .GreaterThan(0)
            .When(request => request.EmployeeId.HasValue);

        RuleFor(request => request.BusinessPartnerId)
            .GreaterThan(0)
            .When(request => request.BusinessPartnerId.HasValue);

        RuleFor(request => request.DriverId)
            .GreaterThan(0)
            .When(request => request.DriverId.HasValue);

        RuleFor(request => request.DriverTripId)
            .GreaterThan(0)
            .When(request => request.DriverTripId.HasValue);
        RuleFor(request => request)
            .Must(request =>
                !request.DriverTripId.HasValue || request.DriverId.HasValue)
            .WithMessage("اختر السائق قبل اختيار الرحلة.")
            .WithName(nameof(CashVoucherUpdateRequest.DriverTripId));

        RuleFor(request => request.ExternalPartyName)
            .MaximumLength(
                CashVoucherRequest.ExternalPartyNameMaximumLength)
            .Must(name => name is null || !string.IsNullOrWhiteSpace(name))
            .WithMessage("اسم الطرف الخارجي لا يمكن أن يكون فارغاً.");

        RuleFor(request => request)
            .Must(HasExactlyOnePostingTarget)
            .WithMessage(
                "اختر طرفاً واحداً أو حساب مصروف أو إيراد واحداً للسند.")
            .OverridePropertyName(
                nameof(CashVoucherUpdateRequest.EmployeeId));

        RuleFor(request => request.Amount)
            .GreaterThan(0)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true);

        RuleFor(request => request.ReferenceNumber)
            .MaximumLength(
                CashVoucherRequest.ReferenceNumberMaximumLength);
        RuleFor(request => request.Description)
            .MaximumLength(
                CashVoucherRequest.DescriptionMaximumLength);
        RuleFor(request => request.Notes)
            .MaximumLength(CashVoucherRequest.NotesMaximumLength);

        RuleFor(request => request.ExchangeRate)
            .Must(rate =>
                !rate.HasValue ||
                ExchangeRateRules.IsValidRate(rate.Value))
            .WithMessage("يجب أن يكون سعر صرف سند النقدية أكبر من صفر.");

        RuleFor(request => request.RowVersion)
            .NotNull()
            .Must(rowVersion => rowVersion is { Length: 8 })
            .WithMessage(
                "يجب إرسال إصدار سند النقدية الحالي للتعديل.");
    }

    private static bool HasExactlyOnePostingTarget(
        CashVoucherUpdateRequest request)
    {
        var selectedTargetCount =
            (request.AccountId.HasValue ? 1 : 0) +
            (request.EmployeeId.HasValue ? 1 : 0) +
            (request.BusinessPartnerId.HasValue ? 1 : 0) +
            (request.DriverId.HasValue ? 1 : 0) +
            (!string.IsNullOrWhiteSpace(request.ExternalPartyName) ? 1 : 0);

        return selectedTargetCount == 1;
    }
}

public sealed class CashVoucherBulkVoucherRequestValidator
    : AbstractValidator<CashVoucherBulkVoucherRequest>
{
    public CashVoucherBulkVoucherRequestValidator()
    {
        RuleFor(request => request.VoucherDate)
            .Must(date => date != default)
            .WithMessage("تاريخ سند النقدية مطلوب.");

        RuleFor(request => request.Direction).IsInEnum();
        RuleFor(request => request.CashboxId).GreaterThan(0);
        RuleFor(request => request.CashMovementTypeId)
            .GreaterThan(0)
            .When(request => request.CashMovementTypeId.HasValue);
        RuleFor(request => request.AccountId)
            .GreaterThan(0)
            .When(request => request.AccountId.HasValue);
        RuleFor(request => request.EmployeeId)
            .GreaterThan(0)
            .When(request => request.EmployeeId.HasValue);
        RuleFor(request => request.BusinessPartnerId)
            .GreaterThan(0)
            .When(request => request.BusinessPartnerId.HasValue);
        RuleFor(request => request.DriverId)
            .GreaterThan(0)
            .When(request => request.DriverId.HasValue);
        RuleFor(request => request.DriverTripId)
            .GreaterThan(0)
            .When(request => request.DriverTripId.HasValue);
        RuleFor(request => request)
            .Must(request =>
                !request.DriverTripId.HasValue || request.DriverId.HasValue)
            .WithMessage("اختر السائق قبل اختيار الرحلة.")
            .WithName(nameof(CashVoucherBulkVoucherRequest.DriverTripId));
        RuleFor(request => request.ExternalPartyName)
            .MaximumLength(CashVoucherRequest.ExternalPartyNameMaximumLength)
            .Must(name => name is null || !string.IsNullOrWhiteSpace(name))
            .WithMessage("اسم الطرف الخارجي لا يمكن أن يكون فارغاً.");
        RuleFor(request => request)
            .Must(HasAtMostOneTarget)
            .WithMessage("لا يمكن اختيار أكثر من طرف أو حساب للسند المرحّل.")
            .OverridePropertyName(nameof(CashVoucherBulkVoucherRequest.EmployeeId));
        RuleFor(request => request.Amount)
            .GreaterThan(0)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true);
        RuleFor(request => request.ReferenceNumber)
            .MaximumLength(CashVoucherRequest.ReferenceNumberMaximumLength);
        RuleFor(request => request.Description)
            .MaximumLength(CashVoucherRequest.DescriptionMaximumLength);
        RuleFor(request => request.Notes)
            .MaximumLength(CashVoucherRequest.NotesMaximumLength);
        RuleFor(request => request.ExchangeRate)
            .Must(rate =>
                !rate.HasValue || ExchangeRateRules.IsValidRate(rate.Value))
            .WithMessage("يجب أن يكون سعر صرف سند النقدية أكبر من صفر.");
    }

    private static bool HasAtMostOneTarget(CashVoucherBulkVoucherRequest request)
    {
        var selectedTargetCount =
            (request.AccountId.HasValue ? 1 : 0) +
            (request.EmployeeId.HasValue ? 1 : 0) +
            (request.BusinessPartnerId.HasValue ? 1 : 0) +
            (request.DriverId.HasValue ? 1 : 0) +
            (!string.IsNullOrWhiteSpace(request.ExternalPartyName) ? 1 : 0);

        return selectedTargetCount <= 1;
    }
}

public sealed class CashVoucherBulkRequestValidator
    : AbstractValidator<CashVoucherBulkRequest>
{
    public const int MaximumItems = 100;

    public CashVoucherBulkRequestValidator()
    {
        RuleFor(request => request.Items)
            .NotEmpty()
            .WithMessage("أرسل سند نقدية واحداً على الأقل.")
            .Must(items => items is null || items.Count <= MaximumItems)
            .WithMessage($"الحد الأقصى هو {MaximumItems} سند نقدية في الطلب الواحد.")
            .Must(HaveUniqueTargetIds)
            .WithMessage("لا يمكن تكرار id بين عمليات التعديل والحذف.");

        RuleForEach(request => request.Items!)
            .NotNull()
            .SetValidator(new CashVoucherBulkItemRequestValidator());
    }

    private static bool HaveUniqueTargetIds(
        IReadOnlyList<CashVoucherBulkItemRequest>? items) =>
        items is null || items
            .Select(item => item switch
            {
                CashVoucherBulkUpdateItemRequest update => (int?)update.Id,
                CashVoucherBulkDeleteItemRequest delete => delete.Id,
                _ => null
            })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .Count() == items.Count(item =>
                item is CashVoucherBulkUpdateItemRequest or
                    CashVoucherBulkDeleteItemRequest);
}

public sealed class CashVoucherBulkItemRequestValidator
    : AbstractValidator<CashVoucherBulkItemRequest>
{
    public CashVoucherBulkItemRequestValidator()
    {
        RuleFor(item => item).SetInheritanceValidator(validator =>
        {
            validator.Add(new CashVoucherBulkAddItemRequestValidator());
            validator.Add(new CashVoucherBulkUpdateItemRequestValidator());
            validator.Add(new CashVoucherBulkDeleteItemRequestValidator());
        });
    }
}

public sealed class CashVoucherBulkAddItemRequestValidator
    : AbstractValidator<CashVoucherBulkAddItemRequest>
{
    public CashVoucherBulkAddItemRequestValidator()
    {
        RuleFor(item => item.Voucher)
            .NotNull()
            .WithMessage("بيانات السند مطلوبة للإضافة.")
            .SetValidator(new CashVoucherBulkVoucherRequestValidator()!);
    }
}

public sealed class CashVoucherBulkUpdateItemRequestValidator
    : AbstractValidator<CashVoucherBulkUpdateItemRequest>
{
    public CashVoucherBulkUpdateItemRequestValidator()
    {
        RuleFor(item => item.Id).GreaterThan(0);
        RuleFor(item => item.RowVersion)
            .NotNull()
            .Must(rowVersion => rowVersion is { Length: 8 })
            .WithMessage("يجب إرسال إصدار سند النقدية الحالي للتعديل.");
        RuleFor(item => item.Voucher)
            .NotNull()
            .WithMessage("بيانات السند مطلوبة للتعديل.")
            .SetValidator(new CashVoucherBulkVoucherRequestValidator()!);
    }
}

public sealed class CashVoucherBulkDeleteItemRequestValidator
    : AbstractValidator<CashVoucherBulkDeleteItemRequest>
{
    public CashVoucherBulkDeleteItemRequestValidator()
    {
        RuleFor(item => item.Id).GreaterThan(0);
        RuleFor(item => item.RowVersion)
            .NotNull()
            .Must(rowVersion => rowVersion is { Length: 8 })
            .WithMessage("يجب إرسال إصدار سند النقدية الحالي للحذف.");
    }
}
