using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tablo.Api.Migrations
{
    /// <inheritdoc />
    public partial class ChangeTableNumberToInt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TableNumber",
                table: "RestaurantTables",
                type: "integer",
                nullable: false,
                defaultValue: -1);

            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    rec RECORD;
                    assigned_num INTEGER;
                BEGIN
                    UPDATE ""RestaurantTables"" SET ""TableNumber"" = -1;
                    
                    FOR rec IN 
                        SELECT ""Id"", ""RestaurantId"", ""NameOrNumber"" 
                        FROM ""RestaurantTables""
                        ORDER BY ""RestaurantId"", ""Id""
                    LOOP
                        assigned_num := NULLIF(REGEXP_REPLACE(rec.""NameOrNumber"", '\D', '', 'g'), '')::integer;
                        IF assigned_num IS NULL THEN
                            assigned_num := 1;
                        END IF;
                        
                        WHILE EXISTS (SELECT 1 FROM ""RestaurantTables"" WHERE ""RestaurantId"" = rec.""RestaurantId"" AND ""TableNumber"" = assigned_num) LOOP
                            assigned_num := assigned_num + 1;
                        END LOOP;

                        UPDATE ""RestaurantTables"" SET ""TableNumber"" = assigned_num WHERE ""Id"" = rec.""Id"";
                    END LOOP;
                END $$;
            ");

            migrationBuilder.DropIndex(
                name: "IX_RestaurantTables_RestaurantId",
                table: "RestaurantTables");

            migrationBuilder.DropColumn(
                name: "NameOrNumber",
                table: "RestaurantTables");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantTables_RestaurantId_TableNumber",
                table: "RestaurantTables",
                columns: new[] { "RestaurantId", "TableNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RestaurantTables_RestaurantId_TableNumber",
                table: "RestaurantTables");

            migrationBuilder.DropColumn(
                name: "TableNumber",
                table: "RestaurantTables");

            migrationBuilder.AddColumn<string>(
                name: "NameOrNumber",
                table: "RestaurantTables",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantTables_RestaurantId",
                table: "RestaurantTables",
                column: "RestaurantId");
        }
    }
}
