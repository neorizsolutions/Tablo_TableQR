using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Tablo.Api.Data;

namespace Tablo.Api.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261125190000_AddEmployeeSecurityStamp")]
public class AddEmployeeSecurityStamp : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SecurityStamp",
            table: "Employees",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "SecurityStamp",
            table: "Employees");
    }
}
