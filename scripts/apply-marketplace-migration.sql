-- Add Columns to Orders
ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "OutletId" uuid REFERENCES "Outlets"("Id") ON DELETE SET NULL;
ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "DeliveryProvider" character varying(50);
ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "ExternalOrderReference" character varying(100);
ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "NormalizedExternalReference" character varying(100);
ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "MarketplaceCollected" boolean NOT NULL DEFAULT FALSE;
ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "MarketplaceEnteredTotal" numeric(18,2);
ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "MarketplaceEnteredTax" numeric(18,2);
ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "MarketplaceEnteredDiscount" numeric(18,2);
ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "MarketplaceEnteredPackaging" numeric(18,2);
ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "MarketplaceFeeSnapshot" numeric(18,2);

-- Add Columns to TableBills
ALTER TABLE "TableBills" ADD COLUMN IF NOT EXISTS "DeliveryProvider" text;
ALTER TABLE "TableBills" ADD COLUMN IF NOT EXISTS "ExternalOrderReference" text;

-- Create Unique Index on Orders for Marketplace Reference
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Orders_RestaurantId_OutletId_DeliveryProvider_NormalizedExternalReference"
ON "Orders" ("RestaurantId", "OutletId", "DeliveryProvider", "NormalizedExternalReference")
WHERE "NormalizedExternalReference" IS NOT NULL;

-- Create MarketplacePayouts Table
CREATE TABLE IF NOT EXISTS "MarketplacePayouts" (
    "Id" uuid NOT NULL PRIMARY KEY,
    "RestaurantId" uuid NOT NULL REFERENCES "Restaurants"("Id") ON DELETE CASCADE,
    "OutletId" uuid REFERENCES "Outlets"("Id") ON DELETE SET NULL,
    "DeliveryProvider" character varying(50) NOT NULL,
    "PayoutReference" character varying(100) NOT NULL,
    "PayoutDateUtc" timestamp with time zone NOT NULL,
    "GrossAmount" numeric(18,2) NOT NULL DEFAULT 0,
    "CommissionDeducted" numeric(18,2) NOT NULL DEFAULT 0,
    "TcsGstDeducted" numeric(18,2) NOT NULL DEFAULT 0,
    "OtherAdjustments" numeric(18,2) NOT NULL DEFAULT 0,
    "NetAmount" numeric(18,2) NOT NULL DEFAULT 0,
    "LinkedOrderIdsJson" character varying(4000),
    "Notes" character varying(500),
    "CreatedAtUtc" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "RecordedByEmployeeId" uuid REFERENCES "Employees"("Id") ON DELETE SET NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_MarketplacePayouts_RestaurantId_DeliveryProvider_PayoutReference"
ON "MarketplacePayouts" ("RestaurantId", "DeliveryProvider", "PayoutReference");

-- Record in __EFMigrationsHistory
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261121150000_AddMarketplaceDeliveryAndPayouts', '8.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;
