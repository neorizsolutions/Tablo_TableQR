using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tablo.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffOrderingAndBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TableBillId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_RestaurantTables_RestaurantId_Id",
                table: "RestaurantTables",
                columns: new[] { "RestaurantId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Employees_RestaurantId_Id",
                table: "Employees",
                columns: new[] { "RestaurantId", "Id" });

            migrationBuilder.CreateTable(
                name: "StaffOrderSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TableId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorEmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OriginalResponseJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffOrderSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffOrderSubmissions_Employees_RestaurantId_ActorEmployeeId",
                        columns: x => new { x.RestaurantId, x.ActorEmployeeId },
                        principalTable: "Employees",
                        principalColumns: new[] { "RestaurantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffOrderSubmissions_RestaurantTables_RestaurantId_TableId",
                        columns: x => new { x.RestaurantId, x.TableId },
                        principalTable: "RestaurantTables",
                        principalColumns: new[] { "RestaurantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TableBills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TableId = table.Column<Guid>(type: "uuid", nullable: false),
                    TableNumberSnapshot = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SettledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SettledByEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    SettlementOperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    SettlementHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PaymentMethod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Tax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Discount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReceiptJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TableBills", x => x.Id);
                    table.UniqueConstraint("AK_TableBills_RestaurantId_TableId_Id", x => new { x.RestaurantId, x.TableId, x.Id });
                    table.ForeignKey(
                        name: "FK_TableBills_Employees_RestaurantId_SettledByEmployeeId",
                        columns: x => new { x.RestaurantId, x.SettledByEmployeeId },
                        principalTable: "Employees",
                        principalColumns: new[] { "RestaurantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TableBills_RestaurantTables_RestaurantId_TableId",
                        columns: x => new { x.RestaurantId, x.TableId },
                        principalTable: "RestaurantTables",
                        principalColumns: new[] { "RestaurantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_RestaurantId_TableId_TableBillId",
                table: "Orders",
                columns: new[] { "RestaurantId", "TableId", "TableBillId" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffOrderSubmissions_RestaurantId_ActorEmployeeId",
                table: "StaffOrderSubmissions",
                columns: new[] { "RestaurantId", "ActorEmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffOrderSubmissions_RestaurantId_OperationId",
                table: "StaffOrderSubmissions",
                columns: new[] { "RestaurantId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffOrderSubmissions_RestaurantId_TableId",
                table: "StaffOrderSubmissions",
                columns: new[] { "RestaurantId", "TableId" });

            migrationBuilder.CreateIndex(
                name: "IX_TableBills_RestaurantId_SettledAtUtc",
                table: "TableBills",
                columns: new[] { "RestaurantId", "SettledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TableBills_RestaurantId_SettledByEmployeeId",
                table: "TableBills",
                columns: new[] { "RestaurantId", "SettledByEmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_TableBills_RestaurantId_SettlementOperationId",
                table: "TableBills",
                columns: new[] { "RestaurantId", "SettlementOperationId" },
                unique: true,
                filter: "\"SettlementOperationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TableBills_RestaurantId_TableId",
                table: "TableBills",
                columns: new[] { "RestaurantId", "TableId" },
                unique: true,
                filter: "\"SettledAtUtc\" IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_TableBills_RestaurantId_TableId_TableBillId",
                table: "Orders",
                columns: new[] { "RestaurantId", "TableId", "TableBillId" },
                principalTable: "TableBills",
                principalColumns: new[] { "RestaurantId", "TableId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_TableBills_RestaurantId_TableId_TableBillId",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "StaffOrderSubmissions");

            migrationBuilder.DropTable(
                name: "TableBills");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_RestaurantTables_RestaurantId_Id",
                table: "RestaurantTables");

            migrationBuilder.DropIndex(
                name: "IX_Orders_RestaurantId_TableId_TableBillId",
                table: "Orders");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Employees_RestaurantId_Id",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "TableBillId",
                table: "Orders");
        }
    }
}
