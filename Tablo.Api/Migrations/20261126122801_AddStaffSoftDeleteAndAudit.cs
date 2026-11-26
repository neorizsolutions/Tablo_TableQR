using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tablo.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffSoftDeleteAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Employees_Email",
                table: "Employees");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Employees",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "Employees",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedByEmployeeId",
                table: "Employees",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmployeeAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetEmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    TargetName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TargetRole = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ActorEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DetailsJson = table.Column<string>(type: "text", nullable: true),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeAuditLogs_Employees_RestaurantId_ActorEmployeeId",
                        columns: x => new { x.RestaurantId, x.ActorEmployeeId },
                        principalTable: "Employees",
                        principalColumns: new[] { "RestaurantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeAuditLogs_Employees_RestaurantId_TargetEmployeeId",
                        columns: x => new { x.RestaurantId, x.TargetEmployeeId },
                        principalTable: "Employees",
                        principalColumns: new[] { "RestaurantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeAuditLogs_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_Email",
                table: "Employees",
                column: "Email",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_RestaurantId_IsDeleted_IsActive",
                table: "Employees",
                columns: new[] { "RestaurantId", "IsDeleted", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAuditLogs_RestaurantId_ActorEmployeeId",
                table: "EmployeeAuditLogs",
                columns: new[] { "RestaurantId", "ActorEmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAuditLogs_RestaurantId_TargetEmployeeId",
                table: "EmployeeAuditLogs",
                columns: new[] { "RestaurantId", "TargetEmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAuditLogs_RestaurantId_TimestampUtc",
                table: "EmployeeAuditLogs",
                columns: new[] { "RestaurantId", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAuditLogs_TargetEmployeeId",
                table: "EmployeeAuditLogs",
                column: "TargetEmployeeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_Employees_Email",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_RestaurantId_IsDeleted_IsActive",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "DeletedByEmployeeId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Employees");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_Email",
                table: "Employees",
                column: "Email",
                unique: true);
        }
    }
}
