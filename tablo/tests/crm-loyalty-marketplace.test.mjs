import test from 'node:test';
import assert from 'node:assert/strict';
import { handleLocalModuleFallback } from '../src/services/localModuleFallback.ts';

const RESTAURANT_ID = '22222222-0000-0000-0000-000000000001';

test('1. Returning customer search: Finds customer by phone or tag with loyalty balance', async () => {
  const result = handleLocalModuleFallback(`/restaurants/${RESTAURANT_ID}/crm/customers?search=9876543210`);
  assert.ok(Array.isArray(result), 'Result should be an array');
  assert.ok(result.length >= 1, 'Should find at least 1 customer');
  const customer = result[0];
  assert.equal(customer.name, 'Rahul Sharma');
  assert.equal(customer.phone, '9876543210');
  assert.ok(customer.loyaltyPointsBalance >= 100, 'Customer should have loyalty points');
  assert.ok(customer.tags.includes('VIP'), 'Should contain VIP tag');
});

test('2. Tenant-scoped customer creation: Registers new profile with contact and preferences', async () => {
  const newPhone = `91${Math.floor(10000000 + Math.random() * 90000000)}`;
  const created = handleLocalModuleFallback(`/restaurants/${RESTAURANT_ID}/crm/customers`, {
    method: 'POST',
    body: JSON.stringify({
      phone: newPhone,
      name: 'Kavita Singhania',
      email: 'kavita@example.test',
      notes: 'Likes non-spicy food, window tables',
      tags: ['Regular', 'Corporate'],
    }),
  });

  assert.ok(created, 'Customer should be created');
  assert.equal(created.phone, newPhone);
  assert.equal(created.name, 'Kavita Singhania');
  assert.equal(created.loyaltyPointsBalance, 0);

  const lookup = handleLocalModuleFallback(`/restaurants/${RESTAURANT_ID}/crm/customers?search=${newPhone}`);
  assert.equal(lookup.length, 1);
  assert.equal(lookup[0].name, 'Kavita Singhania');
});

test('3. Loyalty earning on settled sale: Awards points once and is retry-safe (idempotent)', async () => {
  const customers = handleLocalModuleFallback(`/restaurants/${RESTAURANT_ID}/crm/customers?search=9876543210`);
  const customer = customers[0];
  const initialBalance = customer.loyaltyPointsBalance;
  const billId = `bill-test-${Date.now()}`;
  const eligibleAmount = 2500; // 25 points (1 pt per 100 INR)
  const idempotencyKey = `EARN-${billId}`;

  // First settlement attempt
  const firstEarn = handleLocalModuleFallback(`/restaurants/${RESTAURANT_ID}/crm/loyalty/earn`, {
    method: 'POST',
    body: JSON.stringify({
      customerId: customer.id,
      billId,
      eligibleAmount,
      idempotencyKey,
    }),
  });

  assert.equal(firstEarn.pointsEarned, 25);
  assert.equal(firstEarn.newBalance, initialBalance + 25);

  // Second settlement attempt with duplicate idempotency key (simulating retry or webhook replay)
  const secondEarn = handleLocalModuleFallback(`/restaurants/${RESTAURANT_ID}/crm/loyalty/earn`, {
    method: 'POST',
    body: JSON.stringify({
      customerId: customer.id,
      billId,
      eligibleAmount,
      idempotencyKey,
    }),
  });

  // Must return the exact same balance and NOT double-award
  assert.equal(secondEarn.newBalance, initialBalance + 25);
  assert.equal(customer.loyaltyPointsBalance, initialBalance + 25);
});

test('4. Loyalty redemption: Deducts points, records ledger entry with audit trail', async () => {
  const customers = handleLocalModuleFallback(`/restaurants/${RESTAURANT_ID}/crm/customers?search=9876543210`);
  const customer = customers[0];
  const initialBalance = customer.loyaltyPointsBalance;
  const pointsToRedeem = 30; // ₹30 discount
  const billId = `bill-redeem-${Date.now()}`;

  const redemption = handleLocalModuleFallback(`/restaurants/${RESTAURANT_ID}/crm/loyalty/redeem`, {
    method: 'POST',
    body: JSON.stringify({
      customerId: customer.id,
      billId,
      pointsToRedeem,
      idempotencyKey: `REDEEM-${billId}`,
    }),
  });

  assert.equal(redemption.pointsRedeemed, 30);
  assert.equal(redemption.discountAmountInr, 30);
  assert.equal(redemption.newBalance, initialBalance - 30);
  assert.equal(customer.loyaltyPointsBalance, initialBalance - 30);
  assert.equal(redemption.entry.transactionType, 'Redeem');
  assert.equal(redemption.entry.points, -30);
});

test('5. Loyalty over-redemption protection: Rejects redemption when points exceed balance', async () => {
  const customers = handleLocalModuleFallback(`/restaurants/${RESTAURANT_ID}/crm/customers?search=9988776655`);
  const customer = customers[0];
  const initialBalance = customer.loyaltyPointsBalance; // 10 pts

  assert.throws(
    () => {
      handleLocalModuleFallback(`/restaurants/${RESTAURANT_ID}/crm/loyalty/redeem`, {
        method: 'POST',
        body: JSON.stringify({
          customerId: customer.id,
          billId: 'bill-excess',
          pointsToRedeem: 9999, // Exceeds balance
        }),
      });
    },
    /Insufficient loyalty balance/i
  );

  assert.equal(customer.loyaltyPointsBalance, initialBalance);
});

test('6. Receipt-linked feedback: Submits guest ratings and captures comments', async () => {
  const feedback = handleLocalModuleFallback(`/restaurants/${RESTAURANT_ID}/crm/feedback`, {
    method: 'POST',
    body: JSON.stringify({
      billId: 'bill-fb-test',
      customerName: 'Sneha Rao',
      customerPhone: '9876500000',
      channel: 'DineIn',
      tableNumber: 3,
      overallRating: 5,
      foodRating: 5,
      serviceRating: 5,
      ambianceRating: 4,
      comments: 'The biryani was aromatic and well-cooked. Great ambiance.',
    }),
  });

  assert.ok(feedback.id);
  assert.equal(feedback.overallRating, 5);
  assert.equal(feedback.status, 'New');

  const allFeedback = handleLocalModuleFallback(`/restaurants/${RESTAURANT_ID}/crm/feedback`);
  const found = allFeedback.find((f) => f.id === feedback.id);
  assert.ok(found);
  assert.ok(found.comments.includes('biryani was aromatic'));
});

test('7. Marketplace store outlet management: Toggles Online and Paused receiving states', async () => {
  const stores = handleLocalModuleFallback(`/restaurants/${RESTAURANT_ID}/marketplace/stores`);
  assert.equal(stores.length, 2);
  const swiggy = stores.find((s) => s.provider === 'Swiggy');
  assert.ok(swiggy);
  assert.equal(swiggy.storeStatus, 'Online');

  // Toggle to Paused
  const paused = handleLocalModuleFallback(`/restaurants/${RESTAURANT_ID}/marketplace/stores/swiggy/toggle-status`, {
    method: 'POST',
    body: JSON.stringify({ status: 'Paused' }),
  });
  assert.equal(paused.storeStatus, 'Paused');

  // Toggle back to Online
  const online = handleLocalModuleFallback(`/restaurants/${RESTAURANT_ID}/marketplace/stores/swiggy/toggle-status`, {
    method: 'POST',
    body: JSON.stringify({ status: 'Online' }),
  });
  assert.equal(online.storeStatus, 'Online');
});

test('8. Marketplace catalog sync: Toggles item availability across platforms', async () => {
  const items = handleLocalModuleFallback(`/restaurants/${RESTAURANT_ID}/marketplace/items`);
  assert.ok(items.length >= 2);
  const butterChicken = items.find((i) => i.menuItemName === 'Butter Chicken');
  assert.ok(butterChicken);
  assert.equal(butterChicken.isAvailable, true);

  // Toggle off (sold out)
  const toggledOff = handleLocalModuleFallback(`/restaurants/${RESTAURANT_ID}/marketplace/items/${butterChicken.id}/toggle-availability`, {
    method: 'POST',
    body: JSON.stringify({ isAvailable: false }),
  });
  assert.equal(toggledOff.isAvailable, false);

  // Toggle back on
  const toggledOn = handleLocalModuleFallback(`/restaurants/${RESTAURANT_ID}/marketplace/items/${butterChicken.id}/toggle-availability`, {
    method: 'POST',
    body: JSON.stringify({ isAvailable: true }),
  });
  assert.equal(toggledOn.isAvailable, true);
});

test('9. Inbound order webhook & KOT routing: Splits food and beverages across station KOTs', async () => {
  const eventId = `EVT-TEST-${Date.now()}`;
  const result = handleLocalModuleFallback(`/restaurants/${RESTAURANT_ID}/marketplace/webhook/swiggy`, {
    method: 'POST',
    body: JSON.stringify({
      eventId,
      provider: 'Swiggy',
      eventType: 'ORDER_PLACED',
      externalOrderId: 'SWIGGY-ORD-9021',
      externalStoreId: 'SWIGGY-BLR-001',
      totalAmount: 600,
      customerName: 'Nitin Gadkari',
      items: [
        { externalItemId: 'SW-BC-01', name: 'Butter Chicken', quantity: 1, price: 480 },
        { externalItemId: 'SW-FLS-04', name: 'Fresh Lime Soda', quantity: 1, price: 120 },
      ],
    }),
  });

  assert.equal(result.status, 'Processed');
  assert.ok(result.orderId);
  assert.ok(result.tokenNumber >= 800);
  assert.equal(result.generatedKotTicketIds.length, 2, 'Should generate 2 KOTs: 1 Food + 1 Beverage');
  assert.ok(result.generatedKotTicketIds.some((k) => k.includes('FOOD')));
  assert.ok(result.generatedKotTicketIds.some((k) => k.includes('BEV')));
});

test('10. Inbound event deduplication: Replaying same eventId creates 0 new orders and 0 new KOTs', async () => {
  const fixedEventId = `STATIC-DEDUP-${Date.now()}`;
  const payload = {
    eventId: fixedEventId,
    provider: 'Zomato',
    eventType: 'ORDER_PLACED',
    externalOrderId: 'ZOMATO-ORD-4433',
    externalStoreId: 'ZOMATO-BLR-002',
    totalAmount: 420,
    items: [{ externalItemId: 'SW-PBM-02', name: 'Paneer Butter Masala', quantity: 1, price: 420 }],
  };

  // First ingestion: Processed
  const firstAttempt = handleLocalModuleFallback(`/restaurants/${RESTAURANT_ID}/marketplace/webhook/zomato`, {
    method: 'POST',
    body: JSON.stringify(payload),
  });
  assert.equal(firstAttempt.status, 'Processed');
  assert.equal(firstAttempt.generatedKotTicketIds.length, 1);

  // Second ingestion: Duplicate!
  const secondAttempt = handleLocalModuleFallback(`/restaurants/${RESTAURANT_ID}/marketplace/webhook/zomato`, {
    method: 'POST',
    body: JSON.stringify(payload),
  });
  assert.equal(secondAttempt.status, 'Duplicate');
  assert.equal(secondAttempt.generatedKotTicketIds.length, 0, 'No extra KOTs created on duplicate');
  assert.ok(secondAttempt.message.includes('already been processed'));
});

test('11. Outbound status sync & retry: Retries failed status updates with incremented counter', async () => {
  const queue = handleLocalModuleFallback(`/restaurants/${RESTAURANT_ID}/marketplace/outbound-updates`);
  assert.ok(queue.length >= 1);
  const update = queue[0];
  const initialAttempts = update.attempts;

  const retry = handleLocalModuleFallback(`/restaurants/${RESTAURANT_ID}/marketplace/outbound-updates/${update.id}/retry`, {
    method: 'POST',
  });
  assert.equal(retry.success, true);

  const refreshedQueue = handleLocalModuleFallback(`/restaurants/${RESTAURANT_ID}/marketplace/outbound-updates`);
  const updated = refreshedQueue.find((u) => u.id === update.id);
  assert.equal(updated.attempts, initialAttempts + 1);
  assert.equal(updated.status, 'Success');
});
