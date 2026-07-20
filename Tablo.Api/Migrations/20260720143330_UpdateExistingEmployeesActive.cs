using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tablo.Api.Migrations
{
    /// <inheritdoc />
    public partial class UpdateExistingEmployeesActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"Employees\" SET \"IsActive\" = true;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
