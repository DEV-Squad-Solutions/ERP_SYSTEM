using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using MiniErp.Infrastructure.Migrations;

namespace MiniErp.Tests.Migrations;

public sealed class MergeMigrationRegressionTests
{
    [Fact]
    public void CashVoucherEmployeeMigration_IsIdempotent()
    {
        var migration = new AddCashVoucherEmployeeParty();

        var operation = Assert.Single(
            migration.UpOperations.OfType<SqlOperation>());

        Assert.Contains(
            "COL_LENGTH(N'dbo.CashVouchers', N'EmployeeId') IS NULL",
            operation.Sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "IF NOT EXISTS",
            operation.Sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "FK_CashVouchers_Employees_CompanyId_EmployeeId",
            operation.Sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "[PartyType] = 5",
            operation.Sql,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CashVoucherCompatibilityMigration_IsAnEmptyMarker()
    {
        var migration =
            new AddCashVoucherEmployeePartyCompatibilityMarker();

        Assert.Empty(migration.UpOperations);
        Assert.Empty(migration.DownOperations);

        Assert.Equal(
            "20260818100507_AddCashVoucherEmployeeParty",
            GetMigrationId(typeof(AddCashVoucherEmployeeParty)));
        Assert.Equal(
            "20260821071713_AddCashVoucherEmployeeParty",
            GetMigrationId(
                typeof(AddCashVoucherEmployeePartyCompatibilityMarker)));
    }

    [Fact]
    public void EmployeeRatioMigration_ConvertsDataBeforeAddingConstraints()
    {
        var operations = new modifyEployee().UpOperations;
        var sqlIndex = FindOperationIndex<SqlOperation>(operations);
        var firstAddedConstraintIndex =
            FindOperationIndex<AddCheckConstraintOperation>(operations);
        var sql = ((SqlOperation)operations[sqlIndex]).Sql;

        Assert.True(sqlIndex < firstAddedConstraintIndex);
        Assert.Contains("WHEN 100 THEN 1", sql, StringComparison.Ordinal);
        Assert.Contains("WHEN 75 THEN 2", sql, StringComparison.Ordinal);
        Assert.Contains("WHEN 50 THEN 3", sql, StringComparison.Ordinal);
        Assert.Contains("WHEN 33 THEN 4", sql, StringComparison.Ordinal);
        Assert.Contains("WHEN 25 THEN 5", sql, StringComparison.Ordinal);
        Assert.Contains("WHEN 1 THEN 5", sql, StringComparison.Ordinal);
        Assert.Contains("[WorkDayRatio]", sql, StringComparison.Ordinal);
        Assert.Contains(
            "[WorkDaysDeductionRatio]",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "[WorkOverTimeRatio]",
            sql,
            StringComparison.Ordinal);
    }

    [Fact]
    public void EmployeeRatioMigration_DownRestoresLegacyValuesBeforeConstraints()
    {
        var operations = new modifyEployee().DownOperations;
        var sqlIndex = FindOperationIndex<SqlOperation>(operations);
        var firstAddedConstraintIndex =
            FindOperationIndex<AddCheckConstraintOperation>(operations);
        var sql = ((SqlOperation)operations[sqlIndex]).Sql;

        Assert.True(sqlIndex < firstAddedConstraintIndex);
        Assert.Contains("WHEN 1 THEN 100", sql, StringComparison.Ordinal);
        Assert.Contains("WHEN 2 THEN 75", sql, StringComparison.Ordinal);
        Assert.Contains("WHEN 3 THEN 50", sql, StringComparison.Ordinal);
        Assert.Contains("WHEN 4 THEN 33", sql, StringComparison.Ordinal);
        Assert.Contains("WHEN 5 THEN 25", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void CashVoucherPostingStateMigration_BackfillsExactLegacyPostingRule()
    {
        var operations = new AddCashVoucherPostingState().UpOperations;
        var addColumnIndex = FindOperationIndex<AddColumnOperation>(operations);
        var sqlIndex = FindOperationIndex<SqlOperation>(operations);
        var addColumn = (AddColumnOperation)operations[addColumnIndex];
        var sql = ((SqlOperation)operations[sqlIndex]).Sql;

        Assert.True(addColumnIndex < sqlIndex);
        Assert.Equal("IsPosted", addColumn.Name);
        Assert.Equal("CashVouchers", addColumn.Table);
        Assert.False(addColumn.IsNullable);
        Assert.Equal(false, addColumn.DefaultValue);
        Assert.Contains(
            "[CashMovementTypeId] IS NOT NULL",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "[CashboxTransferId] IS NOT NULL",
            sql,
            StringComparison.Ordinal);
        Assert.DoesNotContain("[InvoiceId]", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("[BaseAmount]", sql, StringComparison.Ordinal);
    }

    private static string GetMigrationId(Type migrationType) =>
        Assert.Single(
            migrationType.GetCustomAttributes(
                typeof(MigrationAttribute),
                inherit: false)
                .Cast<MigrationAttribute>())
            .Id;

    private static int FindOperationIndex<TOperation>(
        IReadOnlyList<MigrationOperation> operations)
        where TOperation : MigrationOperation
    {
        var index = operations
            .Select((operation, index) => (operation, index))
            .First(item => item.operation is TOperation)
            .index;

        return index;
    }
}
