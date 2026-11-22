using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tablo.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionsPlansAndAuditLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    MonthlyPriceInr = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxOutlets = table.Column<int>(type: "integer", nullable: false),
                    MaxTables = table.Column<int>(type: "integer", nullable: false),
                    FeaturesJson = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AutoRenew = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subscriptions_Plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Subscriptions_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorEmail = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FromPlanId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToPlanId = table.Column<Guid>(type: "uuid", nullable: true),
                    FromStatus = table.Column<int>(type: "integer", nullable: true),
                    ToStatus = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    DetailsJson = table.Column<string>(type: "text", nullable: true),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionAuditLogs_Plans_FromPlanId",
                        column: x => x.FromPlanId,
                        principalTable: "Plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubscriptionAuditLogs_Plans_ToPlanId",
                        column: x => x.ToPlanId,
                        principalTable: "Plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubscriptionAuditLogs_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubscriptionAuditLogs_Subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "Subscriptions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Plans_Code",
                table: "Plans",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionAuditLogs_FromPlanId",
                table: "SubscriptionAuditLogs",
                column: "FromPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionAuditLogs_RestaurantId_TimestampUtc",
                table: "SubscriptionAuditLogs",
                columns: new[] { "RestaurantId", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionAuditLogs_SubscriptionId",
                table: "SubscriptionAuditLogs",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionAuditLogs_ToPlanId",
                table: "SubscriptionAuditLogs",
                column: "ToPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_PlanId",
                table: "Subscriptions",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_RestaurantId",
                table: "Subscriptions",
                column: "RestaurantId",
                unique: true);

            // Seed default plans and backfill existing restaurants
            migrationBuilder.Sql(@"
INSERT INTO ""Plans"" (""Id"", ""Code"", ""Name"", ""Description"", ""MonthlyPriceInr"", ""MaxOutlets"", ""MaxTables"", ""FeaturesJson"", ""IsActive"", ""DisplayOrder"", ""CreatedAtUtc"", ""UpdatedAtUtc"")
VALUES 
('11111111-1111-1111-1111-111111111111', 'basic', 'Basic Plan', 'QR menu and ordering, kitchen display, standard billing, and basic sales reports.', 999.00, 1, 15, '[""qr_ordering"",""kitchen_display"",""standard_billing"",""basic_reports"",""menu_management"",""tables_qr""]', true, 1, NOW(), NOW()),
('22222222-2222-2222-2222-222222222222', 'plus', 'Plus Plan', 'Basic features plus staff ordering, dining groups, split bills, mixed payments, table transfers, and advanced payment reports.', 2499.00, 3, 40, '[""qr_ordering"",""kitchen_display"",""standard_billing"",""basic_reports"",""menu_management"",""tables_qr"",""staff_ordering"",""dining_groups"",""split_billing"",""mixed_payments"",""table_transfers"",""advanced_payment_reports""]', true, 2, NOW(), NOW()),
('33333333-3333-3333-3333-333333333333', 'premium', 'Premium Plan', 'Plus features plus inventory, recipe costing, stocktakes, purchasing, batch and expiry tracking, advanced stock and profit reports, and outlet stock transfers.', 4999.00, 10, 100, '[""qr_ordering"",""kitchen_display"",""standard_billing"",""basic_reports"",""menu_management"",""tables_qr"",""staff_ordering"",""dining_groups"",""split_billing"",""mixed_payments"",""table_transfers"",""advanced_payment_reports"",""inventory_core"",""recipes_costing"",""stocktakes"",""purchasing"",""lots_expiry"",""preparations"",""outlet_transfers"",""stock_profit_reports"",""auto_availability""]', true, 3, NOW(), NOW())
ON CONFLICT (""Code"") DO NOTHING;

INSERT INTO ""Subscriptions"" (""Id"", ""RestaurantId"", ""PlanId"", ""Status"", ""StartDateUtc"", ""EndDateUtc"", ""AutoRenew"", ""Notes"", ""CreatedAtUtc"", ""UpdatedAtUtc"")
SELECT 
    gen_random_uuid(), 
    r.""Id"", 
    '33333333-3333-3333-3333-333333333333'::uuid, 
    1, 
    NOW() - INTERVAL '1 month', 
    NOW() + INTERVAL '1 year', 
    true, 
    'Migration backfill to preserve all existing capabilities on rollout.', 
    NOW(), 
    NOW()
FROM ""Restaurants"" r
WHERE NOT EXISTS (SELECT 1 FROM ""Subscriptions"" s WHERE s.""RestaurantId"" = r.""Id"");

INSERT INTO ""SubscriptionAuditLogs"" (""Id"", ""RestaurantId"", ""SubscriptionId"", ""ActorEmail"", ""Action"", ""ToPlanId"", ""ToStatus"", ""Reason"", ""TimestampUtc"")
SELECT 
    gen_random_uuid(),
    s.""RestaurantId"",
    s.""Id"",
    'system@tablo.local',
    'Created',
    s.""PlanId"",
    s.""Status"",
    'Automatic migration backfill to preserve full restaurant capabilities on subscription rollout.',
    NOW()
FROM ""Subscriptions"" s
WHERE NOT EXISTS (SELECT 1 FROM ""SubscriptionAuditLogs"" a WHERE a.""RestaurantId"" = s.""RestaurantId"");
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubscriptionAuditLogs");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropTable(
                name: "Plans");
        }
    }
}
