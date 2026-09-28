using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MiniErp.Infrastructure.Persistence;

#nullable disable

namespace MiniErp.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260925090000_AddRefreshTokenSecurityStamp")]
public sealed class AddRefreshTokenSecurityStamp : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SecurityStampSnapshot",
            table: "RefreshTokens",
            type: "nvarchar(256)",
            maxLength: 256,
            nullable: false,
            defaultValue: string.Empty);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "SecurityStampSnapshot",
            table: "RefreshTokens");
    }
}
