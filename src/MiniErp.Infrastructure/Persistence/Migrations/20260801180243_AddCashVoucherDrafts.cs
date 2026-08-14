using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCashVoucherDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "CashboxId",
                table: "CashVouchers",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "CashMovementTypeId",
                table: "CashVouchers",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CashVouchers_PostingReferencesTogether",
                table: "CashVouchers",
                sql: "([CashboxId] IS NULL AND [CashMovementTypeId] IS NULL) OR ([CashboxId] IS NOT NULL AND [CashMovementTypeId] IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CashVouchers_PostingReferencesTogether",
                table: "CashVouchers");

            migrationBuilder.AlterColumn<int>(
                name: "CashboxId",
                table: "CashVouchers",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CashMovementTypeId",
                table: "CashVouchers",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
