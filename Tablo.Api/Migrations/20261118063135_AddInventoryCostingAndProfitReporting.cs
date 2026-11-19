using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tablo.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryCostingAndProfitReporting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TotalCost",
                table: "StockMovements",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitCostSnapshot",
                table: "StockMovements",
                type: "numeric(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentAverageCost",
                table: "Ingredients",
                type: "numeric(18,6)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "SettledBillItemCosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TableBillId = table.Column<Guid>(type: "uuid", nullable: false),
                    MenuItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    MenuItemNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CategoryNameSnapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPriceSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GrossRevenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AllocatedDiscount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NetRevenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AllocatedTax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalIngredientCost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    GrossProfit = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    GrossMarginPercent = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsCostComplete = table.Column<bool>(type: "boolean", nullable: false),
                    IncompleteReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SettledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettledBillItemCosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SettledBillItemCosts_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SettledBillItemCosts_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SettledBillItemCosts_TableBills_TableBillId",
                        column: x => x.TableBillId,
                        principalTable: "TableBills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SettledBillItemIngredientCosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SettledBillItemCostId = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BaseUnitSnapshot = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RawQuantityPerPortion = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalRawQuantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitCostSnapshot = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    TotalCost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettledBillItemIngredientCosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SettledBillItemIngredientCosts_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SettledBillItemIngredientCosts_SettledBillItemCosts_Settled~",
                        column: x => x.SettledBillItemCostId,
                        principalTable: "SettledBillItemCosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SettledBillItemCosts_MenuItemId",
                table: "SettledBillItemCosts",
                column: "MenuItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SettledBillItemCosts_RestaurantId_MenuItemId",
                table: "SettledBillItemCosts",
                columns: new[] { "RestaurantId", "MenuItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_SettledBillItemCosts_RestaurantId_SettledAtUtc",
                table: "SettledBillItemCosts",
                columns: new[] { "RestaurantId", "SettledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SettledBillItemCosts_RestaurantId_TableBillId",
                table: "SettledBillItemCosts",
                columns: new[] { "RestaurantId", "TableBillId" });

            migrationBuilder.CreateIndex(
                name: "IX_SettledBillItemCosts_TableBillId",
                table: "SettledBillItemCosts",
                column: "TableBillId");

            migrationBuilder.CreateIndex(
                name: "IX_SettledBillItemIngredientCosts_IngredientId",
                table: "SettledBillItemIngredientCosts",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_SettledBillItemIngredientCosts_SettledBillItemCostId",
                table: "SettledBillItemIngredientCosts",
                column: "SettledBillItemCostId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SettledBillItemIngredientCosts");

            migrationBuilder.DropTable(
                name: "SettledBillItemCosts");

            migrationBuilder.DropColumn(
                name: "TotalCost",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "UnitCostSnapshot",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "CurrentAverageCost",
                table: "Ingredients");
        }
    }
}
