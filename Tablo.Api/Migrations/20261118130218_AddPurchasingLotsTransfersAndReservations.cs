using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tablo.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchasingLotsTransfersAndReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "ActorEmployeeId",
                table: "Stocktakes",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "OutletId",
                table: "StockMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StockLotId",
                table: "StockMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutoManageAvailability",
                table: "RestaurantSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CostingMethod",
                table: "RestaurantSettings",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsPrepared",
                table: "Ingredients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Outlets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsCentralKitchen = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Outlets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Outlets_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PreparationRecipes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutputIngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedYieldQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreparationRecipes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PreparationRecipes_Ingredients_OutputIngredientId",
                        column: x => x.OutputIngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PreparationRecipes_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContactPerson = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TaxId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Suppliers_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockLots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: true),
                    IngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    LotCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    InitialQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    AvailableQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    ReceivedOrProducedDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SourceReferenceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockLots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockLots_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StockLots_Outlets_OutletId",
                        column: x => x.OutletId,
                        principalTable: "Outlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StockLots_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservedQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ReservedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReleasedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CommittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockReservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockReservations_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StockReservations_OrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "OrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StockReservations_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StockReservations_Outlets_OutletId",
                        column: x => x.OutletId,
                        principalTable: "Outlets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StockReservations_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SourceOutletId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationOutletId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DispatchedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    DispatchedByEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceivedByEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockTransfers_Employees_CreatedByEmployeeId",
                        column: x => x.CreatedByEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StockTransfers_Employees_DispatchedByEmployeeId",
                        column: x => x.DispatchedByEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StockTransfers_Employees_ReceivedByEmployeeId",
                        column: x => x.ReceivedByEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StockTransfers_Outlets_DestinationOutletId",
                        column: x => x.DestinationOutletId,
                        principalTable: "Outlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransfers_Outlets_SourceOutletId",
                        column: x => x.SourceOutletId,
                        principalTable: "Outlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransfers_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PreparationRecipeItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PreparationRecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    InputIngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    YieldPercent = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreparationRecipeItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PreparationRecipeItems_Ingredients_InputIngredientId",
                        column: x => x.InputIngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PreparationRecipeItems_PreparationRecipes_PreparationRecipe~",
                        column: x => x.PreparationRecipeId,
                        principalTable: "PreparationRecipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    OrderDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpectedDeliveryDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedByEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Employees_ApprovedByEmployeeId",
                        column: x => x.ApprovedByEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Employees_CreatedByEmployeeId",
                        column: x => x.CreatedByEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Outlets_OutletId",
                        column: x => x.OutletId,
                        principalTable: "Outlets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierIngredients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackUnit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PackQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    PackUnitType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ConversionFactor = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    LastPurchasePricePerPack = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    NormalizedPricePerBaseUnit = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierIngredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierIngredients_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupplierIngredients_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupplierIngredients_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PreparationBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreparationRecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutputIngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreparedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PreparedByEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Multiplier = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ExpectedYieldQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ActualYieldQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    WasteQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    WasteReason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TotalInputCost = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    CostPerBaseUnit = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    OutputStockLotId = table.Column<Guid>(type: "uuid", nullable: true),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreparationBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PreparationBatches_Employees_PreparedByEmployeeId",
                        column: x => x.PreparedByEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PreparationBatches_Ingredients_OutputIngredientId",
                        column: x => x.OutputIngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PreparationBatches_Outlets_OutletId",
                        column: x => x.OutletId,
                        principalTable: "Outlets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PreparationBatches_PreparationRecipes_PreparationRecipeId",
                        column: x => x.PreparationRecipeId,
                        principalTable: "PreparationRecipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PreparationBatches_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PreparationBatches_StockLots_OutputStockLotId",
                        column: x => x.OutputStockLotId,
                        principalTable: "StockLots",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StockTransferItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StockTransferId = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    DispatchedQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ReceivedQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    DiscrepancyQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    DiscrepancyReason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UnitCostSnapshot = table.Column<decimal>(type: "numeric(18,6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransferItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockTransferItems_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StockTransferItems_StockTransfers_StockTransferId",
                        column: x => x.StockTransferId,
                        principalTable: "StockTransfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: true),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceivedByEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ShippingCharges = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    GrandTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoices_Employees_ReceivedByEmployeeId",
                        column: x => x.ReceivedByEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseInvoices_Outlets_OutletId",
                        column: x => x.OutletId,
                        principalTable: "Outlets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseInvoices_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoices_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoices_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackUnit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OrderedPackQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ReceivedPackQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UnitPricePerPack = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ConversionFactor = table.Column<decimal>(type: "numeric(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderItems_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderItems_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseReturns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReturnNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreditNoteReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReturnDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedByEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    TotalCreditAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseReturns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseReturns_Employees_ProcessedByEmployeeId",
                        column: x => x.ProcessedByEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseReturns_Outlets_OutletId",
                        column: x => x.OutletId,
                        principalTable: "Outlets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseReturns_PurchaseInvoices_PurchaseInvoiceId",
                        column: x => x.PurchaseInvoiceId,
                        principalTable: "PurchaseInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseReturns_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseReturns_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseInvoiceItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    IngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackUnit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ReceivedPackQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ConversionFactor = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    BaseQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    PackPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    LineDiscount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LineTax = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalLineCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    UnitCostPerBaseUnit = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    StockLotId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReturnedBaseQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseInvoiceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoiceItems_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoiceItems_PurchaseInvoices_PurchaseInvoiceId",
                        column: x => x.PurchaseInvoiceId,
                        principalTable: "PurchaseInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoiceItems_PurchaseOrderItems_PurchaseOrderItemId",
                        column: x => x.PurchaseOrderItemId,
                        principalTable: "PurchaseOrderItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseInvoiceItems_StockLots_StockLotId",
                        column: x => x.StockLotId,
                        principalTable: "StockLots",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PurchaseReturnItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseReturnId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseInvoiceItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    StockLotId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReturnQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UnitCostSnapshot = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    TotalCredit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseReturnItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseReturnItems_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseReturnItems_PurchaseInvoiceItems_PurchaseInvoiceIte~",
                        column: x => x.PurchaseInvoiceItemId,
                        principalTable: "PurchaseInvoiceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseReturnItems_PurchaseReturns_PurchaseReturnId",
                        column: x => x.PurchaseReturnId,
                        principalTable: "PurchaseReturns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseReturnItems_StockLots_StockLotId",
                        column: x => x.StockLotId,
                        principalTable: "StockLots",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_OutletId",
                table: "StockMovements",
                column: "OutletId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_StockLotId",
                table: "StockMovements",
                column: "StockLotId");

            migrationBuilder.CreateIndex(
                name: "IX_Outlets_RestaurantId_Code",
                table: "Outlets",
                columns: new[] { "RestaurantId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_PreparationBatches_OutletId",
                table: "PreparationBatches",
                column: "OutletId");

            migrationBuilder.CreateIndex(
                name: "IX_PreparationBatches_OutputIngredientId",
                table: "PreparationBatches",
                column: "OutputIngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_PreparationBatches_OutputStockLotId",
                table: "PreparationBatches",
                column: "OutputStockLotId");

            migrationBuilder.CreateIndex(
                name: "IX_PreparationBatches_PreparationRecipeId",
                table: "PreparationBatches",
                column: "PreparationRecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_PreparationBatches_PreparedByEmployeeId",
                table: "PreparationBatches",
                column: "PreparedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PreparationBatches_RestaurantId",
                table: "PreparationBatches",
                column: "RestaurantId");

            migrationBuilder.CreateIndex(
                name: "IX_PreparationRecipeItems_InputIngredientId",
                table: "PreparationRecipeItems",
                column: "InputIngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_PreparationRecipeItems_PreparationRecipeId",
                table: "PreparationRecipeItems",
                column: "PreparationRecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_PreparationRecipes_OutputIngredientId",
                table: "PreparationRecipes",
                column: "OutputIngredientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PreparationRecipes_RestaurantId_OutputIngredientId",
                table: "PreparationRecipes",
                columns: new[] { "RestaurantId", "OutputIngredientId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceItems_IngredientId",
                table: "PurchaseInvoiceItems",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceItems_PurchaseInvoiceId",
                table: "PurchaseInvoiceItems",
                column: "PurchaseInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceItems_PurchaseOrderItemId",
                table: "PurchaseInvoiceItems",
                column: "PurchaseOrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceItems_StockLotId",
                table: "PurchaseInvoiceItems",
                column: "StockLotId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_OutletId",
                table: "PurchaseInvoices",
                column: "OutletId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_PurchaseOrderId",
                table: "PurchaseInvoices",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_ReceivedByEmployeeId",
                table: "PurchaseInvoices",
                column: "ReceivedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_RestaurantId_SupplierId_InvoiceNumber",
                table: "PurchaseInvoices",
                columns: new[] { "RestaurantId", "SupplierId", "InvoiceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_SupplierId",
                table: "PurchaseInvoices",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderItems_IngredientId",
                table: "PurchaseOrderItems",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderItems_PurchaseOrderId",
                table: "PurchaseOrderItems",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_ApprovedByEmployeeId",
                table: "PurchaseOrders",
                column: "ApprovedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_CreatedByEmployeeId",
                table: "PurchaseOrders",
                column: "CreatedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_OutletId",
                table: "PurchaseOrders",
                column: "OutletId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_RestaurantId_OrderNumber",
                table: "PurchaseOrders",
                columns: new[] { "RestaurantId", "OrderNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_SupplierId",
                table: "PurchaseOrders",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturnItems_IngredientId",
                table: "PurchaseReturnItems",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturnItems_PurchaseInvoiceItemId",
                table: "PurchaseReturnItems",
                column: "PurchaseInvoiceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturnItems_PurchaseReturnId",
                table: "PurchaseReturnItems",
                column: "PurchaseReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturnItems_StockLotId",
                table: "PurchaseReturnItems",
                column: "StockLotId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_OutletId",
                table: "PurchaseReturns",
                column: "OutletId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_ProcessedByEmployeeId",
                table: "PurchaseReturns",
                column: "ProcessedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_PurchaseInvoiceId",
                table: "PurchaseReturns",
                column: "PurchaseInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_RestaurantId_ReturnNumber",
                table: "PurchaseReturns",
                columns: new[] { "RestaurantId", "ReturnNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_SupplierId",
                table: "PurchaseReturns",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_StockLots_IngredientId",
                table: "StockLots",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_StockLots_OutletId",
                table: "StockLots",
                column: "OutletId");

            migrationBuilder.CreateIndex(
                name: "IX_StockLots_RestaurantId_IngredientId_Status",
                table: "StockLots",
                columns: new[] { "RestaurantId", "IngredientId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StockLots_RestaurantId_LotCode",
                table: "StockLots",
                columns: new[] { "RestaurantId", "LotCode" });

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_IngredientId",
                table: "StockReservations",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_OrderId",
                table: "StockReservations",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_OrderItemId",
                table: "StockReservations",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_OutletId",
                table: "StockReservations",
                column: "OutletId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_RestaurantId_IngredientId_Status",
                table: "StockReservations",
                columns: new[] { "RestaurantId", "IngredientId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_RestaurantId_OrderId_Status",
                table: "StockReservations",
                columns: new[] { "RestaurantId", "OrderId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferItems_IngredientId",
                table: "StockTransferItems",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferItems_StockTransferId",
                table: "StockTransferItems",
                column: "StockTransferId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_CreatedByEmployeeId",
                table: "StockTransfers",
                column: "CreatedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_DestinationOutletId",
                table: "StockTransfers",
                column: "DestinationOutletId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_DispatchedByEmployeeId",
                table: "StockTransfers",
                column: "DispatchedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_ReceivedByEmployeeId",
                table: "StockTransfers",
                column: "ReceivedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_RestaurantId_TransferNumber",
                table: "StockTransfers",
                columns: new[] { "RestaurantId", "TransferNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_SourceOutletId",
                table: "StockTransfers",
                column: "SourceOutletId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierIngredients_IngredientId",
                table: "SupplierIngredients",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierIngredients_RestaurantId_SupplierId_IngredientId",
                table: "SupplierIngredients",
                columns: new[] { "RestaurantId", "SupplierId", "IngredientId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierIngredients_SupplierId",
                table: "SupplierIngredients",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_RestaurantId_Name",
                table: "Suppliers",
                columns: new[] { "RestaurantId", "Name" });

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_Outlets_OutletId",
                table: "StockMovements",
                column: "OutletId",
                principalTable: "Outlets",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_StockLots_StockLotId",
                table: "StockMovements",
                column: "StockLotId",
                principalTable: "StockLots",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_Outlets_OutletId",
                table: "StockMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_StockLots_StockLotId",
                table: "StockMovements");

            migrationBuilder.DropTable(
                name: "PreparationBatches");

            migrationBuilder.DropTable(
                name: "PreparationRecipeItems");

            migrationBuilder.DropTable(
                name: "PurchaseReturnItems");

            migrationBuilder.DropTable(
                name: "StockReservations");

            migrationBuilder.DropTable(
                name: "StockTransferItems");

            migrationBuilder.DropTable(
                name: "SupplierIngredients");

            migrationBuilder.DropTable(
                name: "PreparationRecipes");

            migrationBuilder.DropTable(
                name: "PurchaseInvoiceItems");

            migrationBuilder.DropTable(
                name: "PurchaseReturns");

            migrationBuilder.DropTable(
                name: "StockTransfers");

            migrationBuilder.DropTable(
                name: "PurchaseOrderItems");

            migrationBuilder.DropTable(
                name: "StockLots");

            migrationBuilder.DropTable(
                name: "PurchaseInvoices");

            migrationBuilder.DropTable(
                name: "PurchaseOrders");

            migrationBuilder.DropTable(
                name: "Outlets");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_OutletId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_StockLotId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "OutletId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "StockLotId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "AutoManageAvailability",
                table: "RestaurantSettings");

            migrationBuilder.DropColumn(
                name: "CostingMethod",
                table: "RestaurantSettings");

            migrationBuilder.DropColumn(
                name: "IsPrepared",
                table: "Ingredients");

            migrationBuilder.AlterColumn<Guid>(
                name: "ActorEmployeeId",
                table: "Stocktakes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
