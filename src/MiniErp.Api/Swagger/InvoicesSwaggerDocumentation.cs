using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class InvoicesSwaggerDocumentation : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(InvoicesController))
        {
            return;
        }

        var documentation = context.MethodInfo.Name switch
        {
            nameof(InvoicesController.GetAll) => (
                "Get paginated invoices",
                SwaggerOperationDescription.Create(
                    "Returns one deterministic header-only page of invoices owned by the selected company. Every supplied filter is combined with AND. The response summary contains subtotal, discount, net total, paid amount, and remaining amount across the complete filtered result, not only the current page. List items include PartnerInvoiceNo, Unpaid/PartiallyPaid/Paid status, and current generated payment-voucher references plus child counts; use `GET /Invoices/{id}` to load complete ordered product and container lines when opening details or editing.",
                    "A bearer token containing one validated `company_id`. Optional query fields are `pageNumber`, `pageSize`, `search`, partial `invoiceNumber`, `invoiceType`, `businessPartnerId`, `countryId`, `storeId`, responsible `driverId`, `paymentTerm`, `priceStatus`, inclusive `fromDate`, and inclusive `toDate`. `search` matches invoice, partner, store, country, driver, vehicle, product, and container display values. `priceStatus` accepts `HasMissingPrice` (`Price == 0` on any line) or `AllItemsPriced` (every line has `Price > 0`). Date filters accept ISO, day-first, month-first, named-month, alternate-separator, and Arabic/Persian-digit forms; ISO `yyyy-MM-dd` remains preferred.",
                    "`pageNumber` must be greater than zero; `pageSize` must be between 1 and 100; IDs must be positive; enum values must be supported; and `toDate` must not precede `fromDate`. Ambiguous numeric dates are interpreted day-first, so `01/02/2026` means 1 February 2026.",
                    "Invalid pagination or filters return 400. Empty and later pages return an empty `items` array. A filter with no matches returns zero summary amounts. Deleted and other-company records are excluded.")),
            nameof(InvoicesController.GetItemBalance) => (
                "Get item balance for an invoice",
                SwaggerOperationDescription.Create(
                    "Returns the selected item's as-of quantity in `currentQuantity`, weighted-average cost in `averageCost`, and inventory value for the selected store through the supplied invoice date. The legacy `balance` field remains available and always equals `currentQuantity`. Opening balances are represented by active item movements.",
                    "Positive `storeId` and `itemId`, plus required `asOfDate`. During invoice editing, send the optional current `invoiceId` so that invoice's existing movement is excluded and the displayed quantity is available to the replacement invoice.",
                    "The store and item must be active, belong to the selected company, and the store must be a product store. This informational balance does not replace the full historical stock validation performed when the invoice is saved.",
                    "Invalid values return 400. Missing or other-company records return 404. Inactive references or a container store return 409.")),
            nameof(InvoicesController.GetReturnSources) => (
                "Get original invoices available for a return",
                SwaggerOperationDescription.Create(
                    "Fills the original-invoice selector for a sales or purchase return. SalesReturn loads Sales invoices; PurchaseReturn loads Purchase invoices. Results are restricted to the current company, selected partner and store, invoices dated on or before asOfDate, and invoices with at least one quantity still available to return.",
                    "Required positive businessPartnerId and storeId, required returnType (`SalesReturn` or `PurchaseReturn` only), required ISO asOfDate, and valid pagination. Optional search matches the application invoice number or partner invoice number. When editing, send currentReturnInvoiceId so its existing quantities are excluded from the already-returned total.",
                    "Every returned line includes originalQuantity, returnedQuantity, availableQuantity, original unitPrice, and originalTotal. Fully returned lines may remain in an eligible invoice for display but have availableQuantity zero. originalDiscountAmount remains informational for the original invoice. Saving the return revalidates availability and enforces the original price. The original invoice-level discount applies only when the current return document includes every original line at its full original quantity; every partial return, including a later remainder after an earlier return, receives zero invoice-level discount.",
                    "Invalid filters return 400. Concurrent or excessive return quantities are rejected when the return is saved. An empty result means no matching invoice has a returnable quantity.")),
            nameof(InvoicesController.GetById) => (
                "Get an invoice",
                SwaggerOperationDescription.Create(
                    "Returns an invoice aggregate with PartnerInvoiceNo, optional company-owned item category (`itemsCategoryId` and `itemsCategoryName`), ContentType (Items or Containers), weighbridge fields (`wbWeight`, `wbScaleDifference`, `wbDiscount`, and server-calculated `wbTotal`), payment status, generated payment-voucher/cashbox/type references, and complete product and container lines. Every product entry in `lines` returns its persisted `count`, `weight`, and calculated `quantity`, together with server-calculated inventory cost status, pending quantity, unit cost, total cost, quantity after, average cost after, and inventory value after.",
                    "A bearer token containing one validated `company_id` and a positive route `id`.",
                    "No company ID, currency, item unit, quantity, line total, invoice total, audit field, or row-version value is accepted from the create request.",
                    "Invalid IDs return 400. Missing, deleted, and other-company invoices return 404.")),
            nameof(InvoicesController.Create) => (
                "Create an invoice",
                SwaggerOperationDescription.Create(
                    "Admin only. Creates the complete invoice aggregate, stable item movements, weighted-average cost snapshots in the company base currency, pending-cost allocations, current item/store balances, container movements, full invoice partner movement, optional generated payment voucher and opposite partner payment movement, and driver trip in one Serializable transaction. The response includes transaction/base amounts and resolved exchange-rate snapshots.",
                    "A required `invoiceNumber` of at most 100 characters, optional `partnerInvoiceNo` of at most 100 characters, optional active company-owned `itemsCategoryId`, plus `invoiceType`, `contentType` (`Items` or `Containers`), `invoiceDate`, `businessPartnerId`, `storeId`, the matching `lines` or `containerLines` collection, `discountAmount`, `paidAmount`, and a required `paymentTerm` select (`Cash` or `Credit`). Each item line may send `count` and `weight` for server-calculated quantity, or omit both and send a positive `quantity` directly. Non-negative `wbWeight`, `wbScaleDifference`, and `wbDiscount` use quantity precision. Optional `wbTotal` is an assertion and is never persisted directly: null, omission, or zero skips the match check; for item invoices, a positive value is matched only to the sum of effective item-line quantities (`count * weight`, or a directly supplied `quantity`), not to the server calculation from the three weighbridge inputs. Negative or over-precision `wbTotal` values are rejected. The response always returns the server-calculated `wbTotal`, which can differ from the positive assertion. Optional positive `exchangeRate` and `cashboxExchangeRate` values override automatic dated resolution. Cash requires `paidAmount == total`; Credit accepts zero or a partial amount only. Every positive paid amount requires `cashboxId`; the request does not accept a cash movement type.",
                    "The server selects the independent active partner movement default configured for the exact invoice type. Sales and PurchaseReturn use Receipt; Purchase and SalesReturn use Payment. The selected cashbox must be active, company-owned, and currency-compatible. A Payment cannot make the cashbox negative. `PaymentStatus` is server-calculated as Unpaid, PartiallyPaid, or Paid.",
                    "Invalid or missing references return 404; inactive, currency, direction, partner-effect, cashbox-balance, duplicate item/container, discount/payment, enum, and stock conflicts return 400/409.")),
            nameof(InvoicesController.Update) => (
                "Replace an invoice aggregate",
                SwaggerOperationDescription.Create(
                    "Admin only. Replaces editable fields in one Serializable transaction. Matching item movements are updated in place so their IDs and CreatedOn values remain stable; removed movements are soft-deleted and only new lines create new movements. Every affected item/store timeline and pending-cost allocation set is replayed deterministically.",
                    "Positive route `id`, the editable create fields except `invoiceNumber`, including `contentType` (`Items` or `Containers`), and the original non-empty base64 `rowVersion` returned by the API.",
                    "Invoice number is immutable after creation. PartnerInvoiceNo, itemsCategoryId, and the three weighbridge inputs are editable. For item lines, send either `count` and `weight`, or omit both and send a positive `quantity` directly. Optional `wbTotal` has the same assertion-only behavior as create: null, omission, or zero skips quantity matching; a positive value is matched only to the sum of effective item-line quantities, while the returned and persisted `wbTotal` remains server-calculated from the three weighbridge inputs and may differ from the assertion. Container invoices are excluded from the quantity match. The category must be active and company-owned when newly selected; an already-linked category may remain on an invoice after deactivation. `itemsCategoryName`, returned `wbTotal`, currency, item units, quantities, line totals, subtotal, invoice total, payment status, remaining amount, base-currency snapshots, cash movement type, audit fields, and company ID are server-controlled. Cash remains fully paid; Credit remains zero or partially paid. A positive paid amount requires the current cashbox. The generated voucher and its partner movement are updated in place while payment remains positive, created when payment becomes positive, and soft-deleted when payment becomes zero. An existing compatible voucher keeps its historical movement type; otherwise the server uses the current default.",
                    "A stale token returns 409 (`Invoices.Concurrency`) and requires reloading the invoice. Missing or other-company records return 404. Payment cashbox/default-type and balance rules are the same as create. The complete product-line set is required; container lines are replaced as a complete set.")),
            nameof(InvoicesController.Delete) => (
                "Delete an invoice",
                SwaggerOperationDescription.Create(
                    "Admin only. Soft-deletes the invoice, lines, generated payment voucher, voucher partner movement, and other synchronized side effects, validates stock and final cashbox balance, and rebuilds costing snapshots, allocations, and balances atomically.",
                    "A positive route `id` and an Admin bearer token containing one validated `company_id`.",
                    "No request body is required. Deletion removes only the selected invoice and its synchronized side effects; returns remain independent documents except for an optional sales-return costing source.",
                    "Missing, deleted, and other-company invoices return 404. Removing a Receipt voucher returns 409 if the resulting cashbox balance would be negative. Inbound deletion returns a stock conflict when removing it would make a later historical balance negative. There is no posting, cancellation, reversal, or payment-allocation workflow.")),
            _ => default
        };

        if (documentation == default)
        {
            return;
        }

        operation.Summary = documentation.Item1;
        operation.Description = documentation.Item2;
        operation.OperationId = $"Invoices_{context.MethodInfo.Name}";
    }
}
