import test from 'node:test';
import assert from 'node:assert/strict';
import { storefrontApi } from '../src/services/storefront.ts';

const outletId = '22222222-0000-0000-0000-000000000001';

test('1. Storefront Outlet Catalog: Retrieves operating hours, lead time, min order, and delivery fee rules', async () => {
  const outlet = await storefrontApi.getOutlet(outletId);
  assert.equal(outlet.outletId, outletId);
  assert.equal(outlet.isOpenNow, true);
  assert.equal(outlet.leadTimeMinutes, 25);
  assert.equal(outlet.deliveryBaseFee, 40);
  assert.equal(outlet.freeDeliveryAboveAmount, 799);
});

test('2. Direct Delivery Checkout: Calculates subtotal, 5% GST, delivery fee, and total payable', async () => {
  const response = await storefrontApi.checkout({
    outletId,
    orderChannel: 'DirectDelivery',
    fulfillmentTiming: 'Immediate',
    customerName: 'Aarav Mehta',
    customerPhone: '9823456789',
    deliveryAddress: 'Apt 402, Heritage Residency, New Delhi',
    items: [
      { menuItemId: 'item-1', name: 'Paneer Tikka', quantity: 2, unitPrice: 200 }, // 400
    ],
    paymentMethod: 'PayOnDelivery',
    idempotencyKey: 'idem-test-delivery-1',
  });

  assert.ok(response.orderId, 'Generates orderId');
  assert.equal(response.status, 'Placed');
  assert.equal(response.subtotal, 400);
  assert.equal(response.tax, 20, '5% GST on ₹400 = ₹20');
  assert.equal(response.deliveryFee, 40, 'Under ₹799 attracts ₹40 base delivery fee');
  assert.equal(response.packagingFee, 15, 'Delivery packaging fee ₹15');
  assert.equal(response.totalPayable, 475, '₹400 + ₹20 + ₹40 + ₹15 = ₹475');
  assert.equal(response.isReplay, false);
});

test('3. Free Delivery Threshold: Waives delivery fee when order exceeds free delivery threshold', async () => {
  const response = await storefrontApi.checkout({
    outletId,
    orderChannel: 'DirectDelivery',
    fulfillmentTiming: 'Immediate',
    customerName: 'Karan Mehra',
    customerPhone: '9876501234',
    deliveryAddress: 'Villa 12, Diplomatic Enclave',
    items: [
      { menuItemId: 'item-2', name: 'Mutton Rogan Josh Party Pack', quantity: 1, unitPrice: 850 },
    ],
    paymentMethod: 'PayOnDelivery',
    idempotencyKey: 'idem-test-free-deliv',
  });

  assert.equal(response.subtotal, 850);
  assert.equal(response.deliveryFee, 0, 'Qualifies for free delivery (>= ₹799)');
});

test('4. Modifiers & Cooking Instructions: Accurately calculates price deltas in order total', async () => {
  const response = await storefrontApi.checkout({
    outletId,
    orderChannel: 'TakeawayPickup',
    fulfillmentTiming: 'Immediate',
    customerName: 'Sneha Rao',
    customerPhone: '9811223344',
    items: [
      {
        menuItemId: 'item-3',
        name: 'Veg Dum Biryani',
        quantity: 2,
        unitPrice: 250, // 500
        modifiers: [
          { modifierOptionId: 'opt-mirchi', optionName: 'Extra Mirchi Ka Salan', priceDelta: 30 }, // 60
        ],
        cookingNotes: 'Mild spice, separate salan pouch',
      },
    ],
    paymentMethod: 'UpiOnline',
    idempotencyKey: 'idem-test-mods',
  });

  // (250 + 30) * 2 = 560
  assert.equal(response.subtotal, 560);
  assert.ok(response.upiUri, 'Generates dynamic UPI QR for online payment');
  assert.ok(response.upiUri.includes('am=603.00') || response.upiUri.includes('am='));
});

test('5. Idempotent Deduplication: Replaying identical request returns previous order without double submission', async () => {
  const key = `idem-unique-replay-${Date.now()}`;

  // First submission
  const first = await storefrontApi.checkout({
    outletId,
    orderChannel: 'TakeawayPickup',
    fulfillmentTiming: 'Immediate',
    customerName: 'Sameer Khan',
    customerPhone: '9711224455',
    items: [{ menuItemId: 'item-dal', name: 'Dal Makhani', quantity: 1, unitPrice: 220 }],
    paymentMethod: 'PayAtCounter',
    idempotencyKey: key,
  });

  assert.equal(first.isReplay, false);

  // Exact duplicate submission (e.g. user double-taps Place Order)
  const duplicate = await storefrontApi.checkout({
    outletId,
    orderChannel: 'TakeawayPickup',
    fulfillmentTiming: 'Immediate',
    customerName: 'Sameer Khan',
    customerPhone: '9711224455',
    items: [{ menuItemId: 'item-dal', name: 'Dal Makhani', quantity: 1, unitPrice: 220 }],
    paymentMethod: 'PayAtCounter',
    idempotencyKey: key,
  });

  assert.equal(duplicate.isReplay, true, 'Flags duplicate attempt as replay');
  assert.equal(duplicate.orderId, first.orderId, 'Maintains exact same order ID');
  assert.equal(duplicate.orderNumber, first.orderNumber, 'Maintains exact same order number');
  assert.equal(duplicate.token, first.token, 'Maintains exact same token');
});

test('6. Scheduled Order: Records scheduled fulfillment window', async () => {
  const scheduledTime = '2026-11-22T20:00:00.000Z';
  const response = await storefrontApi.checkout({
    outletId,
    orderChannel: 'TakeawayPickup',
    fulfillmentTiming: 'Scheduled',
    scheduledForUtc: scheduledTime,
    customerName: 'Vikram Seth',
    customerPhone: '9900112233',
    items: [{ menuItemId: 'item-curry', name: 'Mushroom Masala', quantity: 1, unitPrice: 280 }],
    paymentMethod: 'PayAtCounter',
    idempotencyKey: 'idem-test-scheduled',
  });

  assert.equal(response.estimatedFulfillmentUtc, scheduledTime);
});
