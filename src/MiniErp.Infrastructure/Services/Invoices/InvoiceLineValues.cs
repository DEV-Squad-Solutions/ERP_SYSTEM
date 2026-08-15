using MiniErp.Application.Features.Invoices;
using MiniErp.Domain.Entities.Invoicing;

namespace MiniErp.Infrastructure.Services.Invoices;

internal static class InvoiceLineValues
{
    public static bool TryGetEffective(
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
}
