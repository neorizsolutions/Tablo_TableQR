import test from 'node:test';
import assert from 'node:assert/strict';
import { centralKitchenApi } from '../src/services/central-kitchen.ts';

const centralRestaurantId = '22222222-0000-0000-0000-000000000001';
const branchOutletId = '22222222-0000-0000-0000-000000000009';

test.before(async () => {
  try {
    const res = await fetch('http://127.0.0.1:5166/api/auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: 'admin@tablodemo.com', password: 'password123' }),
    });
    if (res.ok) {
      const { token } = await res.json();
      if (typeof globalThis.localStorage === 'undefined') {
        globalThis.localStorage = {
          getItem: (key) => key === 'tablo_token' ? token : null,
          setItem: () => {},
          removeItem: () => {},
        };
      } else {
        globalThis.localStorage.setItem('tablo_token', token);
      }
    }
  } catch {
    // If local API offline, fallback will handle with JWT
  }
});

test('1. Central Production Batch: Posts standardized recipe batch with yield and production costs', async () => {
  const batch = await centralKitchenApi.createProductionBatch({
    restaurantId: centralRestaurantId,
    recipeName: 'Paneer Makhani Gravy Base',
    targetQuantity: 30,
    unit: 'kg',
    unitProductionCost: 160,
  });

  assert.ok(batch.id, 'Batch receives unique identifier');
  assert.equal(batch.recipeName, 'Paneer Makhani Gravy Base');
  assert.equal(batch.targetQuantity, 30);
  assert.equal(batch.totalCost, 4800, '30 kg * ₹160 = ₹4800 total batch cost');
  assert.equal(batch.status, 'Completed');
});

test('2. Outlet Transfer Dispatch: Dispatches stock batch to branch outlet with logged transfer cost', async () => {
  const transfer = await centralKitchenApi.dispatchOutletTransfer({
    sourceKitchenId: centralRestaurantId,
    destinationOutletId: branchOutletId,
    ingredientId: 'ing-makhani-base',
    itemName: 'Paneer Makhani Gravy Base',
    quantityDispatched: 15,
    unit: 'kg',
    unitCost: 160,
  });

  assert.ok(transfer.transferId, 'Generates transfer record');
  assert.equal(transfer.quantityDispatched, 15);
  assert.equal(transfer.dispatchedCost, 2400, '15 kg * ₹160 = ₹2400 dispatched cost');
  assert.equal(transfer.status, 'Dispatched');
  assert.equal(transfer.quantityReceived, 0);
});

test('3. Partial Receipt & Shortage Reconciliation: Records physical dock delivery, shortage, and cost variance', async () => {
  // Dispatch a 10 kg transfer
  const dispatched = await centralKitchenApi.dispatchOutletTransfer({
    sourceKitchenId: centralRestaurantId,
    destinationOutletId: branchOutletId,
    ingredientId: 'ing-dum-gravy',
    itemName: 'Biryani Dum Masala Gravy',
    quantityDispatched: 10,
    unit: 'kg',
    unitCost: 200,
  });

  // Dock receives only 8 kg (2 kg shortage due to damaged transit container)
  const received = await centralKitchenApi.receiveOutletTransfer({
    transferId: dispatched.transferId,
    quantityReceived: 8,
    discrepancyReason: 'Damaged transit container seal',
  });

  assert.equal(received.status, 'PartiallyReceived');
  assert.equal(received.quantityReceived, 8);
  assert.equal(received.shortageQuantity, 2, 'Detects 2 kg shortage');
  assert.equal(received.receivedCost, 1600, '8 kg * ₹200 = ₹1600 received inventory asset');
  assert.equal(received.varianceLossCost, 400, '2 kg * ₹200 = ₹400 variance loss recorded');
  assert.equal(received.discrepancyReason, 'Damaged transit container seal');
});

test('4. Full Receipt without Shortage: Verifies perfect transfer receipt leaves zero variance loss', async () => {
  const dispatched = await centralKitchenApi.dispatchOutletTransfer({
    sourceKitchenId: centralRestaurantId,
    destinationOutletId: branchOutletId,
    ingredientId: 'ing-dal-base',
    itemName: 'Dal Makhani Base',
    quantityDispatched: 12,
    unit: 'kg',
    unitCost: 120,
  });

  const received = await centralKitchenApi.receiveOutletTransfer({
    transferId: dispatched.transferId,
    quantityReceived: 12,
  });

  assert.equal(received.status, 'FullyReceived');
  assert.equal(received.shortageQuantity, 0);
  assert.equal(received.varianceLossCost, 0);
});

test('5. Multi-Outlet Menu Publish Preview: Compiles change diff with localized price and availability rules', async () => {
  const preview = await centralKitchenApi.previewMenuPublish({
    centralRestaurantId,
    targetOutletIds: [centralRestaurantId, branchOutletId],
  });

  assert.ok(preview.targetOutletIds.includes(branchOutletId));
  assert.ok(preview.itemsToAddCount > 0);
  assert.ok(Array.isArray(preview.overridesPreview));
});

test('6. Multi-Outlet Menu Publish Execution: Broadcasts catalog updates across all target outlets', async () => {
  const result = await centralKitchenApi.executeMenuPublish({
    centralRestaurantId,
    targetOutletIds: [centralRestaurantId, branchOutletId],
    itemOverrides: [
      {
        menuItemId: 'item-pbm',
        itemName: 'Paneer Butter Masala',
        centralPrice: 320,
        outletPriceOverride: 360,
      },
      {
        menuItemId: 'item-bir',
        itemName: 'Hyderabadi Chicken Biryani',
        centralPrice: 380,
        outletPriceOverride: 420,
      },
    ],
  });

  assert.equal(result.success, true);
  assert.equal(result.outletsUpdatedCount, 2, 'Updates both outlets');
  assert.ok(result.itemsUpdatedCount >= 2, 'Updates specified item overrides');
  assert.ok(result.publishedAtUtc, 'Logs publish timestamp');
});
