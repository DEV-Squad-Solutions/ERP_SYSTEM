using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AllowPendingInboundSalesReturnCost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ItemMovements_PendingWithinOutbound",
                table: "ItemMovements");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ItemMovements_PendingWithinMovement",
                table: "ItemMovements",
                sql: "[PendingCostQuantity] <= CASE WHEN [QuantityIn] > 0 THEN [QuantityIn] ELSE [QuantityOut] END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ItemMovements_PendingWithinMovement",
                table: "ItemMovements");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ItemMovements_PendingWithinOutbound",
                table: "ItemMovements",
                sql: "[PendingCostQuantity] <= [QuantityOut]");
        }
    }
}
