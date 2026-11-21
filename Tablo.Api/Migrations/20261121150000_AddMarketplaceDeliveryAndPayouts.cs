using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tablo.Api.Migrations
{
    public partial class AddMarketplaceDeliveryAndPayouts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Columns on Orders
            migrationBuilder.AddColumn<Guid>(
                name: "OutletId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryProvider",
                table: "Orders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalOrderReference",
                table: "Orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedExternalReference",
                table: "Orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MarketplaceCollected",
                table: "Orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MarketplaceEnteredTotal",
                table: "Orders",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MarketplaceEnteredTax",
                table: "Orders",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MarketplaceEnteredDiscount",
                table: "Orders",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MarketplaceEnteredPackaging",
                table: "Orders",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MarketplaceFeeSnapshot",
                table: "Orders",
                type: "numeric(18,2)",
                nullable: true);

            // 2. Columns on TableBills
            migrationBuilder.AddColumn<string>(
                name: "DeliveryProvider",
                table: "TableBills",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalOrderReference",
                table: "TableBills",
                type: "text",
                nullable: true);

            // 3. Foreign Key for Order.OutletId
            migrationBuilder.CreateIndex(
                name: "IX_Orders_OutletId",
                table: "Orders",
                column: "OutletId");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Outlets_OutletId",
                table: "Orders",
                column: "OutletId",
                principalTable: "Outlets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // 4. Unique Index on Orders for Marketplace Delivery Uniqueness
            migrationBuilder.CreateIndex(
                name: "IX_Orders_RestaurantId_OutletId_DeliveryProvider_NormalizedExternalReference",
                table: "Orders",
                columns: new[] { "RestaurantId", "OutletId", "DeliveryProvider", "NormalizedExternalReference" },
                unique: true,
                filter: "\"NormalizedExternalReference\" IS NOT NULL");

            // 5. Create MarketplacePayouts Table
            migrationBuilder.CreateTable(
                name: "MarketplacePayouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeliveryProvider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PayoutReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PayoutDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CommissionDeducted = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TcsGstDeducted = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OtherAdjustments = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LinkedOrderIdsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecordedByEmployeeId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketplacePayouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketplacePayouts_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MarketplacePayouts_Outlets_OutletId",
                        column: x => x.OutletId,
                        principalTable: "Outlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MarketplacePayouts_Employees_RecordedByEmployeeId",
                        column: x => x.RecordedByEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketplacePayouts_OutletId",
                table: "MarketplacePayouts",
                column: "OutletId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketplacePayouts_RecordedByEmployeeId",
                table: "MarketplacePayouts",
                column: "RecordedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketplacePayouts_RestaurantId_DeliveryProvider_PayoutReference",
                table: "MarketplacePayouts",
                columns: new[] { "RestaurantId", "DeliveryProvider", "PayoutReference" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "MarketplacePayouts");

            migrationBuilder.DropIndex(
                name: "IX_Orders_RestaurantId_OutletId_DeliveryProvider_NormalizedExternalReference",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Outlets_OutletId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_OutletId",
                table: "Orders");

            migrationBuilder.DropColumn(name: "OutletId", table: "Orders");
            migrationBuilder.DropColumn(name: "DeliveryProvider", table: "Orders");
            migrationBuilder.DropColumn(name: "ExternalOrderReference", table: "Orders");
            migrationBuilder.DropColumn(name: "NormalizedExternalReference", table: "Orders");
            migrationBuilder.DropColumn(name: "MarketplaceCollected", table: "Orders");
            migrationBuilder.DropColumn(name: "MarketplaceEnteredTotal", table: "Orders");
            migrationBuilder.DropColumn(name: "MarketplaceEnteredTax", table: "Orders");
            migrationBuilder.DropColumn(name: "MarketplaceEnteredDiscount", table: "Orders");
            migrationBuilder.DropColumn(name: "MarketplaceEnteredPackaging", table: "Orders");
            migrationBuilder.DropColumn(name: "MarketplaceFeeSnapshot", table: "Orders");

            migrationBuilder.DropColumn(name: "DeliveryProvider", table: "TableBills");
            migrationBuilder.DropColumn(name: "ExternalOrderReference", table: "TableBills");
        }
    }
}
