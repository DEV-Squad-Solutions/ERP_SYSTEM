using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFiscalYearOwnershipToExchangeRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExchangeRates_CompanyId_Currency_RateDate",
                table: "ExchangeRates");

            migrationBuilder.DropIndex(
                name: "IX_ExchangeRates_CompanyId_Currency_RateDate_Id",
                table: "ExchangeRates");

            migrationBuilder.AddColumn<int>(
                name: "FiscalYearId",
                table: "ExchangeRates",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM ExchangeRates AS rate
                    OUTER APPLY (
                        SELECT COUNT_BIG(*) AS MatchCount
                        FROM FiscalYears AS fiscalYear
                        WHERE fiscalYear.CompanyId = rate.CompanyId
                            AND rate.RateDate
                                BETWEEN fiscalYear.StartDate AND fiscalYear.EndDate
                    ) AS matches
                    WHERE matches.MatchCount <> 1)
                BEGIN
                    THROW 51002,
                        'Cannot assign exactly one fiscal year to every ExchangeRates row.',
                        1;
                END;

                UPDATE rate
                SET rate.FiscalYearId = fiscalYear.Id
                FROM ExchangeRates AS rate
                INNER JOIN FiscalYears AS fiscalYear
                    ON fiscalYear.CompanyId = rate.CompanyId
                    AND rate.RateDate
                        BETWEEN fiscalYear.StartDate AND fiscalYear.EndDate;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_Company_FiscalYear",
                table: "ExchangeRates",
                columns: new[] { "CompanyId", "FiscalYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_CompanyId_FiscalYearId_Currency_RateDate",
                table: "ExchangeRates",
                columns: new[] { "CompanyId", "FiscalYearId", "Currency", "RateDate" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_CompanyId_FiscalYearId_Currency_RateDate_Id",
                table: "ExchangeRates",
                columns: new[] { "CompanyId", "FiscalYearId", "Currency", "RateDate", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_ExchangeRates_FiscalYears_CompanyId_FiscalYearId",
                table: "ExchangeRates",
                columns: new[] { "CompanyId", "FiscalYearId" },
                principalTable: "FiscalYears",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExchangeRates_FiscalYears_CompanyId_FiscalYearId",
                table: "ExchangeRates");

            migrationBuilder.DropIndex(
                name: "IX_ExchangeRates_Company_FiscalYear",
                table: "ExchangeRates");

            migrationBuilder.DropIndex(
                name: "IX_ExchangeRates_CompanyId_FiscalYearId_Currency_RateDate",
                table: "ExchangeRates");

            migrationBuilder.DropIndex(
                name: "IX_ExchangeRates_CompanyId_FiscalYearId_Currency_RateDate_Id",
                table: "ExchangeRates");

            migrationBuilder.DropColumn(
                name: "FiscalYearId",
                table: "ExchangeRates");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_CompanyId_Currency_RateDate",
                table: "ExchangeRates",
                columns: new[] { "CompanyId", "Currency", "RateDate" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_CompanyId_Currency_RateDate_Id",
                table: "ExchangeRates",
                columns: new[] { "CompanyId", "Currency", "RateDate", "Id" });
        }
    }
}
