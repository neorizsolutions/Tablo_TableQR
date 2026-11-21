-- Add Columns to Outlets for Storefront and Operating Hours Configuration
ALTER TABLE "Outlets" ADD COLUMN IF NOT EXISTS "IsPublished" boolean NOT NULL DEFAULT TRUE;
ALTER TABLE "Outlets" ADD COLUMN IF NOT EXISTS "IsOpen" boolean NOT NULL DEFAULT TRUE;
ALTER TABLE "Outlets" ADD COLUMN IF NOT EXISTS "Timezone" character varying(50) NOT NULL DEFAULT 'Asia/Kolkata';
ALTER TABLE "Outlets" ADD COLUMN IF NOT EXISTS "OpeningTime" character varying(10) NOT NULL DEFAULT '11:00';
ALTER TABLE "Outlets" ADD COLUMN IF NOT EXISTS "ClosingTime" character varying(10) NOT NULL DEFAULT '23:30';
ALTER TABLE "Outlets" ADD COLUMN IF NOT EXISTS "IsOvernight" boolean NOT NULL DEFAULT FALSE;
ALTER TABLE "Outlets" ADD COLUMN IF NOT EXISTS "LeadTimeMinutes" integer NOT NULL DEFAULT 25;
ALTER TABLE "Outlets" ADD COLUMN IF NOT EXISTS "MinOrderAmount" numeric(18,2) NOT NULL DEFAULT 200.00;
ALTER TABLE "Outlets" ADD COLUMN IF NOT EXISTS "DeliveryBaseFee" numeric(18,2) NOT NULL DEFAULT 40.00;
ALTER TABLE "Outlets" ADD COLUMN IF NOT EXISTS "FreeDeliveryAboveAmount" numeric(18,2) NOT NULL DEFAULT 799.00;
ALTER TABLE "Outlets" ADD COLUMN IF NOT EXISTS "Phone" character varying(50) NOT NULL DEFAULT '+91 98765 43210';
ALTER TABLE "Outlets" ADD COLUMN IF NOT EXISTS "IsTemporarilyClosed" boolean NOT NULL DEFAULT FALSE;
ALTER TABLE "Outlets" ADD COLUMN IF NOT EXISTS "ClosureReason" character varying(255);

-- Add TrackingToken to Orders
ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "TrackingToken" character varying(64);
CREATE INDEX IF NOT EXISTS "IX_Orders_TrackingToken" ON "Orders" ("TrackingToken") WHERE "TrackingToken" IS NOT NULL;

-- Ensure ModifierOptions has RestaurantId for tenant isolation
ALTER TABLE "ModifierOptions" ADD COLUMN IF NOT EXISTS "RestaurantId" uuid;
UPDATE "ModifierOptions" mo
SET "RestaurantId" = mg."RestaurantId"
FROM "ModifierGroups" mg
WHERE mo."ModifierGroupId" = mg."Id" AND mo."RestaurantId" IS NULL;

-- Record in __EFMigrationsHistory
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261122040000_AddOutletStorefrontSettingsAndTrackingToken', '8.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;
