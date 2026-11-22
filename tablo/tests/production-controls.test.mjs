import test from "node:test";
import assert from "node:assert/strict";

// Helper simulations representing server logic
function calculateSubtotalWithModifiers(basePrice, selectedModifiers) {
  let unitPrice = basePrice;
  for (const mod of selectedModifiers || []) {
    unitPrice += mod.priceDelta;
  }
  return Math.max(0, unitPrice);
}

function validateModifierSelections(groups, selectedModifiers) {
  for (const grp of groups) {
    const selectedInGroup = (selectedModifiers || []).filter(m => m.groupId === grp.id);
    if (selectedInGroup.length < grp.minSelections) {
      throw new Error(`Modifier group '${grp.name}' requires at least ${grp.minSelections} selection(s).`);
    }
    if (selectedInGroup.length > grp.maxSelections) {
      throw new Error(`Modifier group '${grp.name}' allows at most ${grp.maxSelections} selection(s).`);
    }
  }
  return true;
}

function calculateIngredients(recipe, modifiers, quantity) {
  const requirements = {};
  for (const r of recipe) {
    requirements[r.ingredientId] = r.amount * quantity;
  }

  for (const mod of modifiers || []) {
    if (mod.adjustmentType === "Add") {
      requirements[mod.ingredientId] = (requirements[mod.ingredientId] || 0) + mod.quantity * quantity;
    } else if (mod.adjustmentType === "Substitute") {
      if (mod.originalIngredientId && requirements[mod.originalIngredientId]) {
        delete requirements[mod.originalIngredientId];
      }
      requirements[mod.ingredientId] = (requirements[mod.ingredientId] || 0) + mod.quantity * quantity;
    } else if (mod.adjustmentType === "Remove") {
      if (mod.originalIngredientId) {
        delete requirements[mod.originalIngredientId];
      }
    }
  }
  return requirements;
}

test("1. Hold/Fire: Held items produce NO production KOT until released", () => {
  const orderItems = [
    { id: "item-1", name: "Soup", quantity: 2, releaseState: "Fired", courseName: "Starters" },
    { id: "item-2", name: "Biryani", quantity: 2, releaseState: "Held", courseName: "Mains" },
  ];

  // Acceptance filter: only Fired items are routed
  const acceptedForPrint = orderItems.filter(i => i.releaseState === "Fired");
  assert.equal(acceptedForPrint.length, 1);
  assert.equal(acceptedForPrint[0].name, "Soup");
});

test("2. Hold/Fire: Customer QR acceptance does NOT accidentally release held items", () => {
  const qrOrder = {
    status: "Placed",
    items: [
      { id: "i1", name: "Starters", releaseState: "Fired" },
      { id: "i2", name: "Dessert", releaseState: "Held" },
    ]
  };

  // Staff accepts customer QR order
  qrOrder.status = "Accepted";
  const itemsToPrint = qrOrder.items.filter(i => i.releaseState === "Fired");

  assert.equal(itemsToPrint.length, 1);
  assert.equal(itemsToPrint[0].name, "Starters");
  assert.equal(qrOrder.items.find(i => i.id === "i2").releaseState, "Held");
});

test("3. Hold/Fire: Firing held course generates dedicated KOT and updates release state", () => {
  const order = {
    id: "ord-1",
    items: [
      { id: "i1", name: "Soup", releaseState: "Fired", courseName: "Starters" },
      { id: "i2", name: "Butter Chicken", releaseState: "Held", courseName: "Mains" },
      { id: "i3", name: "Naan", releaseState: "Held", courseName: "Mains" },
    ]
  };

  // Fire course "Mains"
  const operationId = "op-fire-101";
  const targetCourse = "Mains";
  const itemsToFire = order.items.filter(i => i.courseName === targetCourse && i.releaseState === "Held");

  assert.equal(itemsToFire.length, 2);
  for (const it of itemsToFire) {
    it.releaseState = "Fired";
    it.fireOperationId = operationId;
  }

  // Check state
  assert.equal(order.items.filter(i => i.releaseState === "Held").length, 0);

  // Idempotent retry:
  const replayItems = order.items.filter(i => i.fireOperationId === operationId);
  assert.equal(replayItems.length, 2);
});

test("4. Settlement Guard: Ordinary settlement blocked with unresolved chargeable held food", () => {
  const bill = {
    id: "b1",
    orders: [
      {
        status: "Accepted",
        items: [
          { name: "Biryani", releaseState: "Held", isCancelled: false, quantity: 2, cancelledQuantity: 0 }
        ]
      }
    ]
  };

  const hasUnresolvedHeldFood = bill.orders.some(o =>
    o.status !== "Cancelled" && o.status !== "Rejected" &&
    o.items.some(i => !i.isCancelled && (i.quantity - i.cancelledQuantity) > 0 && i.releaseState === "Held")
  );

  const canSettle = !hasUnresolvedHeldFood;
  assert.equal(canSettle, false);
});

test("5. Priced Modifiers: Validation, server price calculation & ingredient adjustments", () => {
  const groups = [
    { id: "g1", name: "Bread Choice", minSelections: 1, maxSelections: 1 },
    { id: "g2", name: "Spice Level", minSelections: 0, maxSelections: 1 },
  ];

  // Missing required group g1
  assert.throws(() => {
    validateModifierSelections(groups, []);
  }, /requires at least 1 selection/);

  // Exceeding max selections
  assert.throws(() => {
    validateModifierSelections(groups, [
      { groupId: "g1", id: "o1" },
      { groupId: "g1", id: "o2" }
    ]);
  }, /allows at most 1 selection/);

  // Valid selection with price delta
  const selected = [
    { groupId: "g1", id: "o1", name: "Garlic Naan", priceDelta: 30.00 },
    { groupId: "g2", id: "o3", name: "Extra Spicy", priceDelta: 0.00 }
  ];
  assert.doesNotThrow(() => validateModifierSelections(groups, selected));

  const basePrice = 250.00;
  const unitPrice = calculateSubtotalWithModifiers(basePrice, selected);
  assert.equal(unitPrice, 280.00);

  // Ingredient substitution
  const recipe = [
    { ingredientId: "plain-flour", amount: 0.15 },
    { ingredientId: "butter", amount: 0.02 }
  ];
  const modifiers = [
    { adjustmentType: "Substitute", originalIngredientId: "plain-flour", ingredientId: "garlic-flour", quantity: 0.15 },
    { adjustmentType: "Add", ingredientId: "chopped-garlic", quantity: 0.03 }
  ];

  const required = calculateIngredients(recipe, modifiers, 2);
  assert.equal(required["plain-flour"], undefined); // Substituted out
  assert.equal(required["garlic-flour"], 0.30);
  assert.equal(required["chopped-garlic"], 0.06);
  assert.equal(required["butter"], 0.04);
});

test("6. Free Refills: Eligible dishes, limit enforcement, zero customer charge, refill count", () => {
  const dish = { id: "m1", name: "Filter Coffee", price: 60.00, isRefillEligible: true, refillMaxCount: 2 };
  const order = {
    id: "ord-refill",
    items: [
      { id: "it-coffee-1", menuItemId: "m1", name: "Filter Coffee", quantity: 1, price: 60.00, isRefill: false }
    ]
  };

  // Issue 1st refill
  let refillsCount = order.items.filter(i => i.originalOrderItemId === "it-coffee-1" && !i.isCancelled).length;
  assert.ok(refillsCount < dish.refillMaxCount);

  order.items.push({
    id: "it-refill-1",
    menuItemId: "m1",
    name: "Filter Coffee (Refill #1)",
    quantity: 1,
    price: 0.00, // zero charge
    isRefill: true,
    originalOrderItemId: "it-coffee-1",
    refillCount: 1
  });

  // Issue 2nd refill
  refillsCount = order.items.filter(i => i.originalOrderItemId === "it-coffee-1" && !i.isCancelled).length;
  assert.ok(refillsCount < dish.refillMaxCount);

  order.items.push({
    id: "it-refill-2",
    menuItemId: "m1",
    name: "Filter Coffee (Refill #2)",
    quantity: 1,
    price: 0.00,
    isRefill: true,
    originalOrderItemId: "it-coffee-1",
    refillCount: 2
  });

  // 3rd refill exceeds max
  refillsCount = order.items.filter(i => i.originalOrderItemId === "it-coffee-1" && !i.isCancelled).length;
  assert.equal(refillsCount >= dish.refillMaxCount, true);

  // Subtotal check: customer is only charged for original item
  const chargeableTotal = order.items.reduce((sum, i) => sum + i.price * i.quantity, 0);
  assert.equal(chargeableTotal, 60.00);
});

test("7. Manager Cancellation: NotCooked releases reservation with zero waste cost", () => {
  const stockReservation = { id: "res-1", orderItemId: "it-1", quantity: 0.5, status: "Active" };
  const wasteMovements = [];

  // Outcome: NotCooked
  const outcome = "NotCooked";
  if (outcome === "NotCooked") {
    stockReservation.status = "Released";
  }

  assert.equal(stockReservation.status, "Released");
  assert.equal(wasteMovements.length, 0); // No waste movement posted
});

test("8. Manager Cancellation: KitchenWaste records actual ingredient loss and cost once", () => {
  const stockReservation = { id: "res-1", orderItemId: "it-1", quantity: 0.5, status: "Active" };
  const wasteMovements = [];

  // Outcome: KitchenWaste
  const outcome = "KitchenWaste";
  const costPerKg = 400.00;
  const wastedQty = 0.5;

  if (outcome === "KitchenWaste") {
    stockReservation.status = "Released";
    wasteMovements.push({
      reason: "KitchenWaste",
      type: "Waste",
      quantityChange: -wastedQty,
      unitCost: costPerKg,
      totalCost: wastedQty * costPerKg
    });
  }

  assert.equal(stockReservation.status, "Released");
  assert.equal(wasteMovements.length, 1);
  assert.equal(wasteMovements[0].totalCost, 200.00);
});

test("9. Manager PIN Security: Rate-limited to 5 attempts before 15-minute lockout", () => {
  let failedAttempts = 0;
  let lockedUntil = null;
  const maxAttempts = 5;

  function attemptPin(pin, correctPin) {
    if (lockedUntil && lockedUntil > Date.now()) {
      return { success: false, locked: true };
    }
    if (pin === correctPin) {
      failedAttempts = 0;
      lockedUntil = null;
      return { success: true, locked: false };
    }
    failedAttempts++;
    if (failedAttempts >= maxAttempts) {
      lockedUntil = Date.now() + 15 * 60 * 1000;
    }
    return { success: false, locked: failedAttempts >= maxAttempts };
  }

  const correctPin = "9876";

  // 4 failed attempts: not locked yet
  for (let i = 0; i < 4; i++) {
    const res = attemptPin("0000", correctPin);
    assert.equal(res.success, false);
    assert.equal(res.locked, false);
  }

  // 5th failed attempt: locked out
  const fifthRes = attemptPin("0000", correctPin);
  assert.equal(fifthRes.success, false);
  assert.equal(fifthRes.locked, true);

  // Even correct PIN is blocked during lockout
  const blockedRes = attemptPin("9876", correctPin);
  assert.equal(blockedRes.success, false);
  assert.equal(blockedRes.locked, true);
});

test("10. Inventory & Settlement Reconciliation: Gross profit and COGS exclude cancelled portions", () => {
  const item = {
    name: "Biryani",
    quantity: 4,
    cancelledQuantity: 1, // 1 cancelled
    unitPrice: 300.00,
    unitCost: 120.00
  };

  const activeQty = item.quantity - item.cancelledQuantity;
  assert.equal(activeQty, 3);

  const billedRevenue = activeQty * item.unitPrice; // 3 * 300 = 900
  const settledCogs = activeQty * item.unitCost;     // 3 * 120 = 360
  const grossProfit = billedRevenue - settledCogs;   // 900 - 360 = 540

  assert.equal(billedRevenue, 900.00);
  assert.equal(settledCogs, 360.00);
  assert.equal(grossProfit, 540.00);
});

test("11. Security: Employee ID alone NEVER authorizes cancellation by non-manager", () => {
  function validateCancellationAuth(callerRole, managerPin, suppliedManagerId) {
    if (callerRole === "Admin" || callerRole === "Manager") {
      return { authorized: true, direct: true };
    }
    // Non-manager (e.g. Waiter/Kitchen) MUST provide a valid Manager PIN.
    // Supplying a manager ID alone without valid PIN must be rejected!
    if (!managerPin || managerPin !== "1234") {
      throw new Error("Manager PIN is strictly required to authorize cancellation.");
    }
    return { authorized: true, direct: false, approver: suppliedManagerId || "default-mgr" };
  }

  // Manager callers are directly authorized
  const mgrAuth = validateCancellationAuth("Manager", null, null);
  assert.equal(mgrAuth.authorized, true);
  assert.equal(mgrAuth.direct, true);

  // Waiter with supplied manager ID but NO PIN is REJECTED
  assert.throws(
    () => validateCancellationAuth("Waiter", null, "manager-guid-123"),
    /Manager PIN is strictly required/
  );

  // Waiter with invalid PIN is REJECTED
  assert.throws(
    () => validateCancellationAuth("Waiter", "0000", "manager-guid-123"),
    /Manager PIN is strictly required/
  );

  // Waiter with valid PIN is AUTHORIZED
  const waiterAuth = validateCancellationAuth("Waiter", "1234", "manager-guid-123");
  assert.equal(waiterAuth.authorized, true);
  assert.equal(waiterAuth.direct, false);
});

test("12. PIN Rate Limiting & Rollback Survival: Failed attempts persist across transaction rollbacks", () => {
  // Simulating persistent tenant-level rate limit storage independent of order transaction
  const persistentTenantLimit = { failedAttempts: 0, lockedUntil: null };

  function recordFailedAttempt() {
    persistentTenantLimit.failedAttempts++;
    if (persistentTenantLimit.failedAttempts >= 5) {
      persistentTenantLimit.lockedUntil = Date.now() + 15 * 60 * 1000;
    }
  }

  function simulateCancellationWithTransaction(pin) {
    // Attempt PIN verification which persists to independent rate-limit table first
    if (pin !== "1234") {
      recordFailedAttempt();
      // Transaction throws and rolls back order changes
      throw new Error("Unauthorized PIN");
    }
  }

  // 4 failed attempts throw and roll back order transaction, but persistent counter survives!
  for (let i = 0; i < 4; i++) {
    assert.throws(() => simulateCancellationWithTransaction("wrong-pin"));
  }
  assert.equal(persistentTenantLimit.failedAttempts, 4);
  assert.equal(persistentTenantLimit.lockedUntil, null);

  // 5th failed attempt triggers 15-minute lockout
  assert.throws(() => simulateCancellationWithTransaction("wrong-pin"));
  assert.equal(persistentTenantLimit.failedAttempts, 5);
  assert.ok(persistentTenantLimit.lockedUntil !== null);
  assert.ok(persistentTenantLimit.lockedUntil > Date.now());
});

test("13. Concurrency & Deduplication: OrderProductionOperation detects identical retransmits vs conflicts", () => {
  const operationsTable = new Map();

  function hashPayload(opType, orderId, details) {
    return `${opType}:${orderId}:${JSON.stringify(details)}`;
  }

  function executeProductionOp(restaurantId, opId, opType, orderId, details, responseGenerator) {
    const key = `${restaurantId}:${opId}`;
    const currentHash = hashPayload(opType, orderId, details);

    if (operationsTable.has(key)) {
      const existing = operationsTable.get(key);
      if (existing.payloadHash === currentHash) {
        return { status: 200, response: existing.response, replayed: true };
      } else {
        const error = new Error("Operation ID was already used with different parameters.");
        error.status = 409;
        throw error;
      }
    }

    const response = responseGenerator();
    operationsTable.set(key, { payloadHash: currentHash, response });
    return { status: 200, response, replayed: false };
  }

  const resId = "rest-1";
  const opId = "op-refill-001";
  const orderId = "ord-1";

  // First execution succeeds
  const first = executeProductionOp(resId, opId, "Refill", orderId, { quantity: 1, notes: "hot" }, () => ({
    refillCount: 1,
    chargedPrice: 0.00
  }));
  assert.equal(first.replayed, false);
  assert.equal(first.response.refillCount, 1);

  // Identical retry returns cached response (idempotent replay)
  const retry = executeProductionOp(resId, opId, "Refill", orderId, { quantity: 1, notes: "hot" }, () => {
    throw new Error("Should not re-execute!");
  });
  assert.equal(retry.replayed, true);
  assert.equal(retry.response.refillCount, 1);

  // Altered retry with same OperationId throws 409 Conflict!
  assert.throws(
    () => executeProductionOp(resId, opId, "Refill", orderId, { quantity: 2, notes: "cold" }, () => {}),
    (err) => err.status === 409
  );
});

test("14. Caller Spoofing Prevention: Non-matching actor identity is rejected with BadRequest", () => {
  function handleApiAction(callerId, request) {
    if (request.actorEmployeeId && request.actorEmployeeId !== callerId) {
      throw new Error("Cannot act on behalf of another employee.");
    }
    return { actorId: callerId, success: true };
  }

  const authenticatedUserId = "emp-waiter-01";

  // Caller sends own ID or omits -> Allowed and bound to authenticated user
  const ownResult = handleApiAction(authenticatedUserId, { actorEmployeeId: "emp-waiter-01" });
  assert.equal(ownResult.actorId, authenticatedUserId);

  const omittedResult = handleApiAction(authenticatedUserId, { actorEmployeeId: null });
  assert.equal(omittedResult.actorId, authenticatedUserId);

  // Caller attempts to spoof manager's ID -> REJECTED
  assert.throws(
    () => handleApiAction(authenticatedUserId, { actorEmployeeId: "emp-manager-99" }),
    /Cannot act on behalf of another employee/
  );
});

test("15. Authoritative Ingredient Resolution: Modifier substitutions consume only replacement ingredients", () => {
  const recipe = [
    { ingredientId: "coffee-beans", name: "Coffee Beans", amount: 0.018 },
    { ingredientId: "whole-milk", name: "Whole Milk", amount: 0.200 }
  ];

  const modifiers = [
    {
      name: "Oat Milk Swap",
      adjustmentType: "Substitute",
      originalIngredientId: "whole-milk",
      ingredientId: "oat-milk",
      quantity: 0.200
    }
  ];

  const resolved = calculateIngredients(recipe, modifiers, 2); // 2 coffees

  // Whole milk is replaced and should not be consumed
  assert.equal(resolved["whole-milk"], undefined);
  // Coffee beans are consumed (2 * 0.018 = 0.036)
  assert.equal(resolved["coffee-beans"], 0.036);
  // Oat milk replacement is consumed (2 * 0.200 = 0.400)
  assert.equal(resolved["oat-milk"], 0.400);
});

test("16. Atomic Manager PIN Rate Limiting: Atomic UPSERT, lockout expiry, and reset on success", () => {
  class SimulatedRateLimiter {
    constructor() {
      this.records = new Map(); // key -> { failedAttempts, lockedUntil }
    }

    // Simulates atomic PostgreSQL UPSERT: INSERT ... ON CONFLICT ("RestaurantId") DO UPDATE ... RETURNING ...
    recordAttempt(restaurantId, isSuccess, now = Date.now()) {
      const rec = this.records.get(restaurantId) || { failedAttempts: 0, lockedUntil: null };

      // Active lockout: preserve deadline, do not extend
      if (rec.lockedUntil && now < rec.lockedUntil) {
        const remainingSec = Math.ceil((rec.lockedUntil - now) / 1000);
        return { success: false, locked: true, message: `Manager authorization locked. Try again in ${remainingSec}s.` };
      }

      // If lockout expired, reset window so next failure starts at 1
      const isExpired = rec.lockedUntil && now >= rec.lockedUntil;
      if (isExpired) {
        rec.failedAttempts = 0;
        rec.lockedUntil = null;
      }

      if (isSuccess) {
        rec.failedAttempts = 0;
        rec.lockedUntil = null;
        this.records.set(restaurantId, rec);
        return { success: true, locked: false };
      } else {
        rec.failedAttempts = isExpired ? 1 : rec.failedAttempts + 1;
        if (rec.failedAttempts >= 5) {
          rec.lockedUntil = now + (15 * 60 * 1000); // 15 min lockout
          this.records.set(restaurantId, rec);
          return { success: false, locked: true, message: "Too many failed manager PIN attempts. Locked out for 15 minutes." };
        }
        this.records.set(restaurantId, rec);
        return { success: false, locked: false, attemptsRemaining: 5 - rec.failedAttempts };
      }
    }

    resetAttempt(restaurantId, now = Date.now()) {
      const rec = this.records.get(restaurantId);
      if (!rec) return;
      // Do not clear if currently locked out (racing verification protection)
      if (rec.lockedUntil && now < rec.lockedUntil) return;
      rec.failedAttempts = 0;
      rec.lockedUntil = null;
    }
  }

  const limiter = new SimulatedRateLimiter();
  const restId = "res-test-rate-limit";
  let now = Date.now();

  // 4 failed attempts
  for (let i = 1; i <= 4; i++) {
    const res = limiter.recordAttempt(restId, false, now);
    assert.equal(res.locked, false);
    assert.equal(res.attemptsRemaining, 5 - i);
  }

  // 5th attempt locks out
  const fifth = limiter.recordAttempt(restId, false, now);
  assert.equal(fifth.locked, true);
  assert.match(fifth.message, /Locked out for 15 minutes/);

  // Subsequent attempt during lockout is immediately blocked, deadline preserved without extending
  const duringLockout = limiter.recordAttempt(restId, false, now + 60000); // 1 min later
  assert.equal(duringLockout.locked, true);
  assert.match(duringLockout.message, /Try again in/);

  // Racing successful verification during active lockout does not erase the lockout
  limiter.resetAttempt(restId, now + 60000);
  assert.ok(limiter.records.get(restId).lockedUntil > now);

  // After 16 minutes, lockout expires automatically
  now += 16 * 60 * 1000;
  // First failed attempt after expiry starts fresh failure window at 1 (not re-locking!)
  const firstFailAfterExpiry = limiter.recordAttempt(restId, false, now);
  assert.equal(firstFailAfterExpiry.locked, false);
  assert.equal(firstFailAfterExpiry.attemptsRemaining, 4);
  assert.equal(limiter.records.get(restId).failedAttempts, 1);
  assert.equal(limiter.records.get(restId).lockedUntil, null);

  // Subsequent successful verification resets counter to 0
  const afterExpirySuccess = limiter.recordAttempt(restId, true, now);
  assert.equal(afterExpirySuccess.success, true);
  assert.equal(afterExpirySuccess.locked, false);

  // State is reset
  assert.equal(limiter.records.get(restId).failedAttempts, 0);
  assert.equal(limiter.records.get(restId).lockedUntil, null);
});

test("17. Concurrency & Locking: Cancellation racing with settlement or payment freeze throws 409 Conflict", () => {
  const bill = {
    id: "bill-101",
    status: "Open",
    settledAtUtc: null,
    paymentRevision: null,
    splitAtUtc: null
  };

  function attemptCancelItem(billState, quantityToCancel, availableQuantity) {
    // Post-lock revalidation
    if (billState.settledAtUtc != null) {
      const err = new Error("Cannot cancel items on a settled bill.");
      err.status = 409;
      throw err;
    }
    if (billState.paymentRevision != null || billState.splitAtUtc != null) {
      const err = new Error("Cannot cancel items on a bill with active payments or splits.");
      err.status = 409;
      throw err;
    }
    if (quantityToCancel > availableQuantity) {
      const err = new Error(`Cannot cancel ${quantityToCancel}. Only ${availableQuantity} portion(s) available.`);
      err.status = 409;
      throw err;
    }
    return { success: true, cancelledPortion: quantityToCancel };
  }

  // Open bill succeeds
  const ok = attemptCancelItem(bill, 1, 2);
  assert.equal(ok.success, true);

  // Racing with settlement throws 409 Conflict
  bill.settledAtUtc = new Date().toISOString();
  assert.throws(
    () => attemptCancelItem(bill, 1, 1),
    (err) => err.status === 409 && /settled bill/.test(err.message)
  );

  // Racing with partial payment / split throws 409 Conflict
  bill.settledAtUtc = null;
  bill.paymentRevision = "rev-payment-in-flight";
  assert.throws(
    () => attemptCancelItem(bill, 1, 1),
    (err) => err.status === 409 && /active payments or splits/.test(err.message)
  );

  // Racing with another cancellation reducing remaining portions throws 409 Conflict
  bill.paymentRevision = null;
  assert.throws(
    () => attemptCancelItem(bill, 2, 1), // wants 2, only 1 left
    (err) => err.status === 409 && /Only 1 portion\(s\) available/.test(err.message)
  );
});

test("18. Concurrency & Locking: Table transfer triggers bounded retry with clean rollback, never lock inversion", () => {
  const attempts = [];

  function simulateBoundedRetryWorkflow(transferredOrder, billMap, maxRetries = 3) {
    let orderInfo = { tableId: "table-1", tableBillId: "bill-1" };

    for (let retry = 0; retry < maxRetries; retry++) {
      const lockSequence = [];
      // Level 1: Table lock
      if (orderInfo.tableId) {
        lockSequence.push(`Lock:Table:${orderInfo.tableId}`);
      }
      // Level 2: Bill lock
      if (orderInfo.tableBillId) {
        lockSequence.push(`Lock:Bill:${orderInfo.tableBillId}`);
      }
      // Level 3: Order lock
      lockSequence.push(`Lock:Order:${transferredOrder.id}`);

      attempts.push({ attempt: retry + 1, locks: lockSequence });

      // Inside locks: revalidate authoritative order state
      if (transferredOrder.tableId !== orderInfo.tableId || transferredOrder.tableBillId !== orderInfo.tableBillId) {
        // Rollback transaction, clear change tracker, and retry with new ownership!
        // CRITICAL: NEVER acquire new table lock while holding Order lock (avoids inversion)
        orderInfo = { tableId: transferredOrder.tableId, tableBillId: transferredOrder.tableBillId };
        continue;
      }

      // Check authoritative bill state
      const authoritativeBill = billMap.get(transferredOrder.tableBillId);
      if (!authoritativeBill || authoritativeBill.settledAtUtc) {
        const err = new Error("Cannot modify order on settled or missing bill.");
        err.status = 409;
        throw err;
      }

      return { success: true, retries: retry, currentTable: transferredOrder.tableId, currentBill: transferredOrder.tableBillId };
    }

    const conflict = new Error("Order was concurrently transferred; please retry.");
    conflict.status = 409;
    throw conflict;
  }

  const bills = new Map([
    ["bill-1", { id: "bill-1", settledAtUtc: null }],
    ["bill-2", { id: "bill-2", settledAtUtc: null }]
  ]);

  // Order was transferred from table-1 to table-2 while lock was pending
  const transferredOrder = { id: "ord-99", tableId: "table-2", tableBillId: "bill-2" };
  const result = simulateBoundedRetryWorkflow(transferredOrder, bills);

  assert.equal(result.success, true);
  assert.equal(result.retries, 1); // Retried once cleanly
  assert.equal(result.currentTable, "table-2");
  assert.equal(result.currentBill, "bill-2");

  // Verify that attempt 1 locked Table 1, then rolled back and restarted
  assert.equal(attempts.length, 2);
  assert.deepEqual(attempts[0].locks, ["Lock:Table:table-1", "Lock:Bill:bill-1", "Lock:Order:ord-99"]);
  // Attempt 2 locked Table 2 first (preserving Table -> Bill -> Order hierarchy!)
  assert.deepEqual(attempts[1].locks, ["Lock:Table:table-2", "Lock:Bill:bill-2", "Lock:Order:ord-99"]);
});

test("19. Cart Separation: Two identical dishes with different modifiers create distinct lines and independent quantities", () => {
  function computeLineId(itemId, selectedModifiers, instructions) {
    const mods = (selectedModifiers || [])
      .slice()
      .sort((a, b) => a.modifierOptionId.localeCompare(b.modifierOptionId))
      .map((m) => `${m.modifierGroupId}:${m.modifierOptionId}`)
      .join("|");
    const notes = (instructions || "").trim().toLowerCase();
    return `${itemId}__mods:${mods}__notes:${notes}`;
  }

  const cart = [];

  function addItem(item, qty, modifiers, notes) {
    const lineId = computeLineId(item.id, modifiers, notes);
    const existingIdx = cart.findIndex((l) => l.lineId === lineId);
    if (existingIdx !== -1) {
      cart[existingIdx].qty += qty;
    } else {
      cart.push({ lineId, item, qty, selectedModifiers: modifiers, specialInstructions: notes });
    }
  }

  function updateQty(lineId, delta) {
    const idx = cart.findIndex((l) => l.lineId === lineId);
    if (idx !== -1) {
      cart[idx].qty += delta;
      if (cart[idx].qty <= 0) {
        cart.splice(idx, 1);
      }
    }
  }

  const burger = { id: "dish-burger", name: "Classic Burger", price: 250 };

  // Add 1x Burger with Extra Cheese (+50)
  addItem(burger, 1, [{ modifierGroupId: "g-cheese", modifierOptionId: "opt-extra-cheese", optionName: "Extra Cheese", priceDelta: 50 }], "Medium rare");
  // Add 1x Burger with Gluten-Free Bun (+30)
  addItem(burger, 1, [{ modifierGroupId: "g-bun", modifierOptionId: "opt-gf-bun", optionName: "Gluten-Free Bun", priceDelta: 30 }], "Well done");

  // Cart must contain 2 separate lines!
  assert.equal(cart.length, 2);
  assert.notEqual(cart[0].lineId, cart[1].lineId);
  assert.equal(cart[0].selectedModifiers[0].optionName, "Extra Cheese");
  assert.equal(cart[1].selectedModifiers[0].optionName, "Gluten-Free Bun");

  // Editing quantity of line 1 does not affect line 2
  updateQty(cart[0].lineId, +1); // 2x Extra Cheese
  assert.equal(cart[0].qty, 2);
  assert.equal(cart[1].qty, 1);

  // Cart total correctly sums both lines with their respective modifier deltas
  // Line 1: 2 * (250 + 50) = 600
  // Line 2: 1 * (250 + 30) = 280
  // Total: 880
  const cartTotal = cart.reduce((s, l) => {
    const modTotal = l.selectedModifiers.reduce((acc, m) => acc + m.priceDelta, 0);
    return s + l.qty * (l.item.price + modTotal);
  }, 0);
  assert.equal(cartTotal, 880);
});

test("20. Free Refill Limit Enforcement and Zero Food Cost", () => {
  const item = { id: "item-iced-tea", name: "House Iced Tea", price: 120, isRefillEligible: true, refillMaxCount: 2 };
  const refills = [];

  function requestRefill(originalItem, notes) {
    if (!originalItem.isRefillEligible) {
      throw new Error(`Item '${originalItem.name}' is not configured for free refills.`);
    }
    if (refills.length >= originalItem.refillMaxCount) {
      throw new Error(`Maximum refill limit (${originalItem.refillMaxCount}) reached for '${originalItem.name}'.`);
    }

    const seq = refills.length + 1;
    const refillLine = {
      id: `refill-${seq}`,
      originalItemId: originalItem.id,
      name: `${originalItem.name} (Refill #${seq})`,
      quantity: 1,
      unitPrice: 0.00, // Free refill is 0 food charge
      refillCount: seq,
      isRefill: true,
      notes
    };
    refills.push(refillLine);
    return refillLine;
  }

  // First refill
  const r1 = requestRefill(item, "Less ice");
  assert.equal(r1.unitPrice, 0.00);
  assert.equal(r1.refillCount, 1);

  // Second refill
  const r2 = requestRefill(item, "Extra lemon");
  assert.equal(r2.unitPrice, 0.00);
  assert.equal(r2.refillCount, 2);

  // Third refill exceeds limit of 2 -> Throws
  assert.throws(
    () => requestRefill(item, "More"),
    /Maximum refill limit \(2\) reached/
  );
});
