using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using MiniErp.Api.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Tests.Swagger;

public sealed class AccountingMonitoringSwaggerDocumentationTests
{
    [Fact]
    public void Readiness_DocumentsMonitoringIndicators()
    {
        var operation = new OpenApiOperation();

        new AccountingReadinessSwaggerDocumentation().Apply(
            operation,
            Context(
                typeof(AccountingReadinessController),
                nameof(AccountingReadinessController.Get)));

        Assert.Equal("AccountingReadiness_Get", operation.OperationId);
        Assert.Contains("fiscalYearId", operation.Description);
        Assert.Contains("المكررة", operation.Description);
        Assert.Contains("غير المتوازنة", operation.Description);
        Assert.Contains("الروابط المحاسبية", operation.Description);
        Assert.Contains("المرتبات", operation.Description);
    }

    [Fact]
    public void Backfill_DocumentsAtomicIdempotentOperation()
    {
        var operation = new OpenApiOperation();

        new AccountingReadinessSwaggerDocumentation().Apply(
            operation,
            Context(
                typeof(AccountingReadinessController),
                nameof(AccountingReadinessController.Backfill)));

        Assert.Equal("AccountingReadiness_Backfill", operation.OperationId);
        Assert.Contains("ذرية", operation.Description);
        Assert.Contains("Rollback", operation.Description);
        Assert.Contains("لا تكرر", operation.Description);
        Assert.Contains("createdJournals", operation.Description);
    }

    [Fact]
    public void JournalSelect_DocumentsOperationalAccountExclusion()
    {
        var operation = new OpenApiOperation();

        new AccountingSetupSwaggerDocumentation().Apply(
            operation,
            Context(
                typeof(AccountsController),
                nameof(AccountsController.GetJournalSelect)));

        Assert.Equal("Accounts_GetJournalSelect", operation.OperationId);
        Assert.Contains("fiscalYearId", operation.Description);
        Assert.Contains("المرتبطة بعناصر تشغيلية", operation.Description);
    }

    [Theory]
    [InlineData(typeof(AccountMappingsController))]
    [InlineData(typeof(AccountStatementMappingsController))]
    [InlineData(typeof(FinancialStatementLinesController))]
    public void CompletedAccountingController_IsVisibleInSwagger(
        Type controllerType)
    {
        var settings = controllerType.GetCustomAttribute<
            ApiExplorerSettingsAttribute>();

        Assert.False(settings?.IgnoreApi ?? false);
    }

    private static OperationFilterContext Context(
        Type controllerType,
        string methodName)
    {
        var schemaGenerator = new SchemaGenerator(
            new SchemaGeneratorOptions(),
            new JsonSerializerDataContractResolver(
                new JsonSerializerOptions()));
        return new OperationFilterContext(
            new ApiDescription(),
            schemaGenerator,
            new SchemaRepository(),
            new OpenApiDocument(),
            controllerType.GetMethod(methodName)!);
    }
}
