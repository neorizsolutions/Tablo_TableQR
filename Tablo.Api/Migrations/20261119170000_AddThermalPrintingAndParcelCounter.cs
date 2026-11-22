using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tablo.Api.Migrations
{
    public partial class AddThermalPrintingAndParcelCounter : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add columns to Plans
            migrationBuilder.AddColumn<int>(
                name: "MaxKitchenStations",
                table: "Plans",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            // 2. Create ThermalPrinters
            migrationBuilder.CreateTable(
                name: "ThermalPrinters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ConnectionType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Port = table.Column<int>(type: "integer", nullable: false, defaultValue: 9100),
                    LocalQueueName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PaperWidthMm = table.Column<int>(type: "integer", nullable: false, defaultValue: 80),
                    CharactersPerLine = table.Column<int>(type: "integer", nullable: false, defaultValue: 48),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Online"),
                    LastSeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastStatusMessage = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThermalPrinters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ThermalPrinters_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // 3. Create KitchenStations
            migrationBuilder.CreateTable(
                name: "KitchenStations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PrinterId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KitchenStations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KitchenStations_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KitchenStations_ThermalPrinters_PrinterId",
                        column: x => x.PrinterId,
                        principalTable: "ThermalPrinters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            // 4. Create PrintAgentRegistrations
            migrationBuilder.CreateTable(
                name: "PrintAgentRegistrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: true),
                    AgentName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ApiKeyHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    KeyPrefix = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    LastHeartbeatUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrintAgentRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrintAgentRegistrations_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // 5. Create KotBatches
            migrationBuilder.CreateTable(
                name: "KotBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchSequence = table.Column<int>(type: "integer", nullable: false),
                    KotNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TriggeredBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ActorEmail = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KotBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KotBatches_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KotBatches_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // 6. Create KotTickets
            migrationBuilder.CreateTable(
                name: "KotTickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    KotBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    KitchenStationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StationNameSnapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PrinterId = table.Column<Guid>(type: "uuid", nullable: true),
                    TicketNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    OrderNumber = table.Column<int>(type: "integer", nullable: false),
                    TableId = table.Column<Guid>(type: "uuid", nullable: true),
                    TableNumber = table.Column<int>(type: "integer", nullable: false),
                    DiningGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    GroupLabelSnapshot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TicketType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TimestampIstFormatted = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ItemsJson = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsReprint = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ReprintCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    OriginalTicketId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReprintReason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReprinterEmail = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KotTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KotTickets_KitchenStations_KitchenStationId",
                        column: x => x.KitchenStationId,
                        principalTable: "KitchenStations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KotTickets_KotBatches_KotBatchId",
                        column: x => x.KotBatchId,
                        principalTable: "KotBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KotTickets_KotTickets_OriginalTicketId",
                        column: x => x.OriginalTicketId,
                        principalTable: "KotTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_KotTickets_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KotTickets_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KotTickets_ThermalPrinters_PrinterId",
                        column: x => x.PrinterId,
                        principalTable: "ThermalPrinters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            // 7. Create PrintJobs
            migrationBuilder.CreateTable(
                name: "PrintJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    KotTicketId = table.Column<Guid>(type: "uuid", nullable: true),
                    PrinterId = table.Column<Guid>(type: "uuid", nullable: false),
                    StationId = table.Column<Guid>(type: "uuid", nullable: true),
                    JobType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PayloadEscPosBase64 = table.Column<string>(type: "text", nullable: false),
                    ReadableText = table.Column<string>(type: "text", nullable: false),
                    PaperWidthMm = table.Column<int>(type: "integer", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    LastAttemptUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ClaimedByAgentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClaimedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClaimToken = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LeaseExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeliveryState = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Certain"),
                    UncertainReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AttemptHistoryJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrintJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrintJobs_KitchenStations_StationId",
                        column: x => x.StationId,
                        principalTable: "KitchenStations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PrintJobs_KotTickets_KotTicketId",
                        column: x => x.KotTicketId,
                        principalTable: "KotTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PrintJobs_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PrintJobs_ThermalPrinters_PrinterId",
                        column: x => x.PrinterId,
                        principalTable: "ThermalPrinters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // 8. Create DailyTokenSequences
            migrationBuilder.CreateTable(
                name: "DailyTokenSequences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessDate = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    LastTokenNumber = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyTokenSequences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyTokenSequences_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // 9. Add columns to Categories
            migrationBuilder.AddColumn<Guid>(
                name: "KitchenStationId",
                table: "Categories",
                type: "uuid",
                nullable: true);

            // 10. Add columns to MenuItems
            migrationBuilder.AddColumn<Guid>(
                name: "KitchenStationId",
                table: "MenuItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PackagingFee",
                table: "MenuItems",
                type: "numeric(18,2)",
                nullable: true,
                defaultValue: 0m);

            // 11. Alter TableId on Orders to nullable and add parcel columns
            migrationBuilder.AlterColumn<Guid>(
                name: "TableId",
                table: "Orders",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "OrderChannel",
                table: "Orders",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "DineIn");

            migrationBuilder.AddColumn<int>(
                name: "DailyTokenNumber",
                table: "Orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessDate",
                table: "Orders",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerName",
                table: "Orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerPhone",
                table: "Orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmissionOperationId",
                table: "Orders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmissionFingerprint",
                table: "Orders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KotPrintStatus",
                table: "Orders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<string>(
                name: "KotPrintError",
                table: "Orders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PackagingCharge",
                table: "Orders",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            // 12. Alter TableId on TableBills to nullable and add parcel columns
            migrationBuilder.AlterColumn<Guid>(
                name: "TableId",
                table: "TableBills",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "OrderChannel",
                table: "TableBills",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "DineIn");

            migrationBuilder.AddColumn<int>(
                name: "DailyTokenNumber",
                table: "TableBills",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessDate",
                table: "TableBills",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PackagingCharge",
                table: "TableBills",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PackagingChargeModeSnapshot",
                table: "TableBills",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PackagingRateSnapshot",
                table: "TableBills",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PackagingWaiverReason",
                table: "TableBills",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PackagingWaivedBy",
                table: "TableBills",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPackagingWaived",
                table: "TableBills",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // 13. Add columns to RestaurantSettings
            migrationBuilder.AddColumn<bool>(
                name: "KotPrintingEnabled",
                table: "RestaurantSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "KitchenSetupMode",
                table: "RestaurantSettings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "SingleKitchen");

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultKitchenStationId",
                table: "RestaurantSettings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParcelPackagingChargeMode",
                table: "RestaurantSettings",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<decimal>(
                name: "ParcelPackagingFlatRate",
                table: "RestaurantSettings",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ParcelPackagingPercentage",
                table: "RestaurantSettings",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "ParcelPackagingTaxable",
                table: "RestaurantSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "ParcelPackagingTaxRate",
                table: "RestaurantSettings",
                type: "numeric(18,2)",
                nullable: true);

            // 14. Create FKs & Indices
            migrationBuilder.CreateIndex(
                name: "IX_Categories_KitchenStationId",
                table: "Categories",
                column: "KitchenStationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_KitchenStations_KitchenStationId",
                table: "Categories",
                column: "KitchenStationId",
                principalTable: "KitchenStations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_KitchenStationId",
                table: "MenuItems",
                column: "KitchenStationId");

            migrationBuilder.AddForeignKey(
                name: "FK_MenuItems_KitchenStations_KitchenStationId",
                table: "MenuItems",
                column: "KitchenStationId",
                principalTable: "KitchenStations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantSettings_DefaultKitchenStationId",
                table: "RestaurantSettings",
                column: "DefaultKitchenStationId");

            migrationBuilder.AddForeignKey(
                name: "FK_RestaurantSettings_KitchenStations_DefaultKitchenStationId",
                table: "RestaurantSettings",
                column: "DefaultKitchenStationId",
                principalTable: "KitchenStations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.CreateIndex(
                name: "IX_DailyTokenSequences_RestaurantId_BusinessDate",
                table: "DailyTokenSequences",
                columns: new[] { "RestaurantId", "BusinessDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KitchenStations_PrinterId",
                table: "KitchenStations",
                column: "PrinterId");

            migrationBuilder.CreateIndex(
                name: "IX_KitchenStations_RestaurantId_Code",
                table: "KitchenStations",
                columns: new[] { "RestaurantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KitchenStations_RestaurantId_DisplayOrder",
                table: "KitchenStations",
                columns: new[] { "RestaurantId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_KotBatches_OrderId",
                table: "KotBatches",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_KotBatches_RestaurantId_KotNumber",
                table: "KotBatches",
                columns: new[] { "RestaurantId", "KotNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KotBatches_RestaurantId_OrderId_BatchSequence",
                table: "KotBatches",
                columns: new[] { "RestaurantId", "OrderId", "BatchSequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KotTickets_KitchenStationId",
                table: "KotTickets",
                column: "KitchenStationId");

            migrationBuilder.CreateIndex(
                name: "IX_KotTickets_KotBatchId",
                table: "KotTickets",
                column: "KotBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_KotTickets_OriginalTicketId",
                table: "KotTickets",
                column: "OriginalTicketId");

            migrationBuilder.CreateIndex(
                name: "IX_KotTickets_PrinterId",
                table: "KotTickets",
                column: "PrinterId");

            migrationBuilder.CreateIndex(
                name: "IX_KotTickets_RestaurantId_KitchenStationId_CreatedAtUtc",
                table: "KotTickets",
                columns: new[] { "RestaurantId", "KitchenStationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_KotTickets_RestaurantId_OrderId",
                table: "KotTickets",
                columns: new[] { "RestaurantId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_PrintAgentRegistrations_ApiKeyHash",
                table: "PrintAgentRegistrations",
                column: "ApiKeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrintAgentRegistrations_RestaurantId_IsActive",
                table: "PrintAgentRegistrations",
                columns: new[] { "RestaurantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_KotTicketId",
                table: "PrintJobs",
                column: "KotTicketId");

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_PrinterId_Status_CreatedAtUtc",
                table: "PrintJobs",
                columns: new[] { "PrinterId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_RestaurantId_PrinterId_Status",
                table: "PrintJobs",
                columns: new[] { "RestaurantId", "PrinterId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_RestaurantId_Status_LeaseExpiresAtUtc",
                table: "PrintJobs",
                columns: new[] { "RestaurantId", "Status", "LeaseExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_StationId",
                table: "PrintJobs",
                column: "StationId");

            migrationBuilder.CreateIndex(
                name: "IX_ThermalPrinters_RestaurantId_IsActive",
                table: "ThermalPrinters",
                columns: new[] { "RestaurantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ThermalPrinters_RestaurantId_Name",
                table: "ThermalPrinters",
                columns: new[] { "RestaurantId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_RestaurantId_BusinessDate_DailyTokenNumber",
                table: "Orders",
                columns: new[] { "RestaurantId", "BusinessDate", "DailyTokenNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_RestaurantId_SubmissionOperationId",
                table: "Orders",
                columns: new[] { "RestaurantId", "SubmissionOperationId" },
                unique: true,
                filter: "\"SubmissionOperationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TableBills_RestaurantId_BusinessDate_DailyTokenNumber",
                table: "TableBills",
                columns: new[] { "RestaurantId", "BusinessDate", "DailyTokenNumber" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PrintJobs");
            migrationBuilder.DropTable(name: "KotTickets");
            migrationBuilder.DropTable(name: "KotBatches");
            migrationBuilder.DropTable(name: "PrintAgentRegistrations");
            migrationBuilder.DropTable(name: "DailyTokenSequences");

            migrationBuilder.DropForeignKey(
                name: "FK_Categories_KitchenStations_KitchenStationId",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_MenuItems_KitchenStations_KitchenStationId",
                table: "MenuItems");

            migrationBuilder.DropForeignKey(
                name: "FK_RestaurantSettings_KitchenStations_DefaultKitchenStationId",
                table: "RestaurantSettings");

            migrationBuilder.DropTable(name: "KitchenStations");
            migrationBuilder.DropTable(name: "ThermalPrinters");

            migrationBuilder.DropIndex(name: "IX_Categories_KitchenStationId", table: "Categories");
            migrationBuilder.DropColumn(name: "KitchenStationId", table: "Categories");

            migrationBuilder.DropIndex(name: "IX_MenuItems_KitchenStationId", table: "MenuItems");
            migrationBuilder.DropColumn(name: "KitchenStationId", table: "MenuItems");
            migrationBuilder.DropColumn(name: "PackagingFee", table: "MenuItems");

            migrationBuilder.DropIndex(name: "IX_Orders_RestaurantId_BusinessDate_DailyTokenNumber", table: "Orders");
            migrationBuilder.DropIndex(name: "IX_Orders_RestaurantId_SubmissionOperationId", table: "Orders");
            migrationBuilder.DropColumn(name: "OrderChannel", table: "Orders");
            migrationBuilder.DropColumn(name: "DailyTokenNumber", table: "Orders");
            migrationBuilder.DropColumn(name: "BusinessDate", table: "Orders");
            migrationBuilder.DropColumn(name: "CustomerName", table: "Orders");
            migrationBuilder.DropColumn(name: "CustomerPhone", table: "Orders");
            migrationBuilder.DropColumn(name: "SubmissionOperationId", table: "Orders");
            migrationBuilder.DropColumn(name: "SubmissionFingerprint", table: "Orders");
            migrationBuilder.DropColumn(name: "KotPrintStatus", table: "Orders");
            migrationBuilder.DropColumn(name: "KotPrintError", table: "Orders");
            migrationBuilder.DropColumn(name: "PackagingCharge", table: "Orders");

            migrationBuilder.DropIndex(name: "IX_TableBills_RestaurantId_BusinessDate_DailyTokenNumber", table: "TableBills");
            migrationBuilder.DropColumn(name: "OrderChannel", table: "TableBills");
            migrationBuilder.DropColumn(name: "DailyTokenNumber", table: "TableBills");
            migrationBuilder.DropColumn(name: "BusinessDate", table: "TableBills");
            migrationBuilder.DropColumn(name: "PackagingCharge", table: "TableBills");
            migrationBuilder.DropColumn(name: "PackagingChargeModeSnapshot", table: "TableBills");
            migrationBuilder.DropColumn(name: "PackagingRateSnapshot", table: "TableBills");
            migrationBuilder.DropColumn(name: "PackagingWaiverReason", table: "TableBills");
            migrationBuilder.DropColumn(name: "PackagingWaivedBy", table: "TableBills");
            migrationBuilder.DropColumn(name: "IsPackagingWaived", table: "TableBills");

            migrationBuilder.DropIndex(name: "IX_RestaurantSettings_DefaultKitchenStationId", table: "RestaurantSettings");
            migrationBuilder.DropColumn(name: "KotPrintingEnabled", table: "RestaurantSettings");
            migrationBuilder.DropColumn(name: "KitchenSetupMode", table: "RestaurantSettings");
            migrationBuilder.DropColumn(name: "DefaultKitchenStationId", table: "RestaurantSettings");
            migrationBuilder.DropColumn(name: "ParcelPackagingChargeMode", table: "RestaurantSettings");
            migrationBuilder.DropColumn(name: "ParcelPackagingFlatRate", table: "RestaurantSettings");
            migrationBuilder.DropColumn(name: "ParcelPackagingPercentage", table: "RestaurantSettings");
            migrationBuilder.DropColumn(name: "ParcelPackagingTaxable", table: "RestaurantSettings");
            migrationBuilder.DropColumn(name: "ParcelPackagingTaxRate", table: "RestaurantSettings");

            migrationBuilder.DropColumn(name: "MaxKitchenStations", table: "Plans");
        }
    }
}
