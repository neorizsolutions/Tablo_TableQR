-- 1. Orders table
ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "OrderSource" character varying(30);
ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "IsScheduled" boolean NOT NULL DEFAULT false;
ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "ScheduledPickupTimeUtc" timestamp with time zone;
ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "ScheduledPickupTimeLocal" character varying(50);
ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "PreparationLeadMinutes" integer;
ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "ScheduledReleaseTimeUtc" timestamp with time zone;
ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "VehiclePlate" character varying(30);
ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "VehicleModel" character varying(100);
ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "ParkingBay" character varying(30);
ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "DeliveredAtUtc" timestamp with time zone;
ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "DeliveredByEmployeeId" uuid;

-- 2. OrderItems table
ALTER TABLE "OrderItems" ADD COLUMN IF NOT EXISTS "PackingIntent" character varying(30) NOT NULL DEFAULT 'DineIn';
ALTER TABLE "OrderItems" ADD COLUMN IF NOT EXISTS "PackagingFeeApplied" numeric(18,2);
ALTER TABLE "OrderItems" ADD COLUMN IF NOT EXISTS "IsPackagingWaived" boolean NOT NULL DEFAULT false;
ALTER TABLE "OrderItems" ADD COLUMN IF NOT EXISTS "PackagingWaiverReason" character varying(200);
ALTER TABLE "OrderItems" ADD COLUMN IF NOT EXISTS "PackagingWaivedBy" character varying(100);

-- 3. TableBills table
ALTER TABLE "TableBills" ADD COLUMN IF NOT EXISTS "LeftoverPackingCharge" numeric(18,2) NOT NULL DEFAULT 0;
ALTER TABLE "TableBills" ADD COLUMN IF NOT EXISTS "VehiclePlate" character varying(30);
ALTER TABLE "TableBills" ADD COLUMN IF NOT EXISTS "ParkingBay" character varying(30);

-- 4. RestaurantSettings table
ALTER TABLE "RestaurantSettings" ADD COLUMN IF NOT EXISTS "ScheduledPickupEnabled" boolean NOT NULL DEFAULT true;
ALTER TABLE "RestaurantSettings" ADD COLUMN IF NOT EXISTS "DefaultPreparationLeadMinutes" integer NOT NULL DEFAULT 15;
ALTER TABLE "RestaurantSettings" ADD COLUMN IF NOT EXISTS "CarSideOrderingEnabled" boolean NOT NULL DEFAULT true;
ALTER TABLE "RestaurantSettings" ADD COLUMN IF NOT EXISTS "HybridDineInParcelEnabled" boolean NOT NULL DEFAULT true;
ALTER TABLE "RestaurantSettings" ADD COLUMN IF NOT EXISTS "LeftoverContainerCharge" numeric(18,2) NOT NULL DEFAULT 10;
ALTER TABLE "RestaurantSettings" ADD COLUMN IF NOT EXISTS "UpiQrEnabled" boolean NOT NULL DEFAULT false;
ALTER TABLE "RestaurantSettings" ADD COLUMN IF NOT EXISTS "UpiVpa" character varying(100);
ALTER TABLE "RestaurantSettings" ADD COLUMN IF NOT EXISTS "UpiPayeeName" character varying(100);
ALTER TABLE "RestaurantSettings" ADD COLUMN IF NOT EXISTS "UpiMerchantCode" character varying(20);

-- Insert into __EFMigrationsHistory
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261119233000_AddScheduledHybridCarSideAndUpi', '8.0.10')
ON CONFLICT ("MigrationId") DO NOTHING;
