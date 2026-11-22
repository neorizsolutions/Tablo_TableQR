using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tablo.Api.Migrations
{
    public partial class AddKitchenProductionControls : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add columns to Employees
            migrationBuilder.AddColumn<string>(
                name: "ManagerPinHash",
                table: "Employees",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FailedPinAttempts",
                table: "Employees",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PinLockedUntilUtc",
                table: "Employees",
                type: "timestamp with time zone",
                nullable: true);

            // 2. Add columns to MenuItems
            migrationBuilder.AddColumn<bool>(
                name: "IsRefillEligible",
                table: "MenuItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RefillMaxCount",
                table: "MenuItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RefillKitchenStationId",
                table: "MenuItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_RefillKitchenStationId",
                table: "MenuItems",
                column: "RefillKitchenStationId");

            migrationBuilder.AddForeignKey(
                name: "FK_MenuItems_KitchenStations_RefillKitchenStationId",
                table: "MenuItems",
                column: "RefillKitchenStationId",
                principalTable: "KitchenStations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // 3. Add columns to OrderItems
            migrationBuilder.AddColumn<string>(
                name: "ReleaseState",
                table: "OrderItems",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Fired");

            migrationBuilder.AddColumn<string>(
                name: "CourseName",
                table: "OrderItems",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FiredAtUtc",
                table: "OrderItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FiredByEmployeeId",
                table: "OrderItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FireOperationId",
                table: "OrderItems",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiersJson",
                table: "OrderItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRefill",
                table: "OrderItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "OriginalOrderItemId",
                table: "OrderItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RefillCount",
                table: "OrderItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CancelledQuantity",
                table: "OrderItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsCancelled",
                table: "OrderItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_FiredByEmployeeId",
                table: "OrderItems",
                column: "FiredByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OriginalOrderItemId",
                table: "OrderItems",
                column: "OriginalOrderItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_Employees_FiredByEmployeeId",
                table: "OrderItems",
                column: "FiredByEmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_OrderItems_OriginalOrderItemId",
                table: "OrderItems",
                column: "OriginalOrderItemId",
                principalTable: "OrderItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // 4. Add columns to KotTickets
            migrationBuilder.AddColumn<string>(
                name: "CourseName",
                table: "KotTickets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            // 5. Create ModifierGroups
            migrationBuilder.CreateTable(
                name: "ModifierGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MinSelections = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    MaxSelections = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModifierGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModifierGroups_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModifierGroups_RestaurantId_DisplayOrder",
                table: "ModifierGroups",
                columns: new[] { "RestaurantId", "DisplayOrder" });

            // 6. Create ModifierOptions
            migrationBuilder.CreateTable(
                name: "ModifierOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifierGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PriceDelta = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IngredientAdjustmentType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "None"),
                    IngredientId = table.Column<Guid>(type: "uuid", nullable: true),
                    IngredientQuantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    OriginalIngredientId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModifierOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModifierOptions_ModifierGroups_ModifierGroupId",
                        column: x => x.ModifierGroupId,
                        principalTable: "ModifierGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModifierOptions_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ModifierOptions_Ingredients_OriginalIngredientId",
                        column: x => x.OriginalIngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModifierOptions_ModifierGroupId_DisplayOrder",
                table: "ModifierOptions",
                columns: new[] { "ModifierGroupId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ModifierOptions_IngredientId",
                table: "ModifierOptions",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_ModifierOptions_OriginalIngredientId",
                table: "ModifierOptions",
                column: "OriginalIngredientId");

            // 7. Create MenuItemModifierGroups
            migrationBuilder.CreateTable(
                name: "MenuItemModifierGroups",
                columns: table => new
                {
                    MenuItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifierGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItemModifierGroups", x => new { x.MenuItemId, x.ModifierGroupId });
                    table.ForeignKey(
                        name: "FK_MenuItemModifierGroups_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MenuItemModifierGroups_ModifierGroups_ModifierGroupId",
                        column: x => x.ModifierGroupId,
                        principalTable: "ModifierGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemModifierGroups_ModifierGroupId",
                table: "MenuItemModifierGroups",
                column: "ModifierGroupId");

            // 8. Create ItemCancellationLogs
            migrationBuilder.CreateTable(
                name: "ItemCancellationLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    QuantityCancelled = table.Column<int>(type: "integer", nullable: false),
                    PreviousQuantity = table.Column<int>(type: "integer", nullable: false),
                    NewQuantity = table.Column<int>(type: "integer", nullable: false),
                    ProductionOutcome = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "NotCooked"),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RequestedByEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedByEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApproverNameSnapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    WastedCost = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    OperationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemCancellationLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemCancellationLogs_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemCancellationLogs_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemCancellationLogs_OrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "OrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemCancellationLogs_Employees_RequestedByEmployeeId",
                        column: x => x.RequestedByEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ItemCancellationLogs_Employees_ApprovedByEmployeeId",
                        column: x => x.ApprovedByEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItemCancellationLogs_RestaurantId_TimestampUtc",
                table: "ItemCancellationLogs",
                columns: new[] { "RestaurantId", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ItemCancellationLogs_OrderId",
                table: "ItemCancellationLogs",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemCancellationLogs_OrderItemId",
                table: "ItemCancellationLogs",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemCancellationLogs_RequestedByEmployeeId",
                table: "ItemCancellationLogs",
                column: "RequestedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemCancellationLogs_ApprovedByEmployeeId",
                table: "ItemCancellationLogs",
                column: "ApprovedByEmployeeId");

            // 9. Create OrderProductionOperations
            migrationBuilder.CreateTable(
                name: "OrderProductionOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OperationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OriginalResponseJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderProductionOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderProductionOperations_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderProductionOperations_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderProductionOperations_RestaurantId_OperationId",
                table: "OrderProductionOperations",
                columns: new[] { "RestaurantId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderProductionOperations_OrderId",
                table: "OrderProductionOperations",
                column: "OrderId");

            // 10. Create ManagerPinRateLimits
            migrationBuilder.CreateTable(
                name: "ManagerPinRateLimits",
                columns: table => new
                {
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FailedAttempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LockedUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagerPinRateLimits", x => x.RestaurantId);
                    table.ForeignKey(
                        name: "FK_ManagerPinRateLimits_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ManagerPinRateLimits");
            migrationBuilder.DropTable(name: "OrderProductionOperations");
            migrationBuilder.DropTable(name: "ItemCancellationLogs");
            migrationBuilder.DropTable(name: "MenuItemModifierGroups");
            migrationBuilder.DropTable(name: "ModifierOptions");
            migrationBuilder.DropTable(name: "ModifierGroups");

            migrationBuilder.DropForeignKey(name: "FK_OrderItems_Employees_FiredByEmployeeId", table: "OrderItems");
            migrationBuilder.DropForeignKey(name: "FK_OrderItems_OrderItems_OriginalOrderItemId", table: "OrderItems");
            migrationBuilder.DropIndex(name: "IX_OrderItems_FiredByEmployeeId", table: "OrderItems");
            migrationBuilder.DropIndex(name: "IX_OrderItems_OriginalOrderItemId", table: "OrderItems");

            migrationBuilder.DropColumn(name: "ReleaseState", table: "OrderItems");
            migrationBuilder.DropColumn(name: "CourseName", table: "OrderItems");
            migrationBuilder.DropColumn(name: "FiredAtUtc", table: "OrderItems");
            migrationBuilder.DropColumn(name: "FiredByEmployeeId", table: "OrderItems");
            migrationBuilder.DropColumn(name: "FireOperationId", table: "OrderItems");
            migrationBuilder.DropColumn(name: "ModifiersJson", table: "OrderItems");
            migrationBuilder.DropColumn(name: "IsRefill", table: "OrderItems");
            migrationBuilder.DropColumn(name: "OriginalOrderItemId", table: "OrderItems");
            migrationBuilder.DropColumn(name: "RefillCount", table: "OrderItems");
            migrationBuilder.DropColumn(name: "CancelledQuantity", table: "OrderItems");
            migrationBuilder.DropColumn(name: "IsCancelled", table: "OrderItems");

            migrationBuilder.DropForeignKey(name: "FK_MenuItems_KitchenStations_RefillKitchenStationId", table: "MenuItems");
            migrationBuilder.DropIndex(name: "IX_MenuItems_RefillKitchenStationId", table: "MenuItems");
            migrationBuilder.DropColumn(name: "IsRefillEligible", table: "MenuItems");
            migrationBuilder.DropColumn(name: "RefillMaxCount", table: "MenuItems");
            migrationBuilder.DropColumn(name: "RefillKitchenStationId", table: "MenuItems");

            migrationBuilder.DropColumn(name: "CourseName", table: "KotTickets");

            migrationBuilder.DropColumn(name: "ManagerPinHash", table: "Employees");
            migrationBuilder.DropColumn(name: "FailedPinAttempts", table: "Employees");
            migrationBuilder.DropColumn(name: "PinLockedUntilUtc", table: "Employees");
        }
    }
}
