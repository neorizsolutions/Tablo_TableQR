using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Tablo.Api.Data;
namespace Tablo.Api.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261125090000_AddEmployeeMenuAccess")]
public class AddEmployeeMenuAccess : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AddColumn<string[]>(
        name: "AllowedMenuIds", table: "Employees", type: "text[]", nullable: true);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn(
        name: "AllowedMenuIds", table: "Employees");
}
