using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tablo.Api.Migrations
{
    public partial class AddOutletStorefrontSettingsAndTrackingToken : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""Outlets"" ADD COLUMN IF NOT EXISTS ""IsPublished"" boolean NOT NULL DEFAULT TRUE;
                ALTER TABLE ""Outlets"" ADD COLUMN IF NOT EXISTS ""IsOpen"" boolean NOT NULL DEFAULT TRUE;
                ALTER TABLE ""Outlets"" ADD COLUMN IF NOT EXISTS ""Timezone"" character varying(50) NOT NULL DEFAULT 'Asia/Kolkata';
                ALTER TABLE ""Outlets"" ADD COLUMN IF NOT EXISTS ""OpeningTime"" character varying(10) NOT NULL DEFAULT '11:00';
                ALTER TABLE ""Outlets"" ADD COLUMN IF NOT EXISTS ""ClosingTime"" character varying(10) NOT NULL DEFAULT '23:30';
                ALTER TABLE ""Outlets"" ADD COLUMN IF NOT EXISTS ""IsOvernight"" boolean NOT NULL DEFAULT FALSE;
                ALTER TABLE ""Outlets"" ADD COLUMN IF NOT EXISTS ""LeadTimeMinutes"" integer NOT NULL DEFAULT 25;
                ALTER TABLE ""Outlets"" ADD COLUMN IF NOT EXISTS ""MinOrderAmount"" numeric(18,2) NOT NULL DEFAULT 200.00;
                ALTER TABLE ""Outlets"" ADD COLUMN IF NOT EXISTS ""DeliveryBaseFee"" numeric(18,2) NOT NULL DEFAULT 40.00;
                ALTER TABLE ""Outlets"" ADD COLUMN IF NOT EXISTS ""FreeDeliveryAboveAmount"" numeric(18,2) NOT NULL DEFAULT 799.00;
                ALTER TABLE ""Outlets"" ADD COLUMN IF NOT EXISTS ""Phone"" character varying(50) NOT NULL DEFAULT '+91 98765 43210';
                ALTER TABLE ""Outlets"" ADD COLUMN IF NOT EXISTS ""IsTemporarilyClosed"" boolean NOT NULL DEFAULT FALSE;
                ALTER TABLE ""Outlets"" ADD COLUMN IF NOT EXISTS ""ClosureReason"" character varying(255);

                ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""TrackingToken"" character varying(64);
                CREATE INDEX IF NOT EXISTS ""IX_Orders_TrackingToken"" ON ""Orders"" (""TrackingToken"") WHERE ""TrackingToken"" IS NOT NULL;

                ALTER TABLE ""ModifierOptions"" ADD COLUMN IF NOT EXISTS ""RestaurantId"" uuid;
                UPDATE ""ModifierOptions"" mo SET ""RestaurantId"" = mg.""RestaurantId"" FROM ""ModifierGroups"" mg WHERE mo.""ModifierGroupId"" = mg.""Id"" AND mo.""RestaurantId"" IS NULL;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""IX_Orders_TrackingToken"";
                ALTER TABLE ""Orders"" DROP COLUMN IF EXISTS ""TrackingToken"";
                ALTER TABLE ""Outlets"" DROP COLUMN IF EXISTS ""ClosureReason"";
                ALTER TABLE ""Outlets"" DROP COLUMN IF EXISTS ""IsTemporarilyClosed"";
                ALTER TABLE ""Outlets"" DROP COLUMN IF EXISTS ""Phone"";
                ALTER TABLE ""Outlets"" DROP COLUMN IF EXISTS ""FreeDeliveryAboveAmount"";
                ALTER TABLE ""Outlets"" DROP COLUMN IF EXISTS ""DeliveryBaseFee"";
                ALTER TABLE ""Outlets"" DROP COLUMN IF EXISTS ""MinOrderAmount"";
                ALTER TABLE ""Outlets"" DROP COLUMN IF EXISTS ""LeadTimeMinutes"";
                ALTER TABLE ""Outlets"" DROP COLUMN IF EXISTS ""IsOvernight"";
                ALTER TABLE ""Outlets"" DROP COLUMN IF EXISTS ""ClosingTime"";
                ALTER TABLE ""Outlets"" DROP COLUMN IF EXISTS ""OpeningTime"";
                ALTER TABLE ""Outlets"" DROP COLUMN IF EXISTS ""Timezone"";
                ALTER TABLE ""Outlets"" DROP COLUMN IF EXISTS ""IsOpen"";
                ALTER TABLE ""Outlets"" DROP COLUMN IF EXISTS ""IsPublished"";
                ALTER TABLE ""ModifierOptions"" DROP COLUMN IF EXISTS ""RestaurantId"";
            ");
        }
    }
}
