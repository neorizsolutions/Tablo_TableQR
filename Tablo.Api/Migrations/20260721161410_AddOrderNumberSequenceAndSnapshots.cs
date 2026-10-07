using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tablo.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderNumberSequenceAndSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "OrderNumber",
                table: "Orders",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<string>(
                name: "ItemNameSnapshot",
                table: "OrderItems",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitPrice",
                table: "OrderItems",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.CreateTable(
                name: "RestaurantOrderSequences",
                columns: table => new
                {
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    NextOrderNumber = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestaurantOrderSequences", x => x.RestaurantId);
                    table.ForeignKey(
                        name: "FK_RestaurantOrderSequences_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_RestaurantId_CreatedAt",
                table: "Orders",
                columns: new[] { "RestaurantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_RestaurantId_OrderNumber",
                table: "Orders",
                columns: new[] { "RestaurantId", "OrderNumber" },
                unique: true);

            // CUSTOM SQL: Backfill ItemNameSnapshot
            migrationBuilder.Sql(@"
                UPDATE ""OrderItems"" oi
                SET ""ItemNameSnapshot"" = m.""Name""
                FROM ""MenuItems"" m
                WHERE oi.""MenuItemId"" = m.""Id"";
            ");

            // CUSTOM SQL: Initialize NextOrderNumber for existing restaurants
            migrationBuilder.Sql(@"
                INSERT INTO ""RestaurantOrderSequences"" (""RestaurantId"", ""NextOrderNumber"")
                SELECT r.""Id"", COALESCE(MAX(o.""OrderNumber""), 0) + 1
                FROM ""Restaurants"" r
                LEFT JOIN ""Orders"" o ON o.""RestaurantId"" = r.""Id""
                GROUP BY r.""Id"";
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RestaurantOrderSequences");

            migrationBuilder.DropIndex(
                name: "IX_Orders_RestaurantId_CreatedAt",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_RestaurantId_OrderNumber",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ItemNameSnapshot",
                table: "OrderItems");

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitPrice",
                table: "OrderItems",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<int>(
                name: "OrderNumber",
                table: "Orders",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
        }
    }
}
