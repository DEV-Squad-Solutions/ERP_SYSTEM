using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260809170000_RemoveRealtimeOutbox")]
public sealed class RemoveRealtimeOutbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'[RealtimeOutboxMessages]', N'U') IS NOT NULL
               AND EXISTS (
                   SELECT 1
                   FROM [RealtimeOutboxMessages]
                   WHERE [DispatchedAtUtc] IS NULL)
            BEGIN
                THROW 51000, 'Cannot remove RealtimeOutboxMessages while pending messages exist. Stop the old dispatcher, drain or explicitly handle pending rows, then retry the migration.', 1;
            END;

            IF OBJECT_ID(N'[RealtimeOutboxMessages]', N'U') IS NOT NULL
            BEGIN
                DROP TABLE [RealtimeOutboxMessages];
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "RealtimeOutboxMessages",
            columns: table => new
            {
                Id = table.Column<Guid>(
                    type: "uniqueidentifier",
                    nullable: false),
                CompanyId = table.Column<int>(
                    type: "int",
                    nullable: false),
                OccurredAtUtc = table.Column<DateTime>(
                    type: "datetime2",
                    nullable: false),
                Payload = table.Column<string>(
                    type: "nvarchar(max)",
                    nullable: false),
                DispatchedAtUtc = table.Column<DateTime>(
                    type: "datetime2",
                    nullable: true),
                AttemptCount = table.Column<int>(
                    type: "int",
                    nullable: false),
                NextAttemptAtUtc = table.Column<DateTime>(
                    type: "datetime2",
                    nullable: true),
                LastError = table.Column<string>(
                    type: "nvarchar(2000)",
                    maxLength: 2000,
                    nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_RealtimeOutboxMessages",
                    column => column.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_RealtimeOutboxMessages_CompanyId_OccurredAtUtc",
            table: "RealtimeOutboxMessages",
            columns: ["CompanyId", "OccurredAtUtc"]);

        migrationBuilder.CreateIndex(
            name: "IX_RealtimeOutboxMessages_Dispatch",
            table: "RealtimeOutboxMessages",
            columns:
            [
                "DispatchedAtUtc",
                "NextAttemptAtUtc",
                "OccurredAtUtc"
            ]);
    }
}
