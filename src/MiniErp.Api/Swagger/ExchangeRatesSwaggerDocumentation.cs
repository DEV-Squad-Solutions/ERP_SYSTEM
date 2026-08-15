using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class ExchangeRatesSwaggerDocumentation : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType !=
            typeof(ExchangeRatesController))
        {
            return;
        }

        var documentation = context.MethodInfo.Name switch
        {
            nameof(ExchangeRatesController.GetAll) => (
                "Get exchange rates",
                SwaggerOperationDescription.Create(
                    "Returns the selected company's dated exchange rates, ordered by date, currency, and ID.",
                    "Pagination plus optional `currency`, `dateFrom`, `dateTo`, `source`, and `search` filters. Search is trimmed and matches currency codes or notes.",
                    "Search cannot exceed 500 characters after trimming. Each rate means base-currency units per one unit of the foreign currency.",
                    "Only the current tenant's active rates are returned.")),
            nameof(ExchangeRatesController.GetById) => (
                "Get an exchange rate",
                SwaggerOperationDescription.Create(
                    "Returns one tenant-owned exchange rate.",
                    "A positive route `id`.",
                    "The response includes the company base currency and row-version token.",
                    "Missing or deleted records return 404.")),
            nameof(ExchangeRatesController.Resolve) => (
                "Resolve a document exchange rate",
                SwaggerOperationDescription.Create(
                    "Returns rate 1 for the base currency; otherwise resolves the latest active rate dated on or before the requested date.",
                    "`currency` and document `date`.",
                    "Future rates are never used.",
                    "A missing historical rate returns 400 (`ExchangeRates.Missing`).")),
            nameof(ExchangeRatesController.Create) => (
                "Create an exchange rate",
                SwaggerOperationDescription.Create(
                    "Admin only. Creates one dated rate for the selected company.",
                    "`currency`, `rateDate`, `rate`, optional `source`, and optional `notes`.",
                    "Rate must be positive with at most 12 decimal places. Base-currency rows are forbidden.",
                    "An active currency/date duplicate returns 409.")),
            nameof(ExchangeRatesController.Update) => (
                "Update an unused exchange rate",
                SwaggerOperationDescription.Create(
                    "Admin only. Updates an unreferenced rate using optimistic concurrency.",
                    "A positive route `id`, complete fields, and the exact returned `rowVersion`.",
                    "Rates referenced by documents are immutable.",
                    "Stale tokens and duplicate dates return 409.")),
            nameof(ExchangeRatesController.PreviewImport) => (
                "Preview external exchange-rate import",
                SwaggerOperationDescription.Create(
                    "Admin only. Fetches CBE/Frankfurter rates for the selected date and currencies without saving them.",
                    "`rateDate` and optional `currencies`; an empty list means every supported currency except the company base currency.",
                    "The response includes the provider, each returned rate, and the actual provider date.",
                    "Provider 404s are reported per currency; provider outages return 502/504.")),
            nameof(ExchangeRatesController.Import) => (
                "Import external exchange rates",
                SwaggerOperationDescription.Create(
                    "Admin only. Fetches CBE/Frankfurter rates, then creates missing imported rates in a serializable transaction.",
                    "`rateDate`, optional `currencies`, and `replaceUnreferencedImportedRates`.",
                    "Manual rates and referenced imported rates are never overwritten; replacement is opt-in for unreferenced imported rates.",
                    "Provider outages, duplicate races, and concurrency conflicts return documented errors.")),
            nameof(ExchangeRatesController.Delete) => (
                "Delete an unused exchange rate",
                SwaggerOperationDescription.Create(
                    "Admin only. Soft-deletes an unreferenced rate.",
                    "A positive route `id`.",
                    "Referenced rates cannot be deleted.",
                    "Referenced rows return 409 (`ExchangeRates.Referenced`).")),
            _ => default
        };

        if (documentation == default)
        {
            return;
        }

        operation.Summary = documentation.Item1;
        operation.Description = documentation.Item2;
        operation.OperationId =
            $"ExchangeRates_{context.MethodInfo.Name}";
    }
}
