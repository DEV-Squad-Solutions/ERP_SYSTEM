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
                    "Returns one deterministic header-only page of invoices owned by the selected company. Every supplied filter is combined with AND. The response summary contains subtotal, discount, net total, paid amount, and remaining amount across the complete filtered result, not only the current page. List items contain header values and child counts; use `GET /Invoices/{id}` to load complete ordered product and container lines when opening details or editing.",
                    "A bearer token containing one validated `company_id`. Optional query fields are `pageNumber`, `pageSize`, `search`, partial `invoiceNumber`, `invoiceType`, `businessPartnerId`, `countryId`, `storeId`, responsible `driverId`, `paymentTerm`, `priceStatus`, inclusive `fromDate`, and inclusive `toDate`. `search` matches invoice, partner, store, country, driver, vehicle, product, and container display values. `priceStatus` accepts `HasMissingPrice` (`Price == 0` on any line) or `AllItemsPriced` (every line has `Price > 0`). Date filters accept ISO, day-first, month-first, named-month, alternate-separator, and Arabic/Persian-digit forms; ISO `yyyy-MM-dd` remains preferred.",
                    "`pageNumber` must be greater than zero; `pageSize` must be between 1 and 100; IDs must be positive; enum values must be supported; and `toDate` must not precede `fromDate`. Ambiguous numeric dates are interpreted day-first, so `01/02/2026` means 1 February 2026.",
                    "Invalid pagination or filters return 400. Empty and later pages return an empty `items` array. A filter with no matches returns zero summary amounts. Deleted and other-company records are excluded.")),
            nameof(InvoicesController.GetItemBalance) => (
                "Get item balance for an invoice",
                SwaggerOperationDescription.Create(
                    "Returns the selected item's available balance in one product store at the end of the supplied invoice date. The balance is derived from active stock opening balances plus active item movements through that date.",
                    "Positive `storeId` and `itemId`, plus required `asOfDate`. During invoice editing, send the optional current `invoiceId` so that invoice's existing movement is excluded and the displayed quantity is available to the replacement invoice.",
                    "The store and item must be active, belong to the selected company, and the store must be a product store. This informational balance does not replace the full historical stock validation performed when the invoice is saved.",
                    "Invalid values return 400. Missing or other-company records return 404. Inactive references or a container store return 409.")),
            nameof(InvoicesController.GetById) => (
                "Get an invoice",
                SwaggerOperationDescription.Create(
                    "Returns an invoice aggregate with complete product and container lines plus the responsible and optional actual-driver IDs and names. When the actual driver is null, the responsible driver performed the delivery; an external driver name is the physical driver when external-driver mode is enabled. Currency and item units are server-derived; subtotal, discount, net total, paid amount, remaining amount, and payment status are returned using the server calculation rules.",
                    "A bearer token containing one validated `company_id` and a positive route `id`.",
                    "No company ID, currency, item unit, quantity, line total, invoice total, audit field, or row-version value is accepted from the create request.",
                    "Invalid IDs return 400. Missing, deleted, and other-company invoices return 404.")),
            nameof(InvoicesController.Create) => (
                "Create an invoice",
                SwaggerOperationDescription.Create(
                    "Admin only. Creates the complete invoice aggregate and synchronizes item movements, container movements, an outstanding partner movement when the remaining amount is positive, and one responsible-driver trip atomically. The trip stores the optional internal actual driver; external-driver mode keeps the responsible driver and uses the external name as the physical driver. The user enters the invoice number; it is trimmed, may be duplicated, and cannot be changed after creation. Cash is the default payment term.",
                    "A required `invoiceNumber` of at most 100 characters, plus `invoiceType`, `invoiceDate`, `businessPartnerId`, `storeId`, `lines`, `discountAmount`, `paidAmount`, and a required `paymentTerm` select (`Cash` or `Credit`). `driverId` is the optional responsible driver and `actualDriverId` is the optional physical driver. Each product line contains `itemId`, `count`, `weight`, `price`, and optional `notes`; container lines contain `containerId`, `outgoingUnits`, and `incomingUnits`.",
                    "The product store, partner, items, item units, optional country, responsible driver, and actual driver must be active and belong to the selected company. An actual driver requires a responsible driver. Selecting the same driver for both roles normalizes `actualDriverId` to null. External-driver mode requires `externalDriverName`, allows a responsible `driverId`, and does not allow `actualDriverId`. Container lines are allowed for sales and sales-return invoices and require the selected partner's active container store and assigned active containers. Returns are independent invoice documents and do not reference an earlier invoice. Duplicate invoice numbers are allowed. Do not send currency, item unit, subtotal, total, remaining amount, calculated line amounts, row version, or company ID. For both Cash and Credit, `paidAmount` may be zero through the net total; any remaining amount is reflected in the partner movement.",
                    "Invalid or missing references return 404; inactive or mismatched references, duplicate item/container selections, invalid discount or payment amounts, unsupported enums, and stock conflicts return 400/409.")),
            nameof(InvoicesController.Update) => (
                "Replace an invoice aggregate",
                SwaggerOperationDescription.Create(
                    "Admin only. Replaces editable header, product-line, and container-line fields in one transaction. Responsible and actual-driver changes replace the single trip located by company and invoice ID without changing inventory or partner movements. Discount and paid amounts are replaced with the header; partner movement side effects are recreated from the new remaining amount. Child-only changes touch the header and advance its row version.",
                    "Positive route `id`, the editable create fields except `invoiceNumber`, and the original non-empty base64 `rowVersion` returned by the API.",
                    "Invoice number is immutable after creation. Currency, item units, quantities, line totals, subtotal, invoice total, remaining amount, audit fields, and company ID are server-controlled. Payment term, discount amount, and paid amount are editable; paid amount may be between zero and the net total for both Cash and Credit. The complete product-line set is required; container lines are replaced as a complete set.",
                    "A stale token returns 409 (`Invoices.Concurrency`) and requires reloading the invoice. Missing or other-company records return 404. Return and relationship rules are the same as create; returns do not require or allocate against another invoice.")),
            nameof(InvoicesController.Delete) => (
                "Delete an invoice",
                SwaggerOperationDescription.Create(
                    "Admin only. Soft-deletes the invoice, its current product and container lines, and its synchronized movement/trip/partner side effects atomically. Inbound deletion also validates the resulting historical stock timeline. Audit history remains available through query-filter bypasses.",
                    "A positive route `id` and an Admin bearer token containing one validated `company_id`.",
                    "No request body is required. Deletion removes only the selected invoice and its synchronized side effects; returns are independent documents.",
                    "Missing, deleted, and other-company invoices return 404. Inbound deletion returns a stock conflict when removing it would make a later historical balance negative. There is no status, posting, cancellation, reversal, voucher, or allocation workflow.")),
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
