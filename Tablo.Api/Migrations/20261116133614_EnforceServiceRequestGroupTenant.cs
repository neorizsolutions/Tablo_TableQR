using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tablo.Api.Migrations
{
    /// <inheritdoc />
    public partial class EnforceServiceRequestGroupTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ServiceRequests_RestaurantId",
                table: "ServiceRequests");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_RestaurantId_DiningGroupId",
                table: "ServiceRequests",
                columns: new[] { "RestaurantId", "DiningGroupId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceRequests_DiningGroups_RestaurantId_DiningGroupId",
                table: "ServiceRequests",
                columns: new[] { "RestaurantId", "DiningGroupId" },
                principalTable: "DiningGroups",
                principalColumns: new[] { "RestaurantId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiceRequests_DiningGroups_RestaurantId_DiningGroupId",
                table: "ServiceRequests");

            migrationBuilder.DropIndex(
                name: "IX_ServiceRequests_RestaurantId_DiningGroupId",
                table: "ServiceRequests");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_RestaurantId",
                table: "ServiceRequests",
                column: "RestaurantId");
        }
    }
}
