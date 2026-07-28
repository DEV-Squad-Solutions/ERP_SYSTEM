using FluentValidation;
using MiniErp.Domain.Entities.Invoicing;

namespace MiniErp.Application.Features.Invoices;

public sealed class InvoiceLineRequestValidator
    : AbstractValidator<InvoiceLineRequest>
{
    public InvoiceLineRequestValidator()
    {
        RuleFor(line => line.ItemId)
            .GreaterThan(0);

        RuleFor(line => line.Count)
            .GreaterThan(0);

        RuleFor(line => line.Weight)
            .GreaterThan(0)
            .PrecisionScale(
                InvoiceAmountRules.QuantityPrecision,
                InvoiceAmountRules.QuantityScale,
                ignoreTrailingZeros: true);

        RuleFor(line => line.Price)
            .GreaterThanOrEqualTo(0)
            .PrecisionScale(
                InvoiceAmountRules.MoneyPrecision,
                InvoiceAmountRules.MoneyScale,
                ignoreTrailingZeros: true);

        RuleFor(line => line)
            .Must(line => InvoiceAmountRules.TryCalculate(
                line.Count,
                line.Weight,
                line.Price,
                out _,
                out _))
            .WithMessage(
                "نتيجة الكمية أو الإجمالي تتجاوز الدقة الرقمية المسموح بها.");

        RuleFor(line => line.Notes)
            .MaximumLength(InvoiceRequest.NotesMaximumLength);
    }
}

public sealed class InvoiceContainerLineRequestValidator
    : AbstractValidator<InvoiceContainerLineRequest>
{
    public InvoiceContainerLineRequestValidator()
    {
        RuleFor(line => line.ContainerId)
            .GreaterThan(0);

        RuleFor(line => line.OutgoingUnits)
            .GreaterThanOrEqualTo(0);

        RuleFor(line => line.IncomingUnits)
            .GreaterThanOrEqualTo(0);

        RuleFor(line => line)
            .Must(line =>
                line.OutgoingUnits > 0 || line.IncomingUnits > 0)
            .WithMessage(
                "يجب أن تحتوي حركة العبوة على وحدات صادرة أو واردة.");
    }
}

public sealed class InvoiceRequestValidator
    : AbstractValidator<InvoiceRequest>
{
    public InvoiceRequestValidator()
    {
        InvoiceValidationRules.AddCreateRules(this);
    }
}

public sealed class InvoiceUpdateRequestValidator
    : AbstractValidator<InvoiceUpdateRequest>
{
    public InvoiceUpdateRequestValidator()
    {
        InvoiceValidationRules.AddUpdateRules(this);

        RuleFor(request => request.RowVersion)
            .NotNull()
            .Must(rowVersion => rowVersion is { Length: 8 })
            .WithMessage(
                "يجب إرسال إصدار السجل الحالي المكون من 8 بايت.");
    }
}

internal static class InvoiceValidationRules
{
    public static void AddCreateRules(
        AbstractValidator<InvoiceRequest> validator)
    {
        validator.RuleFor(request => request.InvoiceNumber)
            .NotEmpty();

        validator.RuleFor(request => request.InvoiceNumber)
            .MaximumLength(InvoiceRequest.InvoiceNumberMaximumLength)
            .When(request =>
                request.InvoiceNumber is not null &&
                request.InvoiceNumber.Trim().Length >
                InvoiceRequest.InvoiceNumberMaximumLength);

        validator.RuleFor(request => request.InvoiceType)
            .IsInEnum();

        validator.RuleFor(request => request.PaymentTerm)
            .IsInEnum();

        validator.RuleFor(request => request.InvoiceDate)
            .Must(date => date != default)
            .WithMessage("تاريخ الفاتورة مطلوب.");

        validator.RuleFor(request => request.BusinessPartnerId)
            .GreaterThan(0);

        validator.RuleFor(request => request.StoreId)
            .GreaterThan(0);

        validator.RuleFor(request => request.CountryId)
            .GreaterThan(0)
            .When(request => request.CountryId.HasValue);

        validator.RuleFor(request => request.ContainerStoreId)
            .GreaterThan(0)
            .When(request => request.ContainerStoreId.HasValue);

        validator.RuleFor(request => request.DriverId)
            .GreaterThan(0)
            .When(request => request.DriverId.HasValue);

        validator.RuleFor(request => request.ActualDriverId)
            .GreaterThan(0)
            .When(request => request.ActualDriverId.HasValue);

        validator.RuleFor(request => request.DriverId)
            .NotNull()
            .When(request => request.ActualDriverId.HasValue)
            .WithMessage("يجب تحديد السائق الرئيسي قبل السائق الفعلي.");

        validator.RuleFor(request => request.DueDate)
            .GreaterThanOrEqualTo(request => request.InvoiceDate)
            .When(request => request.DueDate.HasValue)
            .WithMessage("تاريخ الاستحقاق يجب ألا يسبق تاريخ الفاتورة.");

        validator.RuleFor(request => request.ExternalDriverName)
            .MaximumLength(InvoiceRequest.ExternalDriverNameMaximumLength);

        validator.RuleFor(request => request.VehicleNumber)
            .MaximumLength(InvoiceRequest.VehicleNumberMaximumLength);

        validator.RuleFor(request => request.ExportInvoiceCode)
            .MaximumLength(InvoiceRequest.ExportInvoiceCodeMaximumLength);

        validator.RuleFor(request => request.Notes)
            .MaximumLength(InvoiceRequest.NotesMaximumLength);

        validator.RuleFor(request => request.DiscountAmount)
            .GreaterThanOrEqualTo(0m)
            .PrecisionScale(
                InvoiceAmountRules.MoneyPrecision,
                InvoiceAmountRules.MoneyScale,
                ignoreTrailingZeros: true);

        validator.RuleFor(request => request.PaidAmount)
            .GreaterThanOrEqualTo(0m)
            .PrecisionScale(
                InvoiceAmountRules.MoneyPrecision,
                InvoiceAmountRules.MoneyScale,
                ignoreTrailingZeros: true);

        AddAmountRules(validator);

        validator.RuleFor(request => request.Lines)
            .NotNull()
            .NotEmpty()
            .Must(lines => lines is not null &&
                lines.Count <= InvoiceRequest.MaximumLineCount)
            .WithMessage(
                $"لا يجوز أن يتجاوز عدد سطور الفاتورة {InvoiceRequest.MaximumLineCount}.")
            .Must(lines => lines is not null &&
                lines.All(line => line is not null))
            .WithMessage("كل سطر في الفاتورة مطلوب.")
            .Must(lines => lines is not null &&
                lines.All(line => line is not null) &&
                lines.Select(line => line.ItemId).Distinct().Count() == lines.Count)
            .WithMessage("لا يجوز تكرار الصنف في سطور الفاتورة.");

        validator.RuleFor(request => request.ContainerLines)
            .NotNull()
            .Must(lines => lines is not null &&
                lines.Count <= InvoiceRequest.MaximumContainerLineCount)
            .WithMessage(
                $"لا يجوز أن يتجاوز عدد سطور العبوات {InvoiceRequest.MaximumContainerLineCount}.")
            .Must(lines => lines is not null &&
                lines.All(line => line is not null))
            .WithMessage("كل سطر عبوة في الفاتورة مطلوب.")
            .Must(lines => lines is not null &&
                lines.All(line => line is not null) &&
                lines.Select(line => line.ContainerId).Distinct().Count() == lines.Count)
            .WithMessage("لا يجوز تكرار العبوة في سطور الفاتورة.");

        validator.RuleFor(request => request.ExternalDriverName)
            .NotEmpty()
            .When(request => request.UsesExternalDriver)
            .WithMessage("اسم السائق الخارجي مطلوب.");

        validator.RuleFor(request => request.ActualDriverId)
            .Null()
            .When(request => request.UsesExternalDriver)
            .WithMessage("لا يجوز اختيار سائق فعلي داخلي مع السائق الخارجي.");

        validator.RuleForEach(request => request.Lines)
            .SetValidator(new InvoiceLineRequestValidator());

        validator.RuleForEach(request => request.ContainerLines)
            .SetValidator(new InvoiceContainerLineRequestValidator());
    }

    public static void AddUpdateRules(
        AbstractValidator<InvoiceUpdateRequest> validator)
    {
        validator.RuleFor(request => request.InvoiceType)
            .IsInEnum();

        validator.RuleFor(request => request.PaymentTerm)
            .IsInEnum();

        validator.RuleFor(request => request.InvoiceDate)
            .Must(date => date != default)
            .WithMessage("تاريخ الفاتورة مطلوب.");

        validator.RuleFor(request => request.BusinessPartnerId)
            .GreaterThan(0);

        validator.RuleFor(request => request.StoreId)
            .GreaterThan(0);

        validator.RuleFor(request => request.CountryId)
            .GreaterThan(0)
            .When(request => request.CountryId.HasValue);

        validator.RuleFor(request => request.ContainerStoreId)
            .GreaterThan(0)
            .When(request => request.ContainerStoreId.HasValue);

        validator.RuleFor(request => request.DriverId)
            .GreaterThan(0)
            .When(request => request.DriverId.HasValue);

        validator.RuleFor(request => request.ActualDriverId)
            .GreaterThan(0)
            .When(request => request.ActualDriverId.HasValue);

        validator.RuleFor(request => request.DriverId)
            .NotNull()
            .When(request => request.ActualDriverId.HasValue)
            .WithMessage("يجب تحديد السائق الرئيسي قبل السائق الفعلي.");

        validator.RuleFor(request => request.DueDate)
            .GreaterThanOrEqualTo(request => request.InvoiceDate)
            .When(request => request.DueDate.HasValue)
            .WithMessage("تاريخ الاستحقاق يجب ألا يسبق تاريخ الفاتورة.");

        validator.RuleFor(request => request.ExternalDriverName)
            .MaximumLength(InvoiceRequest.ExternalDriverNameMaximumLength);

        validator.RuleFor(request => request.VehicleNumber)
            .MaximumLength(InvoiceRequest.VehicleNumberMaximumLength);

        validator.RuleFor(request => request.ExportInvoiceCode)
            .MaximumLength(InvoiceRequest.ExportInvoiceCodeMaximumLength);

        validator.RuleFor(request => request.Notes)
            .MaximumLength(InvoiceRequest.NotesMaximumLength);

        validator.RuleFor(request => request.DiscountAmount)
            .GreaterThanOrEqualTo(0m)
            .PrecisionScale(
                InvoiceAmountRules.MoneyPrecision,
                InvoiceAmountRules.MoneyScale,
                ignoreTrailingZeros: true);

        validator.RuleFor(request => request.PaidAmount)
            .GreaterThanOrEqualTo(0m)
            .PrecisionScale(
                InvoiceAmountRules.MoneyPrecision,
                InvoiceAmountRules.MoneyScale,
                ignoreTrailingZeros: true);

        AddAmountRules(validator);

        validator.RuleFor(request => request.Lines)
            .NotNull()
            .NotEmpty()
            .Must(lines => lines is not null &&
                lines.Count <= InvoiceRequest.MaximumLineCount)
            .WithMessage(
                $"لا يجوز أن يتجاوز عدد سطور الفاتورة {InvoiceRequest.MaximumLineCount}.")
            .Must(lines => lines is not null &&
                lines.All(line => line is not null))
            .WithMessage("كل سطر في الفاتورة مطلوب.")
            .Must(lines => lines is not null &&
                lines.All(line => line is not null) &&
                lines.Select(line => line.ItemId).Distinct().Count() == lines.Count)
            .WithMessage("لا يجوز تكرار الصنف في سطور الفاتورة.");

        validator.RuleFor(request => request.ContainerLines)
            .NotNull()
            .Must(lines => lines is not null &&
                lines.Count <= InvoiceRequest.MaximumContainerLineCount)
            .WithMessage(
                $"لا يجوز أن يتجاوز عدد سطور العبوات {InvoiceRequest.MaximumContainerLineCount}.")
            .Must(lines => lines is not null &&
                lines.All(line => line is not null))
            .WithMessage("كل سطر عبوة في الفاتورة مطلوب.")
            .Must(lines => lines is not null &&
                lines.All(line => line is not null) &&
                lines.Select(line => line.ContainerId).Distinct().Count() == lines.Count)
            .WithMessage("لا يجوز تكرار العبوة في سطور الفاتورة.");

        validator.RuleFor(request => request.ExternalDriverName)
            .NotEmpty()
            .When(request => request.UsesExternalDriver)
            .WithMessage("اسم السائق الخارجي مطلوب.");

        validator.RuleFor(request => request.ActualDriverId)
            .Null()
            .When(request => request.UsesExternalDriver)
            .WithMessage("لا يجوز اختيار سائق فعلي داخلي مع السائق الخارجي.");

        validator.RuleForEach(request => request.Lines)
            .SetValidator(new InvoiceLineRequestValidator());

        validator.RuleForEach(request => request.ContainerLines)
            .SetValidator(new InvoiceContainerLineRequestValidator());
    }

    private static void AddAmountRules(
        AbstractValidator<InvoiceRequest> validator)
    {
        validator.RuleFor(request => request.DiscountAmount)
            .Must((request, discountAmount) =>
                !TryCalculateNetTotal(
                    request.Lines,
                    discountAmount,
                    out var subtotal,
                    out _) ||
                discountAmount <= subtotal)
            .WithMessage(
                "قيمة الخصم يجب ألا تكون سالبة ولا يمكن أن تتجاوز إجمالي سطور الفاتورة.")
            .WithErrorCode("Invoices.InvalidDiscountAmount");

        validator.RuleFor(request => request.PaidAmount)
            .Must((request, paidAmount) =>
                !TryCalculateNetTotal(
                    request.Lines,
                    request.DiscountAmount,
                    out _,
                    out var total) ||
                paidAmount <= total)
            .WithMessage(
                "المبلغ المدفوع يجب ألا يكون سالبًا ولا يمكن أن يتجاوز صافي الفاتورة.")
            .WithErrorCode("Invoices.InvalidPaidAmount");
    }

    private static void AddAmountRules(
        AbstractValidator<InvoiceUpdateRequest> validator)
    {
        validator.RuleFor(request => request.DiscountAmount)
            .Must((request, discountAmount) =>
                !TryCalculateNetTotal(
                    request.Lines,
                    discountAmount,
                    out var subtotal,
                    out _) ||
                discountAmount <= subtotal)
            .WithMessage(
                "قيمة الخصم يجب ألا تكون سالبة ولا يمكن أن تتجاوز إجمالي سطور الفاتورة.")
            .WithErrorCode("Invoices.InvalidDiscountAmount");

        validator.RuleFor(request => request.PaidAmount)
            .Must((request, paidAmount) =>
                !TryCalculateNetTotal(
                    request.Lines,
                    request.DiscountAmount,
                    out _,
                    out var total) ||
                paidAmount <= total)
            .WithMessage(
                "المبلغ المدفوع يجب ألا يكون سالبًا ولا يمكن أن يتجاوز صافي الفاتورة.")
            .WithErrorCode("Invoices.InvalidPaidAmount");
    }

    private static bool TryCalculateNetTotal(
        IReadOnlyList<InvoiceLineRequest>? lines,
        decimal discountAmount,
        out decimal subtotal,
        out decimal total)
    {
        subtotal = 0m;
        total = 0m;

        if (lines is null)
        {
            return false;
        }

        try
        {
            foreach (var line in lines)
            {
                if (line is null ||
                    !InvoiceAmountRules.TryCalculate(
                        line.Count,
                        line.Weight,
                        line.Price,
                        out _,
                        out var lineTotal))
                {
                    return false;
                }

                subtotal += lineTotal;
            }

            total = decimal.Round(
                subtotal - discountAmount,
                InvoiceAmountRules.MoneyScale,
                MidpointRounding.AwayFromZero);
            return true;
        }
        catch (OverflowException)
        {
            subtotal = 0m;
            total = 0m;
            return false;
        }
    }
}
