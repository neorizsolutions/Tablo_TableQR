using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tablo.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDiningGroupsAndPaymentLedgerFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentRevision",
                table: "TableBills",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_TableBills_RestaurantId_Id",
                table: "TableBills",
                columns: new[] { "RestaurantId", "Id" });

            migrationBuilder.CreateTable(
                name: "BillPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TableBillId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorEmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CollectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcknowledgementJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillPayments", x => x.Id);
                    table.CheckConstraint("CK_BillPayments_PositiveAmount", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_BillPayments_Employees_RestaurantId_ActorEmployeeId",
                        columns: x => new { x.RestaurantId, x.ActorEmployeeId },
                        principalTable: "Employees",
                        principalColumns: new[] { "RestaurantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BillPayments_TableBills_RestaurantId_TableBillId",
                        columns: x => new { x.RestaurantId, x.TableBillId },
                        principalTable: "TableBills",
                        principalColumns: new[] { "RestaurantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DiningGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TableId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    CreatedByEmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreationOperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreationHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiningGroups", x => x.Id);
                    table.UniqueConstraint("AK_DiningGroups_RestaurantId_Id", x => new { x.RestaurantId, x.Id });
                    table.ForeignKey(
                        name: "FK_DiningGroups_Employees_RestaurantId_CreatedByEmployeeId",
                        columns: x => new { x.RestaurantId, x.CreatedByEmployeeId },
                        principalTable: "Employees",
                        principalColumns: new[] { "RestaurantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DiningGroups_RestaurantTables_RestaurantId_TableId",
                        columns: x => new { x.RestaurantId, x.TableId },
                        principalTable: "RestaurantTables",
                        principalColumns: new[] { "RestaurantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BillPayments_RestaurantId_ActorEmployeeId",
                table: "BillPayments",
                columns: new[] { "RestaurantId", "ActorEmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_BillPayments_RestaurantId_CollectedAtUtc",
                table: "BillPayments",
                columns: new[] { "RestaurantId", "CollectedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BillPayments_RestaurantId_OperationId",
                table: "BillPayments",
                columns: new[] { "RestaurantId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BillPayments_RestaurantId_TableBillId",
                table: "BillPayments",
                columns: new[] { "RestaurantId", "TableBillId" });

            migrationBuilder.CreateIndex(
                name: "IX_DiningGroups_RestaurantId_CreatedByEmployeeId",
                table: "DiningGroups",
                columns: new[] { "RestaurantId", "CreatedByEmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_DiningGroups_RestaurantId_CreationOperationId",
                table: "DiningGroups",
                columns: new[] { "RestaurantId", "CreationOperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiningGroups_RestaurantId_TableId_Label",
                table: "DiningGroups",
                columns: new[] { "RestaurantId", "TableId", "Label" },
                unique: true,
                filter: "\"ClosedAtUtc\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BillPayments");

            migrationBuilder.DropTable(
                name: "DiningGroups");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_TableBills_RestaurantId_Id",
                table: "TableBills");

            migrationBuilder.DropColumn(
                name: "PaymentRevision",
                table: "TableBills");
        }
    }
}
