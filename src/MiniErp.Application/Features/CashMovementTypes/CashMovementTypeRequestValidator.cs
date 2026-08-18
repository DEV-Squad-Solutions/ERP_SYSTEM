using System.Linq.Expressions;
using FluentValidation;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.CashMovementTypes;

public sealed class CashMovementTypeRequestValidator
    : AbstractValidator<CashMovementTypeRequest>
{
    public CashMovementTypeRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(CashMovementTypeRequest.NameMaximumLength);

        RuleFor(request => request.Direction)
            .IsInEnum();

        RuleFor(request => request.Classification)
            .IsInEnum()
            .Must((request, classification) =>
                classification != CashMovementClassification.PartnerSettlement ||
                request.ForPartner)
            .WithMessage(
                "تسوية العميل أو المورد يجب أن تكون مرتبطة بعميل أو مورد.");

        AddInvoiceDefaultRules(
            request => request.IsDefaultForSales,
            request => request.ForPartner,
            request => request.IsActive,
            request => request.Direction,
            CashDirection.Receipt,
            "البيع");
        AddInvoiceDefaultRules(
            request => request.IsDefaultForPurchase,
            request => request.ForPartner,
            request => request.IsActive,
            request => request.Direction,
            CashDirection.Payment,
            "الشراء");
        AddInvoiceDefaultRules(
            request => request.IsDefaultForSalesReturn,
            request => request.ForPartner,
            request => request.IsActive,
            request => request.Direction,
            CashDirection.Payment,
            "مرتجع البيع");
        AddInvoiceDefaultRules(
            request => request.IsDefaultForPurchaseReturn,
            request => request.ForPartner,
            request => request.IsActive,
            request => request.Direction,
            CashDirection.Receipt,
            "مرتجع الشراء");

        RuleFor(request => request.Notes)
            .MaximumLength(CashMovementTypeRequest.NotesMaximumLength);
    }

    private void AddInvoiceDefaultRules(
        Expression<Func<CashMovementTypeRequest, bool>> selector,
        Func<CashMovementTypeRequest, bool> forPartner,
        Func<CashMovementTypeRequest, bool> isActive,
        Func<CashMovementTypeRequest, CashDirection> direction,
        CashDirection expectedDirection,
        string invoiceTypeName) =>
        CashMovementTypeDefaultValidation.AddRules(
            this,
            selector,
            forPartner,
            isActive,
            request => request.Classification,
            direction,
            expectedDirection,
            invoiceTypeName);
}

public sealed class CashMovementTypeUpdateRequestValidator
    : AbstractValidator<CashMovementTypeUpdateRequest>
{
    public CashMovementTypeUpdateRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(CashMovementTypeRequest.NameMaximumLength);

        RuleFor(request => request.Direction)
            .IsInEnum();

        RuleFor(request => request.Classification)
            .IsInEnum()
            .Must((request, classification) =>
                classification != CashMovementClassification.PartnerSettlement ||
                request.ForPartner)
            .WithMessage(
                "تسوية العميل أو المورد يجب أن تكون مرتبطة بعميل أو مورد.");

        AddInvoiceDefaultRules(
            request => request.IsDefaultForSales,
            request => request.ForPartner,
            request => request.IsActive,
            request => request.Direction,
            CashDirection.Receipt,
            "البيع");
        AddInvoiceDefaultRules(
            request => request.IsDefaultForPurchase,
            request => request.ForPartner,
            request => request.IsActive,
            request => request.Direction,
            CashDirection.Payment,
            "الشراء");
        AddInvoiceDefaultRules(
            request => request.IsDefaultForSalesReturn,
            request => request.ForPartner,
            request => request.IsActive,
            request => request.Direction,
            CashDirection.Payment,
            "مرتجع البيع");
        AddInvoiceDefaultRules(
            request => request.IsDefaultForPurchaseReturn,
            request => request.ForPartner,
            request => request.IsActive,
            request => request.Direction,
            CashDirection.Receipt,
            "مرتجع الشراء");

        RuleFor(request => request.Notes)
            .MaximumLength(CashMovementTypeRequest.NotesMaximumLength);

        RuleFor(request => request.RowVersion)
            .NotNull()
            .Must(rowVersion => rowVersion is { Length: 8 })
            .WithMessage(
                "يجب إرسال إصدار نوع الحركة النقدية الحالي للتعديل.");
    }

    private void AddInvoiceDefaultRules(
        Expression<Func<CashMovementTypeUpdateRequest, bool>> selector,
        Func<CashMovementTypeUpdateRequest, bool> forPartner,
        Func<CashMovementTypeUpdateRequest, bool> isActive,
        Func<CashMovementTypeUpdateRequest, CashDirection> direction,
        CashDirection expectedDirection,
        string invoiceTypeName) =>
        CashMovementTypeDefaultValidation.AddRules(
            this,
            selector,
            forPartner,
            isActive,
            request => request.Classification,
            direction,
            expectedDirection,
            invoiceTypeName);
}

internal static class CashMovementTypeDefaultValidation
{
    public static void AddRules<TRequest>(
        AbstractValidator<TRequest> validator,
        Expression<Func<TRequest, bool>> selector,
        Func<TRequest, bool> forPartner,
        Func<TRequest, bool> isActive,
        Func<TRequest, CashMovementClassification> classification,
        Func<TRequest, CashDirection> direction,
        CashDirection expectedDirection,
        string invoiceTypeName)
    {
        validator.RuleFor(selector)
            .Must((request, isDefault) => !isDefault || forPartner(request))
            .WithMessage(
                $"الحركة الافتراضية لفاتورة {invoiceTypeName} يجب أن تكون حركة عميل أو مورد.");

        validator.RuleFor(selector)
            .Must((request, isDefault) =>
                !isDefault || classification(request) ==
                CashMovementClassification.PartnerSettlement)
            .WithMessage(
                $"الحركة الافتراضية لفاتورة {invoiceTypeName} يجب أن تكون من تصنيف تسوية عميل أو مورد.");

        validator.RuleFor(selector)
            .Must((request, isDefault) => !isDefault || isActive(request))
            .WithMessage(
                $"الحركة الافتراضية لفاتورة {invoiceTypeName} يجب أن تكون نشطة.");

        validator.RuleFor(selector)
            .Must((request, isDefault) =>
                !isDefault || direction(request) == expectedDirection)
            .WithMessage(
                expectedDirection == CashDirection.Receipt
                    ? $"الحركة الافتراضية لفاتورة {invoiceTypeName} يجب أن تكون قبضًا."
                    : $"الحركة الافتراضية لفاتورة {invoiceTypeName} يجب أن تكون صرفًا.");
    }

}
