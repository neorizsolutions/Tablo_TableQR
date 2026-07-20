using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tablo.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTableIsActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "RestaurantTables",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "RestaurantTables");
        }
    }
}
