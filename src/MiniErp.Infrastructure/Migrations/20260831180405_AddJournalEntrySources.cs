using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalEntrySources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_JournalEntries_EntryType",
                table: "JournalEntries");

            migrationBuilder.AddColumn<int>(
                name: "SourceId",
                table: "JournalEntries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceNumber",
                table: "JournalEntries",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceType",
                table: "JournalEntries",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_JournalEntries_Company_AutomaticSource",
                table: "JournalEntries",
                columns: new[] { "CompanyId", "SourceType", "SourceId" },
                unique: true,
                filter: "[EntryType] = 4 AND [ReversalOfEntryId] IS NULL AND [SourceType] IS NOT NULL AND [SourceId] IS NOT NULL AND [Status] = 1 AND [IsDeleted] = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_JournalEntries_EntryType",
                table: "JournalEntries",
                sql: "[EntryType] IN (1, 2, 3, 4)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_JournalEntries_Source",
                table: "JournalEntries",
                sql: "(([EntryType] = 4 AND [SourceType] IS NOT NULL AND [SourceId] IS NOT NULL) OR ([EntryType] IN (1, 2, 3) AND [SourceType] IS NULL AND [SourceId] IS NULL))");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_JournalEntries_Company_AutomaticSource",
                table: "JournalEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_JournalEntries_EntryType",
                table: "JournalEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_JournalEntries_Source",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "SourceId",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "SourceNumber",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "JournalEntries");

            migrationBuilder.AddCheckConstraint(
                name: "CK_JournalEntries_EntryType",
                table: "JournalEntries",
                sql: "[EntryType] IN (1, 2, 3)");
        }
    }
}
