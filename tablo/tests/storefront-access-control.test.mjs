import test from 'node:test';
import assert from 'node:assert/strict';

const API_BASE = 'http://127.0.0.1:5166';

const TENANT1_ID = '22222222-0000-0000-0000-000000000001';
const TENANT1_OUTLET_ID = '22222222-0000-0000-0000-000000000002';

const TENANT2_ID = '22222222-0000-0000-0000-000000000009';
const TENANT2_OUTLET_ID = '22222222-0000-0000-0000-000000000010';

async function login(email, password) {
  const res = await fetch(`${API_BASE}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  });
  if (!res.ok) {
    throw new Error(`Login failed for ${email}: ${res.status}`);
  }
  const data = await res.json();
  return data.token;
}

test('1. Central Kitchen Access Control: Rejects Anonymous, Waiter, and Cross-Tenant Requests', async () => {
  const tenant1AdminToken = await login('admin@tablodemo.com', 'password123');
  const tenant2AdminToken = await login('coastal.admin@tablodemo.com', 'password123');
  const waiterToken = await login('waiter@tablodemo.com', 'password123');

  // Anonymous request to batches -> 401
  const anonRes = await fetch(`${API_BASE}/api/centralkitchen/${TENANT1_ID}/batches`);
  assert.equal(anonRes.status, 401, 'Anonymous request must return 401 Unauthorized');

  // Waiter role to batches -> 403
  const waiterRes = await fetch(`${API_BASE}/api/centralkitchen/${TENANT1_ID}/batches`, {
    headers: { Authorization: `Bearer ${waiterToken}` },
  });
  assert.equal(waiterRes.status, 403, 'Waiter role must return 403 Forbidden');

  // Tenant 2 Admin requesting Tenant 1 batches -> 403
  const crossTenantRes = await fetch(`${API_BASE}/api/centralkitchen/${TENANT1_ID}/batches`, {
    headers: { Authorization: `Bearer ${tenant2AdminToken}` },
  });
  assert.equal(crossTenantRes.status, 403, 'Tenant 2 Admin accessing Tenant 1 must return 403 Forbidden');

  // Tenant 1 Admin requesting Tenant 1 batches -> 200
  const validRes = await fetch(`${API_BASE}/api/centralkitchen/${TENANT1_ID}/batches`, {
    headers: { Authorization: `Bearer ${tenant1AdminToken}` },
  });
  assert.equal(validRes.status, 200, 'Tenant 1 Admin accessing Tenant 1 must return 200 OK');

  const validIngredientId = '44444444-0000-0000-0000-000000000001';

  // Anonymous transfer creation -> 401
  const anonTransfer = await fetch(`${API_BASE}/api/central-kitchen/${TENANT1_ID}/transfers/dispatch`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      sourceKitchenId: TENANT1_OUTLET_ID,
      destinationOutletId: TENANT1_OUTLET_ID,
      ingredientId: validIngredientId,
      itemName: 'Biryani Gravy',
      quantityDispatched: 5,
      unit: 'kg',
    }),
  });
  assert.equal(anonTransfer.status, 401, 'Anonymous transfer post must return 401');

  // Waiter transfer creation -> 403
  const waiterTransfer = await fetch(`${API_BASE}/api/central-kitchen/${TENANT1_ID}/transfers/dispatch`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${waiterToken}`,
    },
    body: JSON.stringify({
      sourceKitchenId: TENANT1_OUTLET_ID,
      destinationOutletId: TENANT1_OUTLET_ID,
      ingredientId: validIngredientId,
      itemName: 'Biryani Gravy',
      quantityDispatched: 5,
      unit: 'kg',
    }),
  });
  assert.equal(waiterTransfer.status, 403, 'Waiter transfer post must return 403');

  // Tenant 2 Admin creating transfer for Tenant 1 -> 403
  const crossTransfer = await fetch(`${API_BASE}/api/central-kitchen/${TENANT1_ID}/transfers/dispatch`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${tenant2AdminToken}`,
    },
    body: JSON.stringify({
      sourceKitchenId: TENANT1_OUTLET_ID,
      destinationOutletId: TENANT1_OUTLET_ID,
      ingredientId: validIngredientId,
      itemName: 'Biryani Gravy',
      quantityDispatched: 5,
      unit: 'kg',
    }),
  });
  assert.equal(crossTransfer.status, 403, 'Cross-tenant transfer post must return 403');
});

test('2. Public Storefront Scope: Strict restaurant isolation and no fallback leaks', async () => {
  // Non-existent restaurant ID -> empty array, never leaks other tenants
  const randomRestId = '00000000-0000-0000-0000-000000000099';
  const noMatchRes = await fetch(`${API_BASE}/api/storefront/restaurants/${randomRestId}/outlets`);
  assert.equal(noMatchRes.status, 200);
  const noMatchOutlets = await noMatchRes.json();
  assert.deepEqual(noMatchOutlets, [], 'Unmatched restaurant must return empty array, never fallback to other outlets');

  // Tenant 1 outlets -> only returns Tenant 1 outlets
  const t1Res = await fetch(`${API_BASE}/api/storefront/restaurants/${TENANT1_ID}/outlets`);
  assert.equal(t1Res.status, 200);
  const t1Outlets = await t1Res.json();
  assert.ok(t1Outlets.length > 0, 'Tenant 1 has published outlets');
  for (const o of t1Outlets) {
    assert.notEqual(o.outletId, TENANT2_OUTLET_ID, 'Tenant 1 outlets must never include Tenant 2 outlet');
  }

  // Invalid outlet ID -> returns 404, never fallback to first outlet
  const invalidOutletRes = await fetch(`${API_BASE}/api/storefront/outlets/00000000-0000-0000-0000-000000000000`);
  assert.equal(invalidOutletRes.status, 404, 'Invalid outlet lookup must return 404');

  // Invalid outlet menu -> returns 404
  const invalidMenuRes = await fetch(`${API_BASE}/api/storefront/outlets/00000000-0000-0000-0000-000000000000/menu`);
  assert.equal(invalidMenuRes.status, 404, 'Invalid outlet menu lookup must return 404');
});

test('3. Storefront Order Tracking & Cross-Tenant Validation', async () => {
  // Invalid tracking token returns 404
  const badTokenRes = await fetch(`${API_BASE}/api/storefront/orders/track/invalid-token-12345`);
  assert.equal(badTokenRes.status, 404, 'Invalid tracking token must return 404');

  // First fetch menu for Tenant 1 to get a valid item
  const menuRes = await fetch(`${API_BASE}/api/storefront/outlets/${TENANT1_OUTLET_ID}/menu`);
  assert.equal(menuRes.status, 200);
  const menu = await menuRes.json();
  const validItem = Array.isArray(menu) ? (menu[0]?.items ? menu[0].items[0] : menu[0]) : null;

  // Try checkout with invalid/cross-tenant item
  const crossItemRes = await fetch(`${API_BASE}/api/storefront/checkout`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      outletId: TENANT1_OUTLET_ID,
      orderChannel: 'TakeawayPickup',
      fulfillmentTiming: 'Immediate',
      customerName: 'Test Guest',
      customerPhone: '9876543210',
      items: [
        {
          menuItemId: '00000000-0000-0000-0000-000000000999',
          name: 'Non Existent Dish',
          quantity: 1,
          unitPrice: 150,
        },
      ],
      paymentMethod: 'PayAtCounter',
      idempotencyKey: `idem-cross-check-${Date.now()}`,
    }),
  });
  assert.equal(crossItemRes.status, 400, 'Cross-tenant or unlinked item must be rejected with 400');

  // Valid checkout produces trackingToken
  const checkoutRes = await fetch(`${API_BASE}/api/storefront/checkout`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      outletId: TENANT1_OUTLET_ID,
      orderChannel: 'TakeawayPickup',
      fulfillmentTiming: 'Immediate',
      customerName: 'Test Guest',
      customerPhone: '9876543210',
      items: [
        {
          menuItemId: validItem.id,
          name: validItem.name,
          quantity: 2,
          unitPrice: validItem.price,
        },
      ],
      paymentMethod: 'PayAtCounter',
      idempotencyKey: `idem-valid-check-${Date.now()}`,
    }),
  });
  assert.equal(checkoutRes.status, 200, 'Valid checkout succeeds');
  const checkoutData = await checkoutRes.json();
  assert.ok(checkoutData.trackingToken, 'Checkout response must include secure trackingToken');

  // Guest tracks order by token without staff authentication
  const trackRes = await fetch(`${API_BASE}/api/storefront/orders/track/${checkoutData.trackingToken}`);
  assert.equal(trackRes.status, 200, 'Guest can track order by secure token');
  const trackData = await trackRes.json();
  assert.equal(trackData.orderNumber, checkoutData.orderNumber);
  assert.ok(trackData.token, 'Has daily token number');
  assert.ok(trackData.status, 'Has order status');
});

test('4. Outlet Settings Admin API & Operating Hours Enforcement', async () => {
  const tenant1AdminToken = await login('admin@tablodemo.com', 'password123');
  const tenant2AdminToken = await login('coastal.admin@tablodemo.com', 'password123');
  const waiterToken = await login('waiter@tablodemo.com', 'password123');

  // Unauthorized updates -> 401 and 403
  const anonUpdate = await fetch(`${API_BASE}/api/storefront/admin/outlets/${TENANT1_OUTLET_ID}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ isTemporarilyClosed: true }),
  });
  assert.equal(anonUpdate.status, 401, 'Anonymous cannot update outlet settings');

  const waiterUpdate = await fetch(`${API_BASE}/api/storefront/admin/outlets/${TENANT1_OUTLET_ID}`, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${waiterToken}`,
    },
    body: JSON.stringify({ isTemporarilyClosed: true }),
  });
  assert.equal(waiterUpdate.status, 403, 'Waiter cannot update outlet settings');

  const crossUpdate = await fetch(`${API_BASE}/api/storefront/admin/outlets/${TENANT1_OUTLET_ID}`, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${tenant2AdminToken}`,
    },
    body: JSON.stringify({ isTemporarilyClosed: true }),
  });
  assert.equal(crossUpdate.status, 403, 'Cross-tenant admin cannot update another outlet settings');

  // Authorized update: set temporarily closed
  const closeRes = await fetch(`${API_BASE}/api/storefront/admin/outlets/${TENANT1_OUTLET_ID}`, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${tenant1AdminToken}`,
    },
    body: JSON.stringify({
      isOpen: true,
      isPublished: true,
      isTemporarilyClosed: true,
      closureReason: 'Kitchen Renovation',
      openingTime: '09:00',
      closingTime: '23:00',
      isOvernight: false,
      leadTimeMinutes: 30,
      minOrderAmount: 250,
      deliveryBaseFee: 40,
      freeDeliveryAboveAmount: 800,
    }),
  });
  assert.equal(closeRes.status, 200, 'Tenant 1 Admin successfully updates outlet settings');

  // Verify storefront reflects closed status
  const outletStatusRes = await fetch(`${API_BASE}/api/storefront/outlets/${TENANT1_OUTLET_ID}`);
  const outletStatus = await outletStatusRes.json();
  assert.equal(outletStatus.isOpenNow, false, 'Outlet should be closed now');
  assert.equal(outletStatus.isTemporarilyClosed, true);
  assert.equal(outletStatus.closureReason, 'Kitchen Renovation');

  // Fetch valid menu item
  const menuRes = await fetch(`${API_BASE}/api/storefront/outlets/${TENANT1_OUTLET_ID}/menu`);
  const menu = await menuRes.json();
  const validItem = Array.isArray(menu) ? (menu[0]?.items ? menu[0].items[0] : menu[0]) : null;

  // Try immediate checkout while closed -> rejected with 400
  const closedCheckout = await fetch(`${API_BASE}/api/storefront/checkout`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      outletId: TENANT1_OUTLET_ID,
      orderChannel: 'TakeawayPickup',
      fulfillmentTiming: 'Immediate',
      customerName: 'Hungry Customer',
      customerPhone: '9876543210',
      items: [{ menuItemId: validItem.id, name: validItem.name, quantity: 1, unitPrice: validItem.price }],
      paymentMethod: 'PayAtCounter',
      idempotencyKey: `idem-closed-check-${Date.now()}`,
    }),
  });
  assert.equal(closedCheckout.status, 400, 'Immediate checkout while closed must be rejected');
  const errBody = await closedCheckout.json();
  assert.ok(errBody.message.includes('Kitchen Renovation') || errBody.message.includes('closed'));

  // Reopen outlet
  const reopenRes = await fetch(`${API_BASE}/api/storefront/admin/outlets/${TENANT1_OUTLET_ID}`, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${tenant1AdminToken}`,
    },
    body: JSON.stringify({
      isOpen: true,
      isPublished: true,
      isTemporarilyClosed: false,
      closureReason: '',
      openingTime: '00:00',
      closingTime: '23:59',
      isOvernight: false,
      leadTimeMinutes: 25,
      minOrderAmount: 100,
      deliveryBaseFee: 40,
      freeDeliveryAboveAmount: 799,
    }),
  });
  assert.equal(reopenRes.status, 200, 'Outlet reopened successfully');
});
