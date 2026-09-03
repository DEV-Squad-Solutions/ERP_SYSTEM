using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FiscalYearClosingOpeningEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE [JournalEntries] DROP CONSTRAINT [CK_JournalEntries_Source];");
            migrationBuilder.Sql("ALTER TABLE [JournalEntries] ADD CONSTRAINT [CK_JournalEntries_Source] CHECK ((([EntryType] = 4 AND [SourceType] IS NOT NULL AND [SourceId] IS NOT NULL) OR ([EntryType] = 3 AND (([SourceType] = 13 AND [SourceId] IS NOT NULL) OR ([SourceType] IS NULL AND [SourceId] IS NULL))) OR ([EntryType] IN (1, 2) AND [SourceType] IS NULL AND [SourceId] IS NULL)));");
            migrationBuilder.Sql("CREATE UNIQUE INDEX [UX_JournalEntries_Company_FiscalYearClosing] ON [JournalEntries] ([CompanyId], [SourceType], [SourceId]) WHERE [EntryType] = 3 AND [SourceType] = 13 AND [SourceId] IS NOT NULL AND [IsDeleted] = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX [UX_JournalEntries_Company_FiscalYearClosing] ON [JournalEntries];");
            migrationBuilder.Sql("ALTER TABLE [JournalEntries] DROP CONSTRAINT [CK_JournalEntries_Source];");
            migrationBuilder.Sql("ALTER TABLE [JournalEntries] ADD CONSTRAINT [CK_JournalEntries_Source] CHECK ((([EntryType] = 4 AND [SourceType] IS NOT NULL AND [SourceId] IS NOT NULL) OR ([EntryType] IN (1, 2, 3) AND [SourceType] IS NULL AND [SourceId] IS NULL)));");
        }
    }
}
