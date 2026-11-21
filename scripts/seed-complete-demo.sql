-- TABLO Comprehensive Test & Demo Database Seeder
-- Covers all modules: SuperAdmin, Auth, Floor Tables, QR Menu, Modifiers, Refills, 
-- Kitchen Production, Printing Stations, Parcel Counter, Car-Side, Hybrid Dine-in, 
-- Billing/Payments, Dynamic UPI QR, Subscriptions, and Inventory.

BEGIN;

-- 1. Roles
INSERT INTO "Roles" ("Id", "Name", "Description") VALUES
('11111111-0000-0000-0000-000000000001', 'Admin', 'Restaurant Administrator'),
('11111111-0000-0000-0000-000000000002', 'Waiter', 'Floor Waiter Staff'),
('11111111-0000-0000-0000-000000000003', 'Kitchen', 'Kitchen Chef & Production Staff'),
('11111111-0000-0000-0000-000000000004', 'Cashier', 'Front Desk Cashier')
ON CONFLICT ("Name") DO NOTHING;

-- 2. Restaurant & Outlets
INSERT INTO "Restaurants" ("Id", "Name", "Description", "Address", "ContactEmail", "ContactPhone", "CreatedAt", "UpdatedAt") VALUES
('22222222-0000-0000-0000-000000000001', 'Tablo Heritage Restaurant', 'Fine dining, live counter, takeaway & car-side service.', '42 Heritage Lane, MG Road, Bengaluru', 'contact@tablodemo.com', '+91 98765 43210', NOW(), NOW())
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "Outlets" ("Id", "RestaurantId", "Name", "Code", "Address", "IsCentralKitchen", "IsActive", "CreatedAtUtc") VALUES
('22222222-0000-0000-0000-000000000002', '22222222-0000-0000-0000-000000000001', 'Main Dining & Counter Outlet', 'OUT-01', 'Ground Floor, 42 Heritage Lane', false, true, NOW())
ON CONFLICT ("Id") DO NOTHING;

-- 3. Employees (Password: password123, Manager PIN: 1234)
-- BCrypt hash for 'password123': $2a$10$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy
-- SHA256 hex for PIN '1234': 03AC674216F3E15C761EE1A5E255F067953623C8B388B4459E13F978D7C846F4
INSERT INTO "Employees" ("Id", "RestaurantId", "RoleId", "FirstName", "LastName", "Email", "PasswordHash", "ManagerPinHash", "FailedPinAttempts", "IsActive") VALUES
('33333333-0000-0000-0000-000000000001', '22222222-0000-0000-0000-000000000001', (SELECT "Id" FROM "Roles" WHERE "Name" = 'Admin' LIMIT 1), 'Aditi', 'Sharma (Admin)', 'admin@tablodemo.com', '$2a$10$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy', '03AC674216F3E15C761EE1A5E255F067953623C8B388B4459E13F978D7C846F4', 0, true),
('33333333-0000-0000-0000-000000000002', '22222222-0000-0000-0000-000000000001', (SELECT "Id" FROM "Roles" WHERE "Name" = 'Waiter' LIMIT 1), 'Rohan', 'Verma (Waiter)', 'waiter@tablodemo.com', '$2a$10$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy', NULL, 0, true),
('33333333-0000-0000-0000-000000000003', '22222222-0000-0000-0000-000000000001', (SELECT "Id" FROM "Roles" WHERE "Name" = 'Kitchen' LIMIT 1), 'Suresh', 'Chef (Kitchen)', 'kitchen@tablodemo.com', '$2a$10$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy', NULL, 0, true),
('33333333-0000-0000-0000-000000000004', '22222222-0000-0000-0000-000000000001', (SELECT "Id" FROM "Roles" WHERE "Name" = 'Cashier' LIMIT 1), 'Pooja', 'Nair (Cashier)', 'cashier@tablodemo.com', '$2a$10$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy', NULL, 0, true)
ON CONFLICT ("Email") DO UPDATE SET "PasswordHash" = EXCLUDED."PasswordHash", "ManagerPinHash" = EXCLUDED."ManagerPinHash", "IsActive" = true;

-- 4. Plans & Premium Subscription
INSERT INTO "Plans" ("Id", "Code", "Name", "Description", "MonthlyPriceInr", "MaxOutlets", "MaxTables", "MaxKitchenStations", "FeaturesJson", "DisplayOrder", "IsActive", "CreatedAtUtc", "UpdatedAtUtc") VALUES
('44444444-0000-0000-0000-000000000001', 'basic', 'Basic Plan', 'QR menu and ordering, kitchen display, standard billing.', 999.00, 1, 15, 1, '["qr_ordering","kitchen_display","standard_billing","basic_reports","menu_management","tables_qr","qr-menu","customer-ordering","kitchen-display","order-status-tracking","bill-requests","service-requests","sales-reports"]', 1, true, NOW(), NOW()),
('44444444-0000-0000-0000-000000000002', 'plus', 'Plus Plan', 'Basic plus staff ordering, dining groups, split bills, mixed payments, table transfers.', 2499.00, 3, 40, 2, '["qr_ordering","kitchen_display","standard_billing","basic_reports","menu_management","tables_qr","staff_ordering","dining_groups","split_billing","mixed_payments","table_transfers","advanced_payment_reports","thermal_printing","parcel_counter","hold_fire_courses","item_modifiers","free_refills","waste_cancellation","scheduled_pickup","hybrid_dinein_parcel","carside_ordering","dynamic_upi_qr","qr-menu","customer-ordering","kitchen-display","order-status-tracking","bill-requests","service-requests","sales-reports","staff-ordering","dining-groups","split-bills","mixed-payments","table-transfers","payment-reports","thermal-printing","parcel-counter","hold-fire","priced-modifiers","free-refills","cancellation-waste","scheduled-pickup","hybrid-dine-in-parcel","car-side-ordering","dynamic-upi-qr"]', 2, true, NOW(), NOW()),
('44444444-0000-0000-0000-000000000003', 'premium', 'Premium Plan', 'Complete enterprise operations: inventory, production controls, thermal printing, parcel counter, car-side, dynamic UPI QR.', 4999.00, 10, 100, 5, '["qr_ordering","kitchen_display","standard_billing","basic_reports","menu_management","tables_qr","staff_ordering","dining_groups","split_billing","mixed_payments","table_transfers","advanced_payment_reports","thermal_printing","multiple_stations","parcel_counter","hold_fire_courses","item_modifiers","free_refills","waste_cancellation","scheduled_pickup","hybrid_dinein_parcel","carside_ordering","dynamic_upi_qr","inventory_core","recipes_costing","stocktakes","purchasing","lots_expiry","preparations","outlet_transfers","stock_profit_reports","auto_availability","qr-menu","customer-ordering","kitchen-display","order-status-tracking","bill-requests","service-requests","sales-reports","staff-ordering","dining-groups","split-bills","mixed-payments","table-transfers","payment-reports","inventory-management","recipe-costing","stocktakes","supplier-purchasing","batch-tracking","inventory-reports","outlet-stock-transfers","thermal-printing","parcel-counter","hold-fire","priced-modifiers","free-refills","cancellation-waste","scheduled-pickup","hybrid-dine-in-parcel","car-side-ordering","dynamic-upi-qr"]', 3, true, NOW(), NOW())
ON CONFLICT ("Code") DO UPDATE SET "FeaturesJson" = EXCLUDED."FeaturesJson";

INSERT INTO "Subscriptions" ("Id", "RestaurantId", "PlanId", "Status", "StartDateUtc", "EndDateUtc", "AutoRenew", "Notes", "CreatedAtUtc", "UpdatedAtUtc") VALUES
('44444444-0000-0000-0000-000000000004', '22222222-0000-0000-0000-000000000001', (SELECT "Id" FROM "Plans" WHERE "Code" = 'premium' LIMIT 1), 1, NOW() - INTERVAL '1 month', NOW() + INTERVAL '1 year', true, 'Full demo subscription with all 28 capabilities active.', NOW(), NOW())
ON CONFLICT ("Id") DO NOTHING;

-- 5. Restaurant Settings
INSERT INTO "RestaurantSettings" (
  "RestaurantId", "IsAcceptingOrders", "AutoManageAvailability", 
  "ParcelPackagingChargeMode", "ParcelPackagingFlatRate", "ParcelPackagingPercentage", "ParcelPackagingTaxable", "ParcelPackagingTaxRate",
  "ScheduledPickupEnabled", "DefaultPreparationLeadMinutes", "CarSideOrderingEnabled", "HybridDineInParcelEnabled", "LeftoverContainerCharge",
  "UpiQrEnabled", "UpiVpa", "UpiPayeeName", "UpiMerchantCode"
) VALUES (
  '22222222-0000-0000-0000-000000000001', true, true,
  'PerItem', 20.00, 5.00, false, 0.00,
  true, 15, true, true, 10.00,
  true, 'tablodemo@okhdfcbank', 'Tablo Heritage Restaurant', '5812'
) ON CONFLICT ("RestaurantId") DO UPDATE SET
  "IsAcceptingOrders" = true,
  "ScheduledPickupEnabled" = true,
  "CarSideOrderingEnabled" = true,
  "HybridDineInParcelEnabled" = true,
  "UpiQrEnabled" = true,
  "UpiVpa" = 'tablodemo@okhdfcbank',
  "UpiPayeeName" = 'Tablo Heritage Restaurant';

-- 6. Kitchen Stations & Thermal Printers (Connected to Virtual Printers on 9100 and 9101)
INSERT INTO "ThermalPrinters" ("Id", "RestaurantId", "OutletId", "Name", "ConnectionType", "IpAddress", "Port", "PaperWidthMm", "CharactersPerLine", "Status", "IsActive", "CreatedAtUtc", "UpdatedAtUtc") VALUES
('66666666-1000-0000-0000-000000000001', '22222222-0000-0000-0000-000000000001', '22222222-0000-0000-0000-000000000002', 'Food Kitchen 80mm Virtual Printer', 'NetworkTcp', '127.0.0.1', 9100, 80, 48, 'Online', true, NOW(), NOW()),
('66666666-1000-0000-0000-000000000002', '22222222-0000-0000-0000-000000000001', '22222222-0000-0000-0000-000000000002', 'Beverage Station 58mm Virtual Printer', 'NetworkTcp', '127.0.0.1', 9101, 58, 32, 'Online', true, NOW(), NOW()),
('66666666-1000-0000-0000-000000000003', '22222222-0000-0000-0000-000000000001', '22222222-0000-0000-0000-000000000002', 'Front Counter Bill Printer', 'NetworkTcp', '127.0.0.1', 9100, 80, 48, 'Online', true, NOW(), NOW())
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "KitchenStations" ("Id", "RestaurantId", "OutletId", "Name", "Code", "Description", "PrinterId", "IsDefault", "IsActive", "DisplayOrder", "CreatedAtUtc", "UpdatedAtUtc") VALUES
('66666666-0000-0000-0000-000000000001', '22222222-0000-0000-0000-000000000001', '22222222-0000-0000-0000-000000000002', 'Food Kitchen Station', 'FOOD-01', 'Handles starters, main courses and tandoor', '66666666-1000-0000-0000-000000000001', true, true, 1, NOW(), NOW()),
('66666666-0000-0000-0000-000000000002', '22222222-0000-0000-0000-000000000001', '22222222-0000-0000-0000-000000000002', 'Beverage & Bar Station', 'BEV-01', 'Handles mocktails, sodas and filter coffee', '66666666-1000-0000-0000-000000000002', false, true, 2, NOW(), NOW()),
('66666666-0000-0000-0000-000000000003', '22222222-0000-0000-0000-000000000001', '22222222-0000-0000-0000-000000000002', 'Front Counter Bill Station', 'FRONT-01', 'Counter receipts & parcel handover', '66666666-1000-0000-0000-000000000003', false, true, 3, NOW(), NOW())
ON CONFLICT ("Id") DO NOTHING;

-- 7. Dining Tables (QR Tokens for instant testing)
INSERT INTO "RestaurantTables" ("Id", "RestaurantId", "TableNumber", "QRToken", "IsActive") VALUES
('77777777-0000-0000-0000-000000000001', '22222222-0000-0000-0000-000000000001', 1, 'table_1_demo_token', true),
('77777777-0000-0000-0000-000000000002', '22222222-0000-0000-0000-000000000001', 2, 'table_2_demo_token', true),
('77777777-0000-0000-0000-000000000003', '22222222-0000-0000-0000-000000000001', 3, 'table_3_demo_token', true),
('77777777-0000-0000-0000-000000000004', '22222222-0000-0000-0000-000000000001', 4, 'table_4_demo_token', true),
('77777777-0000-0000-0000-000000000005', '22222222-0000-0000-0000-000000000001', 5, 'table_5_demo_token', true),
('77777777-0000-0000-0000-000000000006', '22222222-0000-0000-0000-000000000001', 6, 'table_6_demo_token', true),
('77777777-0000-0000-0000-000000000007', '22222222-0000-0000-0000-000000000001', 7, 'table_7_demo_token', true),
('77777777-0000-0000-0000-000000000008', '22222222-0000-0000-0000-000000000001', 8, 'table_8_demo_token', true),
('77777777-0000-0000-0000-000000000009', '22222222-0000-0000-0000-000000000001', 9, 'table_9_demo_token', true),
('77777777-0000-0000-0000-000000000010', '22222222-0000-0000-0000-000000000001', 10, 'table_10_demo_token', true)
ON CONFLICT ("Id") DO NOTHING;

-- 8. Categories
INSERT INTO "Categories" ("Id", "RestaurantId", "Name", "DisplayOrder", "KitchenStationId") VALUES
('88888888-0000-0000-0000-000000000001', '22222222-0000-0000-0000-000000000001', 'Starters & Appetizers', 1, '66666666-0000-0000-0000-000000000001'),
('88888888-0000-0000-0000-000000000002', '22222222-0000-0000-0000-000000000001', 'Main Course Curries', 2, '66666666-0000-0000-0000-000000000001'),
('88888888-0000-0000-0000-000000000003', '22222222-0000-0000-0000-000000000001', 'Tandoor & Breads', 3, '66666666-0000-0000-0000-000000000001'),
('88888888-0000-0000-0000-000000000004', '22222222-0000-0000-0000-000000000001', 'Artisan Beverages', 4, '66666666-0000-0000-0000-000000000002'),
('88888888-0000-0000-0000-000000000005', '22222222-0000-0000-0000-000000000001', 'Desserts', 5, '66666666-0000-0000-0000-000000000001')
ON CONFLICT ("Id") DO NOTHING;

-- 9. Menu Items (with Packaging Fees, Stations, Refill rules)
INSERT INTO "MenuItems" (
  "Id", "RestaurantId", "CategoryId", "Name", "Description", "Price", 
  "IsAvailable", "PackagingFee", "KitchenStationId", "IsRefillEligible", "RefillMaxCount", "RefillKitchenStationId"
) VALUES
('99999999-0000-0000-0000-000000000001', '22222222-0000-0000-0000-000000000001', '88888888-0000-0000-0000-000000000001', 'Paneer Tikka Angara', 'Charcoal grilled cottage cheese cubes with bell peppers', 280.00, true, 15.00, '66666666-0000-0000-0000-000000000001', false, 0, NULL),
('99999999-0000-0000-0000-000000000002', '22222222-0000-0000-0000-000000000001', '88888888-0000-0000-0000-000000000001', 'Crispy Corn & Waterchestnut', 'Tossed in mild spices and lime', 220.00, true, 10.00, '66666666-0000-0000-0000-000000000001', false, 0, NULL),
('99999999-0000-0000-0000-000000000003', '22222222-0000-0000-0000-000000000001', '88888888-0000-0000-0000-000000000002', 'Butter Chicken Handi', 'Tender chicken slow-simmered in rich tomato gravy', 360.00, true, 20.00, '66666666-0000-0000-0000-000000000001', false, 0, NULL),
('99999999-0000-0000-0000-000000000004', '22222222-0000-0000-0000-000000000001', '88888888-0000-0000-0000-000000000002', 'Dal Makhani Bukhara', 'Black lentils slow cooked overnight with churned butter', 260.00, true, 15.00, '66666666-0000-0000-0000-000000000001', false, 0, NULL),
('99999999-0000-0000-0000-000000000005', '22222222-0000-0000-0000-000000000001', '88888888-0000-0000-0000-000000000002', 'Dum Gosht Dum Biryani', 'Fragrant basmati rice layered with marinated meat', 390.00, true, 25.00, '66666666-0000-0000-0000-000000000001', false, 0, NULL),
('99999999-0000-0000-0000-000000000006', '22222222-0000-0000-0000-000000000001', '88888888-0000-0000-0000-000000000003', 'Butter Garlic Naan', 'Clay-oven leavened flatbread brushed with garlic butter', 55.00, true, 5.00, '66666666-0000-0000-0000-000000000001', false, 0, NULL),
('99999999-0000-0000-0000-000000000007', '22222222-0000-0000-0000-000000000001', '88888888-0000-0000-0000-000000000004', 'Mango Lassi Royale', 'Thick churned yoghurt with Alphonso mango pulp', 95.00, true, 10.00, '66666666-0000-0000-0000-000000000002', false, 0, NULL),
('99999999-0000-0000-0000-000000000008', '22222222-0000-0000-0000-000000000001', '88888888-0000-0000-0000-000000000004', 'South Indian Filter Coffee', 'Traditional brass-brewed chicory coffee (Free Refill x2)', 60.00, true, 5.00, '66666666-0000-0000-0000-000000000002', true, 2, '66666666-0000-0000-0000-000000000002'),
('99999999-0000-0000-0000-000000000009', '22222222-0000-0000-0000-000000000001', '88888888-0000-0000-0000-000000000005', 'Gulab Jamun with Rabdi', 'Warm mawa dumplings served over saffron rabdi', 140.00, true, 10.00, '66666666-0000-0000-0000-000000000001', false, 0, NULL)
ON CONFLICT ("Id") DO NOTHING;

-- 10. Modifier Groups & Options
INSERT INTO "ModifierGroups" ("Id", "RestaurantId", "Name", "MinSelections", "MaxSelections", "DisplayOrder", "IsActive", "CreatedAtUtc", "UpdatedAtUtc") VALUES
('AAAAAAAA-0000-0000-0000-000000000001', '22222222-0000-0000-0000-000000000001', 'Spice Level', 1, 1, 1, true, NOW(), NOW()),
('AAAAAAAA-0000-0000-0000-000000000002', '22222222-0000-0000-0000-000000000001', 'Gourmet Add-ons', 0, 3, 2, true, NOW(), NOW())
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "ModifierOptions" ("Id", "ModifierGroupId", "Name", "PriceDelta", "DisplayOrder", "IsAvailable", "CreatedAtUtc", "UpdatedAtUtc") VALUES
('BBBBBBBB-0000-0000-0000-000000000001', 'AAAAAAAA-0000-0000-0000-000000000001', 'Mild', 0.00, 1, true, NOW(), NOW()),
('BBBBBBBB-0000-0000-0000-000000000002', 'AAAAAAAA-0000-0000-0000-000000000001', 'Medium Spicy', 0.00, 2, true, NOW(), NOW()),
('BBBBBBBB-0000-0000-0000-000000000003', 'AAAAAAAA-0000-0000-0000-000000000001', 'Extra Fiery Hot', 10.00, 3, true, NOW(), NOW()),
('BBBBBBBB-0000-0000-0000-000000000004', 'AAAAAAAA-0000-0000-0000-000000000002', 'Extra Amul Butter Cube', 20.00, 1, true, NOW(), NOW()),
('BBBBBBBB-0000-0000-0000-000000000005', 'AAAAAAAA-0000-0000-0000-000000000002', 'Grated Processed Cheese', 35.00, 2, true, NOW(), NOW())
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "MenuItemModifierGroups" ("MenuItemId", "ModifierGroupId", "DisplayOrder") VALUES
('99999999-0000-0000-0000-000000000003', 'AAAAAAAA-0000-0000-0000-000000000001', 1),
('99999999-0000-0000-0000-000000000003', 'AAAAAAAA-0000-0000-0000-000000000002', 2),
('99999999-0000-0000-0000-000000000005', 'AAAAAAAA-0000-0000-0000-000000000001', 1)
ON CONFLICT ("MenuItemId", "ModifierGroupId") DO NOTHING;

-- 11. Suppliers, Ingredients & Inventory Stock
INSERT INTO "Suppliers" ("Id", "RestaurantId", "Name", "ContactPerson", "Email", "Phone", "IsActive", "CreatedAtUtc", "UpdatedAtUtc") VALUES
('CCCCCCCC-0000-0000-0000-000000000001', '22222222-0000-0000-0000-000000000001', 'Royal Dairy Products', 'Manish Gupta', 'orders@royaldairy.in', '+91 99887 76655', true, NOW(), NOW()),
('CCCCCCCC-0000-0000-0000-000000000002', '22222222-0000-0000-0000-000000000001', 'Prime Poultry & Meats', 'Arjun Singh', 'sales@primepoultry.in', '+91 91234 56789', true, NOW(), NOW()),
('CCCCCCCC-0000-0000-0000-000000000003', '22222222-0000-0000-0000-000000000001', 'Malabar Spices & Grains', 'Kiran Kumar', 'kiran@malabarspices.com', '+91 97654 32109', true, NOW(), NOW())
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "Ingredients" ("Id", "RestaurantId", "Name", "BaseUnit", "CurrentAverageCost", "LowStockThreshold", "IsActive", "CreatedAtUtc", "UpdatedAtUtc") VALUES
('DDDDDDDD-0000-0000-0000-000000000001', '22222222-0000-0000-0000-000000000001', 'Fresh Cottage Cheese (Paneer)', 'kg', 320.00, 10.00, true, NOW(), NOW()),
('DDDDDDDD-0000-0000-0000-000000000002', '22222222-0000-0000-0000-000000000001', 'Boneless Fresh Chicken', 'kg', 240.00, 20.00, true, NOW(), NOW()),
('DDDDDDDD-0000-0000-0000-000000000003', '22222222-0000-0000-0000-000000000001', 'Aged Basmati Rice', 'kg', 110.00, 50.00, true, NOW(), NOW()),
('DDDDDDDD-0000-0000-0000-000000000004', '22222222-0000-0000-0000-000000000001', 'Pure Dairy Butter', 'kg', 450.00, 15.00, true, NOW(), NOW()),
('DDDDDDDD-0000-0000-0000-000000000005', '22222222-0000-0000-0000-000000000001', 'Arabica Coffee Beans', 'kg', 600.00, 5.00, true, NOW(), NOW())
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "RecipeItems" ("Id", "RestaurantId", "MenuItemId", "IngredientId", "UsableQuantity", "YieldPercent", "CreatedAtUtc", "UpdatedAtUtc") VALUES
('EEEEEEEE-0000-0000-0000-000000000001', '22222222-0000-0000-0000-000000000001', '99999999-0000-0000-0000-000000000003', 'DDDDDDDD-0000-0000-0000-000000000002', 0.250, 100.0, NOW(), NOW()),
('EEEEEEEE-0000-0000-0000-000000000002', '22222222-0000-0000-0000-000000000001', '99999999-0000-0000-0000-000000000003', 'DDDDDDDD-0000-0000-0000-000000000004', 0.040, 100.0, NOW(), NOW()),
('EEEEEEEE-0000-0000-0000-000000000003', '22222222-0000-0000-0000-000000000001', '99999999-0000-0000-0000-000000000005', 'DDDDDDDD-0000-0000-0000-000000000003', 0.200, 100.0, NOW(), NOW())
ON CONFLICT ("Id") DO NOTHING;

-- Initial Opening Stock Movements
INSERT INTO "StockMovements" ("Id", "RestaurantId", "IngredientId", "QuantityChange", "UnitCostSnapshot", "TotalCost", "Reason", "ReferenceType", "UnitSnapshot", "TimestampUtc") VALUES
(gen_random_uuid(), '22222222-0000-0000-0000-000000000001', 'DDDDDDDD-0000-0000-0000-000000000001', 45.0, 320.00, 14400.00, 'Opening setup balance', 'OpeningBalance', 'kg', NOW() - INTERVAL '5 days'),
(gen_random_uuid(), '22222222-0000-0000-0000-000000000001', 'DDDDDDDD-0000-0000-0000-000000000002', 80.0, 240.00, 19200.00, 'Opening setup balance', 'OpeningBalance', 'kg', NOW() - INTERVAL '5 days'),
(gen_random_uuid(), '22222222-0000-0000-0000-000000000001', 'DDDDDDDD-0000-0000-0000-000000000003', 150.0, 110.00, 16500.00, 'Opening setup balance', 'OpeningBalance', 'kg', NOW() - INTERVAL '5 days'),
(gen_random_uuid(), '22222222-0000-0000-0000-000000000001', 'DDDDDDDD-0000-0000-0000-000000000004', 30.0, 450.00, 13500.00, 'Opening setup balance', 'OpeningBalance', 'kg', NOW() - INTERVAL '5 days'),
(gen_random_uuid(), '22222222-0000-0000-0000-000000000001', 'DDDDDDDD-0000-0000-0000-000000000005', 12.0, 600.00, 7200.00, 'Opening setup balance', 'OpeningBalance', 'kg', NOW() - INTERVAL '5 days');

-- 12. Live Sample Orders Across Modules
-- A. Active Table 1 Dine-In Order with Mixed PackingIntent (DineIn vs Takeaway)
INSERT INTO "TableBills" ("Id", "RestaurantId", "TableId", "TableNumberSnapshot", "OrderChannel", "DailyTokenNumber", "BusinessDate", "Subtotal", "PackagingCharge", "PackagingRateSnapshot", "IsPackagingWaived", "LeftoverPackingCharge", "Tax", "Discount", "TotalAmount", "CreatedAtUtc") VALUES
('FA000000-0000-0000-0000-000000000001', '22222222-0000-0000-0000-000000000001', '77777777-0000-0000-0000-000000000001', 1, 'DineIn', 1, TO_CHAR(NOW() AT TIME ZONE 'Asia/Kolkata', 'YYYY-MM-DD'), 470.00, 20.00, 20.00, false, 10.00, 25.00, 0.00, 525.00, NOW() - INTERVAL '30 minutes')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "Orders" ("Id", "RestaurantId", "TableId", "TableBillId", "OrderNumber", "OrderChannel", "DailyTokenNumber", "BusinessDate", "Status", "KotPrintStatus", "IsScheduled", "Subtotal", "PackagingCharge", "Tax", "Discount", "TotalAmount", "CreatedAt", "UpdatedAt") VALUES
('FB000000-0000-0000-0000-000000000001', '22222222-0000-0000-0000-000000000001', '77777777-0000-0000-0000-000000000001', 'FA000000-0000-0000-0000-000000000001', 101, 'DineIn', 1, TO_CHAR(NOW() AT TIME ZONE 'Asia/Kolkata', 'YYYY-MM-DD'), 'Served', 'Printed', false, 470.00, 20.00, 25.00, 0.00, 515.00, NOW() - INTERVAL '30 minutes', NOW() - INTERVAL '10 minutes')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "OrderItems" ("Id", "OrderId", "MenuItemId", "ItemNameSnapshot", "Quantity", "UnitPrice", "ReleaseState", "CourseName", "PackingIntent", "IsRefill", "RefillCount", "CancelledQuantity", "IsCancelled", "IsPackagingWaived") VALUES
('FC000000-0000-0000-0000-000000000001', 'FB000000-0000-0000-0000-000000000001', '99999999-0000-0000-0000-000000000003', 'Butter Chicken Handi', 1, 360.00, 'Fired', 'Mains', 'Takeaway', false, 0, 0, false, false),
('FC000000-0000-0000-0000-000000000002', 'FB000000-0000-0000-0000-000000000001', '99999999-0000-0000-0000-000000000006', 'Butter Garlic Naan', 2, 55.00, 'Fired', 'Breads', 'DineIn', false, 0, 0, false, false)
ON CONFLICT ("Id") DO NOTHING;

-- Partial Payment of ₹300 on Table 1 (Remaining balance ₹225)
INSERT INTO "BillPayments" ("Id", "RestaurantId", "TableBillId", "ActorEmployeeId", "OperationId", "TableIdSnapshot", "TableNumberSnapshot", "Method", "Amount", "CollectedAtUtc", "PayloadHash", "AcknowledgementJson") VALUES
(gen_random_uuid(), '22222222-0000-0000-0000-000000000001', 'FA000000-0000-0000-0000-000000000001', '33333333-0000-0000-0000-000000000002', gen_random_uuid(), '77777777-0000-0000-0000-000000000001', 1, 'Cash', 300.00, NOW() - INTERVAL '15 minutes', 'HASH300CASH', '{"method":"Cash","amount":300.00}');

-- B. Table 2 Active Order with Hold/Fire Courses
INSERT INTO "TableBills" ("Id", "RestaurantId", "TableId", "TableNumberSnapshot", "OrderChannel", "DailyTokenNumber", "BusinessDate", "Subtotal", "PackagingCharge", "PackagingRateSnapshot", "IsPackagingWaived", "LeftoverPackingCharge", "Tax", "Discount", "TotalAmount", "CreatedAtUtc") VALUES
('FA000000-0000-0000-0000-000000000002', '22222222-0000-0000-0000-000000000001', '77777777-0000-0000-0000-000000000002', 2, 'DineIn', 2, TO_CHAR(NOW() AT TIME ZONE 'Asia/Kolkata', 'YYYY-MM-DD'), 540.00, 0.00, 0.00, false, 0.00, 27.00, 0.00, 567.00, NOW() - INTERVAL '20 minutes')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "Orders" ("Id", "RestaurantId", "TableId", "TableBillId", "OrderNumber", "OrderChannel", "DailyTokenNumber", "BusinessDate", "Status", "KotPrintStatus", "IsScheduled", "Subtotal", "PackagingCharge", "Tax", "Discount", "TotalAmount", "CreatedAt", "UpdatedAt") VALUES
('FB000000-0000-0000-0000-000000000002', '22222222-0000-0000-0000-000000000001', '77777777-0000-0000-0000-000000000002', 'FA000000-0000-0000-0000-000000000002', 102, 'DineIn', 2, TO_CHAR(NOW() AT TIME ZONE 'Asia/Kolkata', 'YYYY-MM-DD'), 'Accepted', 'Printed', false, 540.00, 0.00, 27.00, 0.00, 567.00, NOW() - INTERVAL '20 minutes', NOW() - INTERVAL '20 minutes')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "OrderItems" ("Id", "OrderId", "MenuItemId", "ItemNameSnapshot", "Quantity", "UnitPrice", "ReleaseState", "CourseName", "PackingIntent", "IsRefill", "RefillCount", "CancelledQuantity", "IsCancelled", "IsPackagingWaived") VALUES
('FC000000-0000-0000-0000-000000000003', 'FB000000-0000-0000-0000-000000000002', '99999999-0000-0000-0000-000000000001', 'Paneer Tikka Angara', 1, 280.00, 'Fired', 'Starters', 'DineIn', false, 0, 0, false, false),
('FC000000-0000-0000-0000-000000000004', 'FB000000-0000-0000-0000-000000000002', '99999999-0000-0000-0000-000000000004', 'Dal Makhani Bukhara', 1, 260.00, 'Held', 'Mains', 'DineIn', false, 0, 0, false, false)
ON CONFLICT ("Id") DO NOTHING;

-- C. Parcel Counter Token #001: Walk-In Takeaway (Fully Settled Receipt)
INSERT INTO "TableBills" ("Id", "RestaurantId", "TableId", "TableNumberSnapshot", "OrderChannel", "DailyTokenNumber", "BusinessDate", "Subtotal", "PackagingCharge", "PackagingRateSnapshot", "IsPackagingWaived", "LeftoverPackingCharge", "Tax", "Discount", "TotalAmount", "SettledAtUtc", "PaymentMethod", "CreatedAtUtc") VALUES
('FA000000-0000-0000-0000-000000000003', '22222222-0000-0000-0000-000000000001', NULL, 0, 'ParcelTakeaway', 1, TO_CHAR(NOW() AT TIME ZONE 'Asia/Kolkata', 'YYYY-MM-DD'), 360.00, 20.00, 20.00, false, 0.00, 19.00, 0.00, 399.00, NOW() - INTERVAL '40 minutes', 'UPI', NOW() - INTERVAL '50 minutes')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "Orders" ("Id", "RestaurantId", "TableId", "TableBillId", "OrderNumber", "OrderChannel", "DailyTokenNumber", "BusinessDate", "CustomerName", "CustomerPhone", "Status", "KotPrintStatus", "IsScheduled", "Subtotal", "PackagingCharge", "Tax", "Discount", "TotalAmount", "CreatedAt", "UpdatedAt") VALUES
('FB000000-0000-0000-0000-000000000003', '22222222-0000-0000-0000-000000000001', NULL, 'FA000000-0000-0000-0000-000000000003', 103, 'ParcelTakeaway', 1, TO_CHAR(NOW() AT TIME ZONE 'Asia/Kolkata', 'YYYY-MM-DD'), 'Kavita Krishnan', '9876501234', 'Served', 'Printed', false, 360.00, 20.00, 19.00, 0.00, 399.00, NOW() - INTERVAL '50 minutes', NOW() - INTERVAL '40 minutes')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "OrderItems" ("Id", "OrderId", "MenuItemId", "ItemNameSnapshot", "Quantity", "UnitPrice", "ReleaseState", "PackingIntent", "IsRefill", "RefillCount", "CancelledQuantity", "IsCancelled", "IsPackagingWaived") VALUES
('FC000000-0000-0000-0000-000000000005', 'FB000000-0000-0000-0000-000000000003', '99999999-0000-0000-0000-000000000003', 'Butter Chicken Handi', 1, 360.00, 'Fired', 'Takeaway', false, 0, 0, false, false)
ON CONFLICT ("Id") DO NOTHING;

-- D. Advance Scheduled Parcel Pickup (Token #002 via WhatsApp)
INSERT INTO "TableBills" ("Id", "RestaurantId", "TableId", "TableNumberSnapshot", "OrderChannel", "DailyTokenNumber", "BusinessDate", "Subtotal", "PackagingCharge", "PackagingRateSnapshot", "IsPackagingWaived", "LeftoverPackingCharge", "Tax", "Discount", "TotalAmount", "CreatedAtUtc") VALUES
('FA000000-0000-0000-0000-000000000004', '22222222-0000-0000-0000-000000000001', NULL, 0, 'ParcelTakeaway', 2, TO_CHAR(NOW() AT TIME ZONE 'Asia/Kolkata', 'YYYY-MM-DD'), 485.00, 35.00, 20.00, false, 0.00, 26.00, 0.00, 546.00, NOW() - INTERVAL '10 minutes')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "Orders" (
  "Id", "RestaurantId", "TableId", "TableBillId", "OrderNumber", "OrderChannel", "DailyTokenNumber", "BusinessDate",
  "CustomerName", "CustomerPhone", "OrderSource", "IsScheduled", "ScheduledPickupTimeUtc", "ScheduledPickupTimeLocal",
  "PreparationLeadMinutes", "ScheduledReleaseTimeUtc", "Status", "KotPrintStatus", "Subtotal", "PackagingCharge", "Tax", "Discount", "TotalAmount", "CreatedAt", "UpdatedAt"
) VALUES (
  'FB000000-0000-0000-0000-000000000004', '22222222-0000-0000-0000-000000000001', NULL, 'FA000000-0000-0000-0000-000000000004', 104, 'ParcelTakeaway', 2, TO_CHAR(NOW() AT TIME ZONE 'Asia/Kolkata', 'YYYY-MM-DD'),
  'Vikram Malhotra', '9845011223', 'WhatsApp', true, NOW() + INTERVAL '2 hours', TO_CHAR((NOW() AT TIME ZONE 'Asia/Kolkata') + INTERVAL '2 hours', 'YYYY-MM-DD HH24:MI'),
  15, NOW() + INTERVAL '105 minutes', 'Scheduled', 'Pending', 485.00, 35.00, 26.00, 0.00, 546.00, NOW() - INTERVAL '10 minutes', NOW() - INTERVAL '10 minutes'
) ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "OrderItems" ("Id", "OrderId", "MenuItemId", "ItemNameSnapshot", "Quantity", "UnitPrice", "ReleaseState", "PackingIntent", "IsRefill", "RefillCount", "CancelledQuantity", "IsCancelled", "IsPackagingWaived") VALUES
('FC000000-0000-0000-0000-000000000006', 'FB000000-0000-0000-0000-000000000004', '99999999-0000-0000-0000-000000000005', 'Dum Gosht Dum Biryani', 1, 390.00, 'Fired', 'Takeaway', false, 0, 0, false, false),
('FC000000-0000-0000-0000-000000000007', 'FB000000-0000-0000-0000-000000000004', '99999999-0000-0000-0000-000000000007', 'Mango Lassi Royale', 1, 95.00, 'Fired', 'Takeaway', false, 0, 0, false, false)
ON CONFLICT ("Id") DO NOTHING;

-- E. Car-Side Ordering (Token #003 in CarParking Channel)
INSERT INTO "TableBills" ("Id", "RestaurantId", "TableId", "TableNumberSnapshot", "OrderChannel", "DailyTokenNumber", "BusinessDate", "VehiclePlate", "ParkingBay", "Subtotal", "PackagingCharge", "PackagingRateSnapshot", "IsPackagingWaived", "LeftoverPackingCharge", "Tax", "Discount", "TotalAmount", "CreatedAtUtc") VALUES
('FA000000-0000-0000-0000-000000000005', '22222222-0000-0000-0000-000000000001', NULL, 0, 'CarParking', 3, TO_CHAR(NOW() AT TIME ZONE 'Asia/Kolkata', 'YYYY-MM-DD'), 'KA01MJ4321', 'Bay 3', 335.00, 20.00, 20.00, false, 0.00, 17.75, 0.00, 372.75, NOW() - INTERVAL '5 minutes')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "Orders" (
  "Id", "RestaurantId", "TableId", "TableBillId", "OrderNumber", "OrderChannel", "DailyTokenNumber", "BusinessDate",
  "CustomerName", "CustomerPhone", "VehiclePlate", "VehicleModel", "ParkingBay",
  "Status", "KotPrintStatus", "IsScheduled", "Subtotal", "PackagingCharge", "Tax", "Discount", "TotalAmount", "CreatedAt", "UpdatedAt"
) VALUES (
  'FB000000-0000-0000-0000-000000000005', '22222222-0000-0000-0000-000000000001', NULL, 'FA000000-0000-0000-0000-000000000005', 105, 'CarParking', 3, TO_CHAR(NOW() AT TIME ZONE 'Asia/Kolkata', 'YYYY-MM-DD'),
  'Sameer Khan', '9900112233', 'KA01MJ4321', 'White Swift VXi', 'Bay 3',
  'Preparing', 'Printed', false, 335.00, 20.00, 17.75, 0.00, 372.75, NOW() - INTERVAL '5 minutes', NOW() - INTERVAL '5 minutes'
) ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "OrderItems" ("Id", "OrderId", "MenuItemId", "ItemNameSnapshot", "Quantity", "UnitPrice", "ReleaseState", "PackingIntent", "IsRefill", "RefillCount", "CancelledQuantity", "IsCancelled", "IsPackagingWaived") VALUES
('FC000000-0000-0000-0000-000000000008', 'FB000000-0000-0000-0000-000000000005', '99999999-0000-0000-0000-000000000001', 'Paneer Tikka Angara', 1, 280.00, 'Fired', 'Takeaway', false, 0, 0, false, false),
('FC000000-0000-0000-0000-000000000009', 'FB000000-0000-0000-0000-000000000005', '99999999-0000-0000-0000-000000000006', 'Butter Garlic Naan', 1, 55.00, 'Fired', 'Takeaway', false, 0, 0, false, false)
ON CONFLICT ("Id") DO NOTHING;

-- Daily Token Sequence sync
INSERT INTO "DailyTokenSequences" ("Id", "RestaurantId", "BusinessDate", "LastTokenNumber", "UpdatedAtUtc") VALUES
(gen_random_uuid(), '22222222-0000-0000-0000-000000000001', TO_CHAR(NOW() AT TIME ZONE 'Asia/Kolkata', 'YYYY-MM-DD'), 3, NOW())
ON CONFLICT ("RestaurantId", "BusinessDate") DO UPDATE SET "LastTokenNumber" = 3;

COMMIT;
