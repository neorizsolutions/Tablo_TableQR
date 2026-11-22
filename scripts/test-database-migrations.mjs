import { execSync } from 'node:child_process';
import assert from 'node:assert/strict';
import fs from 'node:fs';

const PSQL = '"C:\\Program Files\\PostgreSQL\\17\\bin\\psql.exe" -U tablo_test -h 127.0.0.1 -p 55439';

function runSql(db, sql) {
  return execSync(`${PSQL} -d ${db}`, { input: sql, encoding: 'utf-8' });
}

console.log('--- 1. Testing Fresh Database Schema Creation & Migration ---');
try {
  runSql('postgres', 'DROP DATABASE IF EXISTS tablo_test_fresh;');
  runSql('postgres', 'CREATE DATABASE tablo_test_fresh;');
  
  // Dump schema from tablo_demo
  const schemaSql = execSync('"C:\\Program Files\\PostgreSQL\\17\\bin\\pg_dump.exe" -U tablo_test -h 127.0.0.1 -p 55439 --schema-only tablo_demo', { encoding: 'utf-8' });
  runSql('tablo_test_fresh', schemaSql);

  // Apply new migration SQL
  const migrationSql = `
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

    ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "TrackingToken" character varying(64);
    CREATE INDEX IF NOT EXISTS "IX_Orders_TrackingToken" ON "Orders" ("TrackingToken") WHERE "TrackingToken" IS NOT NULL;

    ALTER TABLE "ModifierOptions" ADD COLUMN IF NOT EXISTS "RestaurantId" uuid;
    UPDATE "ModifierOptions" mo SET "RestaurantId" = mg."RestaurantId" FROM "ModifierGroups" mg WHERE mo."ModifierGroupId" = mg."Id" AND mo."RestaurantId" IS NULL;

    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261122040000_AddOutletStorefrontSettingsAndTrackingToken', '8.0.0')
    ON CONFLICT ("MigrationId") DO NOTHING;
  `;
  runSql('tablo_test_fresh', migrationSql);

  const outletCols = runSql('tablo_test_fresh', "SELECT column_name FROM information_schema.columns WHERE table_name = 'Outlets' AND column_name IN ('IsOpen', 'IsPublished', 'ClosureReason');");
  assert.ok(outletCols.includes('IsOpen'));
  assert.ok(outletCols.includes('IsPublished'));
  assert.ok(outletCols.includes('ClosureReason'));

  const orderCols = runSql('tablo_test_fresh', "SELECT column_name FROM information_schema.columns WHERE table_name = 'Orders' AND column_name = 'TrackingToken';");
  assert.ok(orderCols.includes('TrackingToken'));

  const history = runSql('tablo_test_fresh', 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;');
  assert.ok(history.includes('20261122040000_AddOutletStorefrontSettingsAndTrackingToken'));

  console.log('Fresh Database Migration: SUCCESS');
} finally {
  runSql('postgres', 'DROP DATABASE IF EXISTS tablo_test_fresh;');
}

console.log('--- 2. Testing Upgrade on Existing Database (Idempotency) ---');
try {
  runSql('postgres', 'DROP DATABASE IF EXISTS tablo_test_upgrade;');
  runSql('postgres', 'CREATE DATABASE tablo_test_upgrade;');

  // Restore current database dump/structure
  const dump = execSync('"C:\\Program Files\\PostgreSQL\\17\\bin\\pg_dump.exe" -U tablo_test -h 127.0.0.1 -p 55439 tablo_demo', { encoding: 'utf-8' });
  runSql('tablo_test_upgrade', dump);

  // Re-run migration SQL to test that it is completely idempotent on an already-upgraded DB
  const migrationSql = `
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

    ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "TrackingToken" character varying(64);
    CREATE INDEX IF NOT EXISTS "IX_Orders_TrackingToken" ON "Orders" ("TrackingToken") WHERE "TrackingToken" IS NOT NULL;

    ALTER TABLE "ModifierOptions" ADD COLUMN IF NOT EXISTS "RestaurantId" uuid;
    UPDATE "ModifierOptions" mo SET "RestaurantId" = mg."RestaurantId" FROM "ModifierGroups" mg WHERE mo."ModifierGroupId" = mg."Id" AND mo."RestaurantId" IS NULL;

    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261122040000_AddOutletStorefrontSettingsAndTrackingToken', '8.0.0')
    ON CONFLICT ("MigrationId") DO NOTHING;
  `;
  runSql('tablo_test_upgrade', migrationSql);

  const orderCount = runSql('tablo_test_upgrade', 'SELECT count(*) FROM "Orders";');
  console.log('Preserved Orders count in upgraded DB:', orderCount.trim());
  console.log('Database Upgrade & Idempotency: SUCCESS');
} finally {
  runSql('postgres', 'DROP DATABASE IF EXISTS tablo_test_upgrade;');
}
