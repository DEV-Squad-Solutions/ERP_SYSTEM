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
                    "Returns one deterministic page of invoices owned by the selected company. Each item includes complete ordered product and container lines, subtotal, discount, net total, paid amount, remaining amount, payment term, payment status, and row-version token.",
                    "A bearer token containing one validated `company_id`; `pageNumber` and `pageSize` are optional.",
                    "`pageNumber` must be greater than zero and `pageSize` must be between 1 and 100.",
                    "Invalid pagination returns 400. Empty and later pages return an empty `items` array. Deleted and other-company records are excluded.")),
            nameof(InvoicesController.GetById) => (
                "Get an invoice",
                SwaggerOperationDescription.Create(
                    "Returns an invoice aggregate with complete product and container lines. Currency and item units are server-derived; subtotal, discount, net total, paid amount, remaining amount, and payment status are returned using the server calculation rules.",
                    "A bearer token containing one validated `company_id` and a positive route `id`.",
                    "No company ID, invoice number, currency, item unit, quantity, line total, invoice total, audit field, or row-version value is accepted from the create request.",
                    "Invalid IDs return 400. Missing, deleted, and other-company invoices return 404.")),
            nameof(InvoicesController.Create) => (
                "Create an invoice",
                SwaggerOperationDescription.Create(
                    "Admin only. Creates the complete invoice aggregate and synchronizes item movements, container movements, an outstanding partner movement when the remaining amount is positive, and an internal-driver trip atomically. Cash is the default payment term and must be fully paid.",
                    "`invoiceType`, `invoiceDate`, `businessPartnerId`, `storeId`, `lines`, `discountAmount`, `paidAmount`, and a required `paymentTerm` select (`Cash` or `Credit`). Each product line contains `itemId`, `count`, `weight`, `price`, and optional `notes`; container lines contain `containerId`, `outgoingUnits`, and `incomingUnits`.",
                    "The product store, partner, items, item units, optional country, and driver must be active and belong to the selected company. Container lines are allowed for sales and sales-return invoices and require the selected partner's active container store and assigned active containers. Returns are independent invoice documents and do not reference an earlier invoice. Do not send invoice number, currency, item unit, subtotal, total, remaining amount, calculated line amounts, row version, or company ID. For Cash, `paidAmount` must equal the net total; for Credit, it may be zero through the net total.",
                    "Invalid or missing references return 404; inactive or mismatched references, duplicates, invalid discount or payment amounts, unsupported enums, and stock conflicts return 400/409.")),
            nameof(InvoicesController.Update) => (
                "Replace an invoice aggregate",
                SwaggerOperationDescription.Create(
                    "Admin only. Replaces editable header, product-line, and container-line fields in one transaction. Discount and paid amounts are replaced with the header; partner movement side effects are recreated from the new remaining amount. Child-only changes touch the header and advance its row version.",
                    "Positive route `id`, the create fields, and the original non-empty base64 `rowVersion` returned by the API.",
                    "Currency, item units, quantities, line totals, subtotal, invoice total, remaining amount, audit fields, and company ID are server-controlled. Payment term, discount amount, and paid amount are editable; Cash must remain fully paid and Credit paid amount must be between zero and the net total. The complete product-line set is required; container lines are replaced as a complete set.",
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
