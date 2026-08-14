using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations;

public partial class SimplifyAutomaticIdentifiers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "EntityIdentifierSequences",
            columns: table => new
            {
                Scope = table.Column<string>(
                    type: "nvarchar(32)",
                    maxLength: 32,
                    nullable: false),
                Prefix = table.Column<string>(
                    type: "nvarchar(16)",
                    maxLength: 16,
                    nullable: false),
                LastNumber = table.Column<int>(
                    type: "int",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_EntityIdentifierSequences",
                    sequence => new
                    {
                        sequence.Scope,
                        sequence.Prefix
                    });
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "EntityIdentifierSequences");
    }
}
