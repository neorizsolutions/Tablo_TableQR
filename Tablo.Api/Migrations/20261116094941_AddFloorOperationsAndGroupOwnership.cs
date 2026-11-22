using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tablo.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFloorOperationsAndGroupOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_TableBills_RestaurantId_TableId_TableBillId",
                table: "Orders");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_TableBills_RestaurantId_TableId_Id",
                table: "TableBills");

            migrationBuilder.DropIndex(
                name: "IX_TableBills_RestaurantId_TableId",
                table: "TableBills");

            migrationBuilder.DropIndex(
                name: "IX_Orders_RestaurantId_TableId_TableBillId",
                table: "Orders");

            migrationBuilder.AddColumn<string>(
                name: "AllocationJson",
                table: "TableBills",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DiningGroupId",
                table: "TableBills",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GroupLabelSnapshot",
                table: "TableBills",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentBillId",
                table: "TableBills",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SplitAtUtc",
                table: "TableBills",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DiningGroupId",
                table: "ServiceRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DiningGroupId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GroupLabel",
                table: "Orders",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GroupLabelSnapshot",
                table: "BillPayments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TableIdSnapshot",
                table: "BillPayments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "TableNumberSnapshot",
                table: "BillPayments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Foundation installations can already contain ledger collections.
            // Preserve their known location before transfers become available.
            migrationBuilder.Sql("""
                UPDATE "BillPayments" AS p
                SET "TableIdSnapshot" = b."TableId",
                    "TableNumberSnapshot" = b."TableNumberSnapshot",
                    "GroupLabelSnapshot" = b."GroupLabelSnapshot"
                FROM "TableBills" AS b
                WHERE p."RestaurantId" = b."RestaurantId" AND p."TableBillId" = b."Id";
                """);

            migrationBuilder.CreateTable(
                name: "FloorOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorEmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FloorOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FloorOperations_Employees_RestaurantId_ActorEmployeeId",
                        columns: x => new { x.RestaurantId, x.ActorEmployeeId },
                        principalTable: "Employees",
                        principalColumns: new[] { "RestaurantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TableBills_RestaurantId_DiningGroupId",
                table: "TableBills",
                columns: new[] { "RestaurantId", "DiningGroupId" });

            migrationBuilder.CreateIndex(
                name: "IX_TableBills_RestaurantId_ParentBillId",
                table: "TableBills",
                columns: new[] { "RestaurantId", "ParentBillId" });

            migrationBuilder.CreateIndex(
                name: "IX_TableBills_RestaurantId_TableId_DiningGroupId",
                table: "TableBills",
                columns: new[] { "RestaurantId", "TableId", "DiningGroupId" },
                unique: true,
                filter: "\"SettledAtUtc\" IS NULL AND \"SplitAtUtc\" IS NULL AND \"PaymentRevision\" IS NULL AND \"ParentBillId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TableBills_UngroupedOrdering",
                table: "TableBills",
                columns: new[] { "RestaurantId", "TableId" },
                unique: true,
                filter: "\"DiningGroupId\" IS NULL AND \"SettledAtUtc\" IS NULL AND \"SplitAtUtc\" IS NULL AND \"PaymentRevision\" IS NULL AND \"ParentBillId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_RestaurantId_DiningGroupId",
                table: "Orders",
                columns: new[] { "RestaurantId", "DiningGroupId" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_RestaurantId_TableBillId",
                table: "Orders",
                columns: new[] { "RestaurantId", "TableBillId" });

            migrationBuilder.CreateIndex(
                name: "IX_FloorOperations_RestaurantId_ActorEmployeeId",
                table: "FloorOperations",
                columns: new[] { "RestaurantId", "ActorEmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_FloorOperations_RestaurantId_OperationId",
                table: "FloorOperations",
                columns: new[] { "RestaurantId", "OperationId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_DiningGroups_RestaurantId_DiningGroupId",
                table: "Orders",
                columns: new[] { "RestaurantId", "DiningGroupId" },
                principalTable: "DiningGroups",
                principalColumns: new[] { "RestaurantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_TableBills_RestaurantId_TableBillId",
                table: "Orders",
                columns: new[] { "RestaurantId", "TableBillId" },
                principalTable: "TableBills",
                principalColumns: new[] { "RestaurantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TableBills_DiningGroups_RestaurantId_DiningGroupId",
                table: "TableBills",
                columns: new[] { "RestaurantId", "DiningGroupId" },
                principalTable: "DiningGroups",
                principalColumns: new[] { "RestaurantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TableBills_TableBills_RestaurantId_ParentBillId",
                table: "TableBills",
                columns: new[] { "RestaurantId", "ParentBillId" },
                principalTable: "TableBills",
                principalColumns: new[] { "RestaurantId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_DiningGroups_RestaurantId_DiningGroupId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_TableBills_RestaurantId_TableBillId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_TableBills_DiningGroups_RestaurantId_DiningGroupId",
                table: "TableBills");

            migrationBuilder.DropForeignKey(
                name: "FK_TableBills_TableBills_RestaurantId_ParentBillId",
                table: "TableBills");

            migrationBuilder.DropTable(
                name: "FloorOperations");

            migrationBuilder.DropIndex(
                name: "IX_TableBills_RestaurantId_DiningGroupId",
                table: "TableBills");

            migrationBuilder.DropIndex(
                name: "IX_TableBills_RestaurantId_ParentBillId",
                table: "TableBills");

            migrationBuilder.DropIndex(
                name: "IX_TableBills_RestaurantId_TableId_DiningGroupId",
                table: "TableBills");

            migrationBuilder.DropIndex(
                name: "IX_TableBills_UngroupedOrdering",
                table: "TableBills");

            migrationBuilder.DropIndex(
                name: "IX_Orders_RestaurantId_DiningGroupId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_RestaurantId_TableBillId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "AllocationJson",
                table: "TableBills");

            migrationBuilder.DropColumn(
                name: "DiningGroupId",
                table: "TableBills");

            migrationBuilder.DropColumn(
                name: "GroupLabelSnapshot",
                table: "TableBills");

            migrationBuilder.DropColumn(
                name: "ParentBillId",
                table: "TableBills");

            migrationBuilder.DropColumn(
                name: "SplitAtUtc",
                table: "TableBills");

            migrationBuilder.DropColumn(
                name: "DiningGroupId",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "DiningGroupId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "GroupLabel",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "GroupLabelSnapshot",
                table: "BillPayments");

            migrationBuilder.DropColumn(
                name: "TableIdSnapshot",
                table: "BillPayments");

            migrationBuilder.DropColumn(
                name: "TableNumberSnapshot",
                table: "BillPayments");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_TableBills_RestaurantId_TableId_Id",
                table: "TableBills",
                columns: new[] { "RestaurantId", "TableId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_TableBills_RestaurantId_TableId",
                table: "TableBills",
                columns: new[] { "RestaurantId", "TableId" },
                unique: true,
                filter: "\"SettledAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_RestaurantId_TableId_TableBillId",
                table: "Orders",
                columns: new[] { "RestaurantId", "TableId", "TableBillId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_TableBills_RestaurantId_TableId_TableBillId",
                table: "Orders",
                columns: new[] { "RestaurantId", "TableId", "TableBillId" },
                principalTable: "TableBills",
                principalColumns: new[] { "RestaurantId", "TableId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
