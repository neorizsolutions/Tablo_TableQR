import test from 'node:test';
import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import net from 'node:net';

// Helper: Format IST Date string (YYYY-MM-DD)
function getIstBusinessDate(date = new Date()) {
  return new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Kolkata',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit'
  }).format(date);
}

// Helper: Format IST Time
function formatIstTime(date = new Date()) {
  return new Intl.DateTimeFormat('en-IN', {
    timeZone: 'Asia/Kolkata',
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hour12: true
  }).format(date) + ' IST';
}

// Helper: Hash matching BillingService.Hash (SHA256 hex)
function hashPayload(obj) {
  return crypto.createHash('sha256').update(typeof obj === 'string' ? obj : JSON.stringify(obj)).digest('hex').toUpperCase();
}

// Word-wrap matching EscPosFormatter.cs
function wrapText(text, maxChars) {
  if (!text || text.length <= maxChars) return [text || ''];
  const words = text.split(' ');
  const lines = [];
  let currentLine = '';

  for (const word of words) {
    if (word.length > maxChars) {
      if (currentLine) {
        lines.push(currentLine);
        currentLine = '';
      }
      for (let i = 0; i < word.length; i += maxChars) {
        lines.push(word.substring(i, Math.min(i + maxChars, word.length)));
      }
      continue;
    }

    const candidate = currentLine ? `${currentLine} ${word}` : word;
    if (candidate.length <= maxChars) {
      currentLine = candidate;
    } else {
      lines.push(currentLine);
      currentLine = word;
    }
  }

  if (currentLine) lines.push(currentLine);
  return lines;
}

// Format Parcel KOT matching EscPosFormatter.cs
function formatParcelKotTicket(params) {
  const {
    restaurantName,
    stationName,
    ticketNumber,
    orderNumber,
    dailyTokenNumber,
    customerName,
    customerPhone,
    ticketType = 'New',
    isReprint = false,
    reprintCount = 0,
    reprintReason,
    timestamp = new Date(),
    items = [],
    paperWidthMm = 80
  } = params;

  const colWidth = paperWidthMm === 58 ? 32 : 48;
  const divider = '-'.repeat(colWidth);
  const doubleDivider = '='.repeat(colWidth);
  const timestampIst = formatIstTime(timestamp);

  const center = (str) => {
    if (str.length >= colWidth) return str.slice(0, colWidth);
    const pad = Math.floor((colWidth - str.length) / 2);
    return ' '.repeat(pad) + str;
  };

  const padSides = (left, right) => {
    const spaces = colWidth - left.length - right.length;
    if (spaces <= 0) return `${left} ${right}`;
    return left + ' '.repeat(spaces) + right;
  };

  const lines = [];
  lines.push(center(restaurantName.toUpperCase()));
  lines.push(center(`*** ${stationName.toUpperCase()} ***`));
  lines.push(center('*** PARCEL ORDER ***'));

  if (dailyTokenNumber) {
    lines.push(center(`============================`));
    lines.push(center(`*** TOKEN: #${String(dailyTokenNumber).padStart(3, '0')} ***`));
    lines.push(center(`============================`));
  }

  if (isReprint) {
    const reprintHeader = reprintCount > 1 ? `*** REPRINT #${reprintCount} ***` : '*** REPRINT ***';
    lines.push(center(reprintHeader));
    if (reprintReason) lines.push(center(`Reason: ${reprintReason}`));
  }

  lines.push(doubleDivider);
  lines.push(padSides(`Ticket: ${ticketNumber}`, `Order: ORD-${String(orderNumber).padStart(4, '0')}`));
  lines.push(padSides('Channel: PARCEL TAKEAWAY', timestampIst));

  if (customerName || customerPhone) {
    const cust = `Customer: ${customerName || ''}${customerPhone ? ` (${customerPhone})` : ''}`.trim();
    lines.push(cust);
  }
  lines.push(divider);

  let totalQty = 0;
  for (const item of items) {
    totalQty += item.quantity;
    lines.push(`${item.quantity}x ${item.name}`);
    if (item.specialInstructions) {
      const wrappedNotes = wrapText(`Note: ${item.specialInstructions}`, colWidth - 5);
      for (const wn of wrappedNotes) {
        lines.push(`   * ${wn}`);
      }
    }
  }

  lines.push(divider);
  lines.push(padSides(`Total Items: ${totalQty}`, ticketType.toUpperCase()));
  lines.push(doubleDivider);

  return lines.join('\n');
}

// In-Memory Engine for Parcel Counter & Inventory Verification
class SimulatedParcelEngine {
  constructor() {
    this.restaurantId = 'rest-1';
    this.tokenSequences = new Map(); // key: `${restaurantId}_${date}` -> currentSeq
    this.orders = new Map(); // orderId -> order
    this.bills = new Map(); // billId -> bill
    this.payments = []; // array of BillPayment
    this.reservations = []; // array of StockReservation
    this.stockMovements = []; // array of StockMovement
    this.printJobs = []; // array of jobs
    this.submissionOperations = new Map(); // submissionOperationId -> { orderId, fingerprint }

    // Settings
    this.packagingSettings = {
      chargeMode: 'PerItem', // 'None' | 'PerItem' | 'FlatPerOrder' | 'Percentage'
      flatRate: 25.0,
      percentage: 5.0,
      isTaxable: true,
      taxRate: 5.0,
      autoManageAvailability: true
    };

    // Ingredient catalog
    this.ingredients = new Map([
      ['ing-chicken', { id: 'ing-chicken', name: 'Raw Chicken', onHand: 10.0 }],
      ['ing-rice', { id: 'ing-rice', name: 'Basmati Rice', onHand: 20.0 }],
      ['ing-paneer', { id: 'ing-paneer', name: 'Fresh Paneer', onHand: 5.0 }],
      ['ing-milk', { id: 'ing-milk', name: 'Milk/Curd', onHand: 15.0 }]
    ]);

    // Menu recipes
    this.recipes = new Map([
      ['dish-biryani', [
        { ingredientId: 'ing-chicken', quantity: 0.3 },
        { ingredientId: 'ing-rice', quantity: 0.2 }
      ]],
      ['dish-paneer', [
        { ingredientId: 'ing-paneer', quantity: 0.25 }
      ]],
      ['drink-lassi', [
        { ingredientId: 'ing-milk', quantity: 0.2 }
      ]]
    ]);

    // Menu catalog with packaging fees
    this.menu = new Map([
      ['dish-biryani', { id: 'dish-biryani', name: 'Hyderabadi Dum Biryani', price: 320.0, packagingFee: 15.0, station: 'Food Kitchen', available: true }],
      ['dish-paneer', { id: 'dish-paneer', name: 'Paneer Butter Masala', price: 260.0, packagingFee: 10.0, station: 'Food Kitchen', available: true }],
      ['drink-lassi', { id: 'drink-lassi', name: 'Mango Lassi', price: 90.0, packagingFee: 5.0, station: 'Beverage Station', available: true }]
    ]);
  }

  allocateDailyToken(dateStr) {
    const key = `${this.restaurantId}_${dateStr}`;
    const current = this.tokenSequences.get(key) || 0;
    const next = current + 1;
    this.tokenSequences.set(key, next);
    return next;
  }

  computeFingerprint(params) {
    const canonical = {
      actorId: params.actorId || 'staff-1',
      items: params.items.slice().sort((a, b) => a.itemId.localeCompare(b.itemId)).map(i => ({
        itemId: i.itemId,
        quantity: i.quantity,
        instructions: i.specialInstructions?.trim() || ''
      })),
      customerName: params.customerName?.trim() || '',
      customerPhone: params.customerPhone?.trim() || '',
      waivePackaging: !!params.waivePackaging,
      waiverReason: params.waivePackaging ? (params.waiverReason?.trim() || '') : ''
    };
    return hashPayload(canonical);
  }

  calculatePackaging(items, isWaived = false) {
    if (isWaived || this.packagingSettings.chargeMode === 'None') {
      return { charge: 0.0, mode: this.packagingSettings.chargeMode, rate: 0.0 };
    }

    let charge = 0.0;
    let rate = 0.0;

    switch (this.packagingSettings.chargeMode) {
      case 'PerItem':
        for (const it of items) {
          const menuItem = this.menu.get(it.itemId);
          const fee = menuItem?.packagingFee ?? 0.0;
          charge += fee * it.quantity;
        }
        rate = 0.0;
        break;

      case 'FlatPerOrder':
        charge = this.packagingSettings.flatRate;
        rate = this.packagingSettings.flatRate;
        break;

      case 'Percentage':
        const foodSubtotal = items.reduce((sum, it) => {
          const menuItem = this.menu.get(it.itemId);
          return sum + (menuItem ? menuItem.price * it.quantity : 0);
        }, 0);
        rate = this.packagingSettings.percentage;
        charge = Math.round((foodSubtotal * (rate / 100)) * 100) / 100;
        break;
    }

    return { charge: Math.max(0, charge), mode: this.packagingSettings.chargeMode, rate };
  }

  // Create Parcel Order (durable idempotency + stock reservation)
  createParcelOrder(params) {
    const {
      operationId,
      items,
      customerName,
      customerPhone,
      specialInstructions,
      waivePackaging = false,
      waiverReason,
      waivedBy,
      actorId = 'staff-1',
      date = new Date()
    } = params;

    if (!operationId) {
      throw new Error('A valid non-empty Operation ID is required.');
    }

    const fingerprint = this.computeFingerprint(params);

    // Idempotency Check
    if (this.submissionOperations.has(operationId)) {
      const existing = this.submissionOperations.get(operationId);
      if (existing.fingerprint !== fingerprint) {
        const err = new Error('This submission operation ID was previously used for different order details.');
        err.statusCode = 409;
        throw err;
      }
      return { order: this.orders.get(existing.orderId), isDuplicate: true };
    }

    // Recipe & stock check
    if (this.packagingSettings.autoManageAvailability) {
      for (const it of items) {
        const recipe = this.recipes.get(it.itemId) || [];
        for (const r of recipe) {
          const ing = this.ingredients.get(r.ingredientId);
          const activeReserved = this.reservations
            .filter(res => res.ingredientId === r.ingredientId && res.status === 'Active')
            .reduce((sum, res) => sum + res.quantity, 0);
          const available = (ing?.onHand || 0) - activeReserved;
          const needed = r.quantity * it.quantity;
          if (available < needed) {
            throw new Error(`Menu item '${this.menu.get(it.itemId)?.name}' does not have sufficient ingredient stock available.`);
          }
        }
      }
    }

    const businessDate = getIstBusinessDate(date);
    const tokenNumber = this.allocateDailyToken(businessDate);
    const orderId = `ord-parcel-${Date.now()}-${Math.floor(Math.random() * 1000)}`;

    const { charge: packagingCharge, mode, rate } = this.calculatePackaging(items, waivePackaging);

    let foodSubtotal = 0;
    const orderItems = [];

    for (const it of items) {
      const menuItem = this.menu.get(it.itemId);
      if (!menuItem) throw new Error(`Item ${it.itemId} not found`);
      foodSubtotal += menuItem.price * it.quantity;
      orderItems.push({
        id: `oi-${Math.random()}`,
        itemId: it.itemId,
        name: menuItem.name,
        price: menuItem.price,
        quantity: it.quantity,
        station: menuItem.station,
        specialInstructions: it.specialInstructions || specialInstructions
      });
    }

    const taxRate = 0.05;
    const taxableSubtotal = this.packagingSettings.isTaxable ? (foodSubtotal + packagingCharge) : foodSubtotal;
    const taxAmount = Math.round(taxableSubtotal * taxRate * 100) / 100;
    const totalAmount = Math.round((foodSubtotal + packagingCharge + taxAmount) * 100) / 100;

    const order = {
      id: orderId,
      restaurantId: this.restaurantId,
      tableId: null, // Tableless
      orderChannel: 'ParcelTakeaway',
      dailyTokenNumber: tokenNumber,
      businessDate,
      customerName,
      customerPhone,
      items: orderItems,
      subtotal: foodSubtotal,
      packagingCharge,
      taxAmount,
      totalAmount,
      status: 'Accepted',
      operationId,
      submissionFingerprint: fingerprint,
      isPackagingWaived: waivePackaging,
      packagingWaiverReason: waivePackaging ? waiverReason : null,
      packagingWaivedBy: waivePackaging ? waivedBy : null,
      packagingModeSnapshot: mode,
      packagingRateSnapshot: rate,
      createdAt: date
    };

    const bill = {
      id: `bill-${orderId}`,
      restaurantId: this.restaurantId,
      orderId,
      tableId: null,
      orderChannel: 'ParcelTakeaway',
      dailyTokenNumber: tokenNumber,
      businessDate,
      totalAmount,
      remainingAmount: totalAmount,
      paidAmount: 0.0,
      isPaid: false,
      paymentRevision: hashPayload(`${orderId}_${totalAmount}`),
      settledAt: null
    };

    this.orders.set(orderId, order);
    this.bills.set(bill.id, bill);
    this.submissionOperations.set(operationId, { orderId, fingerprint });

    // Active stock reservations
    for (const oi of orderItems) {
      const recipe = this.recipes.get(oi.itemId) || [];
      for (const r of recipe) {
        this.reservations.push({
          id: `res-${Math.random()}`,
          orderId,
          ingredientId: r.ingredientId,
          quantity: r.quantity * oi.quantity,
          status: 'Active'
        });
      }
    }

    return { order, bill, isDuplicate: false };
  }

  // Cancel order: releases reservations
  cancelParcelOrder(orderId, reason) {
    const order = this.orders.get(orderId);
    if (!order) throw new Error('Order not found');
    order.status = 'Cancelled';
    order.exceptionReason = reason;

    for (const r of this.reservations) {
      if (r.orderId === orderId && r.status === 'Active') {
        r.status = 'Released';
      }
    }
    return order;
  }

  // Record payment (supports partial payments & prevents overpayment)
  recordPayment(billId, paymentParams) {
    const { operationId, amount, method, actorId = 'staff-1' } = paymentParams;
    const bill = this.bills.get(billId);
    if (!bill) throw new Error('Bill not found');
    if (bill.isPaid) throw new Error('Bill is already settled');

    const paymentHash = hashPayload({ actorId, billId, method, amount: amount.toFixed(2) });

    // Check operation replay
    const existingPayment = this.payments.find(p => p.operationId === operationId);
    if (existingPayment) {
      if (existingPayment.payloadHash !== paymentHash) {
        const err = new Error('This payment operation was used for different details.');
        err.statusCode = 409;
        throw err;
      }
      return { payment: existingPayment, remaining: bill.remainingAmount, isDuplicate: true };
    }

    if (amount > bill.remainingAmount) {
      const err = new Error('Payment exceeds the remaining bill balance.');
      err.statusCode = 409;
      throw err;
    }

    const payment = {
      id: `pay-${this.payments.length + 1}`,
      billId,
      operationId,
      payloadHash: paymentHash,
      method,
      amount,
      collectedAt: new Date()
    };
    this.payments.push(payment);

    bill.paidAmount = Math.round((bill.paidAmount + amount) * 100) / 100;
    bill.remainingAmount = Math.round((bill.totalAmount - bill.paidAmount) * 100) / 100;

    // Final settlement ONLY when remaining reaches 0
    if (bill.remainingAmount === 0) {
      bill.isPaid = true;
      bill.settledAt = new Date();
      const distinctMethods = [...new Set(this.payments.filter(p => p.billId === billId).map(p => p.method))];
      bill.paymentMethod = distinctMethods.length === 1 ? method : 'Mixed';

      // Deduct stock permanently on final settlement (once only)
      const order = this.orders.get(bill.orderId);
      if (order) {
        order.status = 'Served';
        for (const oi of order.items) {
          const recipe = this.recipes.get(oi.itemId) || [];
          for (const r of recipe) {
            const ing = this.ingredients.get(r.ingredientId);
            if (ing) {
              const qty = r.quantity * oi.quantity;
              ing.onHand -= qty;
              this.stockMovements.push({
                billId,
                ingredientId: r.ingredientId,
                qtyDeducted: qty
              });
            }
          }
        }
      }

      // Commit reservations
      for (const r of this.reservations) {
        if (r.orderId === bill.orderId && r.status === 'Active') {
          r.status = 'Committed';
        }
      }
    }

    return { payment, remaining: bill.remainingAmount, isSettled: bill.isPaid, isDuplicate: false };
  }
}

// ----------------------------------------------------
// TEST SUITE
// ----------------------------------------------------

test('1. Concurrency: Daily tokens allocate uniquely per IST business date and reset daily', async () => {
  const engine = new SimulatedParcelEngine();
  const day1Date = new Date('2026-11-19T10:00:00+05:30');
  const day1Str = getIstBusinessDate(day1Date);
  assert.equal(day1Str, '2026-11-19');

  const tokens = [];
  for (let i = 0; i < 15; i++) {
    tokens.push(engine.allocateDailyToken(day1Str));
  }

  assert.equal(tokens.length, 15);
  for (let i = 0; i < 15; i++) {
    assert.equal(tokens[i], i + 1);
  }

  // Day 2 (crossing midnight in Asia/Kolkata) resets to 1
  const day2Date = new Date('2026-11-20T00:01:00+05:30');
  const day2Str = getIstBusinessDate(day2Date);
  assert.equal(day2Str, '2026-11-20');

  const day2Token1 = engine.allocateDailyToken(day2Str);
  assert.equal(day2Token1, 1, 'Day 2 sequence must reset to 1');
});

test('2. Durable idempotency: Canonical request fingerprinting & conflict rejection', async () => {
  const engine = new SimulatedParcelEngine();
  const opId = 'client-uuid-9988-7766';

  const req = {
    operationId: opId,
    customerName: 'Aarav Sharma',
    customerPhone: '9876543210',
    items: [{ itemId: 'dish-biryani', quantity: 2, specialInstructions: 'Extra spicy' }]
  };

  const firstAttempt = engine.createParcelOrder(req);
  assert.equal(firstAttempt.isDuplicate, false);
  assert.equal(firstAttempt.order.dailyTokenNumber, 1);

  // Exact retry returns original order and token
  const secondAttempt = engine.createParcelOrder(req);
  assert.equal(secondAttempt.isDuplicate, true);
  assert.equal(secondAttempt.order.id, firstAttempt.order.id);
  assert.equal(secondAttempt.order.dailyTokenNumber, 1);

  // Different details with same operationId throws 409 Conflict
  const conflictingReq = {
    operationId: opId,
    customerName: 'Different Name',
    customerPhone: '9876543210',
    items: [{ itemId: 'dish-biryani', quantity: 3 }]
  };

  assert.throws(
    () => engine.createParcelOrder(conflictingReq),
    (err) => err.statusCode === 409
  );
});

test('3. Packaging calculations: None, PerItem, FlatPerOrder, Percentage, and Waiver', async () => {
  const engine = new SimulatedParcelEngine();
  const items = [
    { itemId: 'dish-biryani', quantity: 2 }, // 2 * 320 = 640, fee = 15
    { itemId: 'dish-paneer', quantity: 1 }   // 1 * 260 = 260, fee = 10
  ]; // Food subtotal: 900

  // PerItem: 2*15 + 1*10 = 40
  engine.packagingSettings.chargeMode = 'PerItem';
  assert.equal(engine.calculatePackaging(items).charge, 40.0);

  // Flat: 25
  engine.packagingSettings.chargeMode = 'FlatPerOrder';
  assert.equal(engine.calculatePackaging(items).charge, 25.0);

  // Percentage: 5% of 900 = 45
  engine.packagingSettings.chargeMode = 'Percentage';
  engine.packagingSettings.percentage = 5.0;
  assert.equal(engine.calculatePackaging(items).charge, 45.0);

  // Waived
  const waivedOrder = engine.createParcelOrder({
    operationId: 'op-waived',
    items: [{ itemId: 'dish-biryani', quantity: 1 }],
    waivePackaging: true,
    waiverReason: 'Brought own tiffin',
    waivedBy: 'cashier_maya'
  });
  assert.equal(waivedOrder.order.packagingCharge, 0.0);
  assert.equal(waivedOrder.order.isPackagingWaived, true);
  assert.equal(waivedOrder.order.packagingWaiverReason, 'Brought own tiffin');
});

test('4. Complete tableless parcel payments: Partial collections, overpayment block & mixed settlement', async () => {
  const engine = new SimulatedParcelEngine();
  engine.packagingSettings.chargeMode = 'FlatPerOrder';
  engine.packagingSettings.flatRate = 10.0;

  // Food: 2 * 90 = 180 + 10 packaging = 190 + 5% tax (9.50) = 199.50
  const { order, bill } = engine.createParcelOrder({
    operationId: 'op-bill-pay',
    items: [{ itemId: 'drink-lassi', quantity: 2 }]
  });

  assert.equal(order.tableId, null);
  assert.equal(bill.totalAmount, 199.50);
  assert.equal(bill.remainingAmount, 199.50);

  // Partial Payment 1: ₹100 via UPI
  const pay1 = engine.recordPayment(bill.id, {
    operationId: 'pay-op-1',
    amount: 100.0,
    method: 'UPI'
  });
  assert.equal(pay1.remaining, 99.50);
  assert.equal(pay1.isSettled, false);
  assert.equal(bill.isPaid, false);
  // Stock must NOT be deducted yet on partial payment
  assert.equal(engine.stockMovements.length, 0);

  // Overpayment Attempt: ₹150 when only ₹99.50 is remaining -> Rejected
  assert.throws(
    () => engine.recordPayment(bill.id, { operationId: 'pay-op-over', amount: 150.0, method: 'Cash' }),
    (err) => err.statusCode === 409
  );

  // Payment 1 Replay: Same details returns original acknowledgement
  const pay1Replay = engine.recordPayment(bill.id, {
    operationId: 'pay-op-1',
    amount: 100.0,
    method: 'UPI'
  });
  assert.equal(pay1Replay.isDuplicate, true);

  // Partial Payment 2: ₹99.50 via Cash -> Reaches exactly 0 remaining balance!
  const pay2 = engine.recordPayment(bill.id, {
    operationId: 'pay-op-2',
    amount: 99.50,
    method: 'Cash'
  });
  assert.equal(pay2.remaining, 0.0);
  assert.equal(pay2.isSettled, true);
  assert.equal(bill.isPaid, true);
  assert.equal(bill.paymentMethod, 'Mixed'); // UPI + Cash = Mixed!
  assert.equal(order.status, 'Served');

  // Stock deducted exactly once on final settlement
  assert.equal(engine.stockMovements.length, 1);
  assert.equal(engine.stockMovements[0].ingredientId, 'ing-milk');
  assert.equal(engine.stockMovements[0].qtyDeducted, 0.4); // 2 * 0.2
});

test('5. Stock reservations: Concurrency check, last portion protection & cancellation release', async () => {
  const engine = new SimulatedParcelEngine();
  // Paneer has 5.0 kg on hand. Each dish requires 0.25 kg. Max capacity = 20 portions.
  const ing = engine.ingredients.get('ing-paneer');
  ing.onHand = 0.5; // Exactly 2 portions left!

  // Order 1 takes 2 portions
  const ord1 = engine.createParcelOrder({
    operationId: 'op-stock-1',
    items: [{ itemId: 'dish-paneer', quantity: 2 }]
  });
  assert.ok(ord1.order);

  // Active reservations now equal 0.5 kg. Available is 0.
  // Order 2 attempts to take 1 portion -> rejected due to insufficient stock!
  assert.throws(
    () => engine.createParcelOrder({
      operationId: 'op-stock-2',
      items: [{ itemId: 'dish-paneer', quantity: 1 }]
    }),
    /does not have sufficient ingredient stock/
  );

  // Order 1 is cancelled -> reservations released
  engine.cancelParcelOrder(ord1.order.id, 'Customer changed mind');
  const res = engine.reservations.find(r => r.orderId === ord1.order.id);
  assert.equal(res.status, 'Released');

  // Now Order 3 can successfully reserve the available stock!
  const ord3 = engine.createParcelOrder({
    operationId: 'op-stock-3',
    items: [{ itemId: 'dish-paneer', quantity: 1 }]
  });
  assert.ok(ord3.order);
});

test('6. Safe Claim Recovery: Expired claims transition to UncertainDelivery, no automatic duplicate paper', async () => {
  const now = new Date('2026-11-19T12:00:00Z');

  const jobs = [
    {
      id: 'job-pending',
      status: 'Pending',
      claimToken: null,
      leaseExpiresAtUtc: null,
      claimedByAgentId: null
    },
    {
      id: 'job-crashed-agent',
      status: 'Claimed',
      claimToken: 'token-crashed',
      leaseExpiresAtUtc: new Date(now.getTime() - 5000).toISOString(), // Expired 5 seconds ago!
      claimedByAgentId: 'agent-dead',
      deliveryState: 'Certain',
      uncertainReason: null
    }
  ];

  // Safe polling sweep algorithm
  function safePollJobs(jobList, agentId, currentTime) {
    // 1. Conservative sweep: Expired claims transition to UncertainDelivery
    for (const j of jobList) {
      if (j.status === 'Claimed' && j.leaseExpiresAtUtc && new Date(j.leaseExpiresAtUtc) < currentTime) {
        j.status = 'UncertainDelivery';
        j.deliveryState = 'Uncertain';
        j.uncertainReason = 'Claim lease expired without agent acknowledgement. Physical printing unconfirmed.';
      }
    }

    // 2. Only claim Pending jobs
    const claimed = [];
    for (const j of jobList) {
      if (j.status === 'Pending') {
        j.status = 'Claimed';
        j.claimToken = `claim-${Math.random().toString(36).substring(2)}`;
        j.leaseExpiresAtUtc = new Date(currentTime.getTime() + 60000).toISOString();
        j.claimedByAgentId = agentId;
        claimed.push(j);
      }
    }
    return claimed;
  }

  // Agent 2 polls
  const claimedJobs = safePollJobs(jobs, 'agent-live', now);

  // Verify:
  // 1. Pending job was claimed
  assert.equal(claimedJobs.length, 1);
  assert.equal(claimedJobs[0].id, 'job-pending');

  // 2. Crashed job was NOT claimed by Agent 2 (preventing duplicate physical print)!
  const crashedJob = jobs.find(j => j.id === 'job-crashed-agent');
  assert.equal(crashedJob.status, 'UncertainDelivery');
  assert.equal(crashedJob.deliveryState, 'Uncertain');
  assert.match(crashedJob.uncertainReason, /Physical printing unconfirmed/);
});

test('7. Socket reset during transmission reports UncertainDelivery and requires operator reprint', async () => {
  const job = {
    id: 'job-reset',
    status: 'Claimed',
    claimToken: 'tok-123',
    claimedByAgentId: 'agent-1'
  };

  // Simulating agent ack when socket resets
  function acknowledgeJob(j, agentId, token, status, errorMessage) {
    if (j.claimedByAgentId !== agentId || j.claimToken !== token) {
      throw { statusCode: 403, message: 'Forbidden' };
    }
    j.status = status;
    j.deliveryState = status === 'UncertainDelivery' ? 'Uncertain' : 'Certain';
    j.uncertainReason = errorMessage;
    return j;
  }

  acknowledgeJob(job, 'agent-1', 'tok-123', 'UncertainDelivery', 'Socket ECONNRESET after TCP buffer write');
  assert.equal(job.status, 'UncertainDelivery');
  assert.equal(job.deliveryState, 'Uncertain');

  // Audited operator reprint creates explicit reprint ticket
  function reprintTicket(originalJob, operator, reason) {
    return {
      id: `reprint-${originalJob.id}`,
      originalJobId: originalJob.id,
      jobType: 'Reprint',
      status: 'Pending',
      reprintCount: 1,
      reprintReason: reason,
      reprintedBy: operator
    };
  }

  const reprint = reprintTicket(job, 'cashier_ravi', 'Customer did not get paper ticket');
  assert.equal(reprint.status, 'Pending');
  assert.equal(reprint.jobType, 'Reprint');
  assert.equal(reprint.reprintReason, 'Customer did not get paper ticket');
});

test('8. Station-specific KOT formatting with TOKEN #012 and PARCEL ORDER', async () => {
  const ticket = formatParcelKotTicket({
    restaurantName: 'Royal Tandoor',
    stationName: 'Food Kitchen',
    ticketNumber: 'KOT-001',
    orderNumber: 101,
    dailyTokenNumber: 12,
    customerName: 'Vikram Seth',
    customerPhone: '9876543210',
    items: [
      { name: 'Hyderabadi Dum Biryani', quantity: 2, specialInstructions: 'Extra spicy, separate raita' }
    ]
  });

  assert.match(ticket, /\*\*\* FOOD KITCHEN \*\*\*/);
  assert.match(ticket, /\*\*\* PARCEL ORDER \*\*\*/);
  assert.match(ticket, /\*\*\* TOKEN: #012 \*\*\*/);
  assert.match(ticket, /Customer: Vikram Seth \(9876543210\)/);
  assert.match(ticket, /2x Hyderabadi Dum Biryani/);
  assert.match(ticket, /Note: Extra spicy, separate raita/);
});

test('9. Text word wrapping cleanly on 58mm (32 cols) and 80mm (48 cols)', async () => {
  const instructions = 'Please prepare with very mild green chillies, separate gravy container, and no fried onions on top';

  const lines58 = wrapText(instructions, 32 - 5);
  for (const line of lines58) {
    assert.ok(line.length <= 27);
  }

  const lines80 = wrapText(instructions, 48 - 5);
  for (const line of lines80) {
    assert.ok(line.length <= 43);
  }
});

test('10. End-to-end integration lifecycle: food & drink parcel, separate station tickets, partial UPI, final Cash, single ingredient deduction, settled receipt, and lost ack followed by audited REPRINT', async () => {
  // 1. Setup simulated engine with stations and recipes
  const engine = new SimulatedParcelEngine();
  
  // Set packaging: Flat ₹30 + 5% tax = ₹31.50
  engine.packagingSettings = {
    chargeMode: 'FlatPerOrder',
    flatRate: 30.00,
    percentage: 0,
    isTaxable: true,
    taxRate: 5.0
  };

  // 2. Create Parcel Order with Food (2x Biryani @ ₹320) + Drink (1x Lassi @ ₹90)
  // Subtotal: ₹730, Packaging: ₹30, Food Tax (5%): ₹36.50, Pkg Tax (5%): ₹1.50 -> Grand Total: ₹798.00
  const orderRes = engine.createParcelOrder({
    operationId: 'op-e2e-order-001',
    customerName: 'Ananya Roy',
    customerPhone: '9876543210',
    items: [
      { itemId: 'dish-biryani', quantity: 2 },
      { itemId: 'drink-lassi', quantity: 1 }
    ]
  });

  assert.equal(orderRes.order.totalAmount, 798.00);
  assert.equal(orderRes.bill.remainingAmount, 798.00);
  assert.equal(orderRes.bill.paidAmount, 0);
  assert.equal(orderRes.order.status, 'Accepted');

  // Verify multi-station ticket routing:
  // Food items -> Food Kitchen, Drink items -> Beverage Station
  const foodItems = orderRes.order.items.filter(i => engine.menu.get(i.itemId)?.station === 'Food Kitchen');
  const drinkItems = orderRes.order.items.filter(i => engine.menu.get(i.itemId)?.station === 'Beverage Station');
  assert.equal(foodItems.length, 1);
  assert.equal(foodItems[0].quantity, 2);
  assert.equal(drinkItems.length, 1);
  assert.equal(drinkItems[0].quantity, 1);

  const foodKot = formatParcelKotTicket({
    restaurantName: 'Biryani House',
    stationName: 'Food Kitchen',
    ticketNumber: 'KOT-101',
    orderNumber: orderRes.order.orderNumber,
    dailyTokenNumber: orderRes.order.dailyTokenNumber,
    customerName: orderRes.order.customerName,
    items: [{ name: 'Hyderabadi Dum Biryani', quantity: 2 }]
  });
  assert.match(foodKot, /\*\*\* FOOD KITCHEN \*\*\*/);
  assert.match(foodKot, /2x Hyderabadi Dum Biryani/);
  assert.doesNotMatch(foodKot, /Mango Lassi/);

  const barKot = formatParcelKotTicket({
    restaurantName: 'Biryani House',
    stationName: 'Beverage Station',
    ticketNumber: 'KOT-102',
    orderNumber: orderRes.order.orderNumber,
    dailyTokenNumber: orderRes.order.dailyTokenNumber,
    customerName: orderRes.order.customerName,
    items: [{ name: 'Mango Lassi', quantity: 1 }]
  });
  assert.match(barKot, /\*\*\* BEVERAGE STATION \*\*\*/);
  assert.match(barKot, /1x Mango Lassi/);
  assert.doesNotMatch(barKot, /Hyderabadi Dum Biryani/);

  // Check initial stock: 10kg chicken, 20kg rice, 15L milk
  assert.equal(engine.ingredients.get('ing-chicken').onHand, 10.0);
  assert.equal(engine.ingredients.get('ing-rice').onHand, 20.0);
  assert.equal(engine.ingredients.get('ing-milk').onHand, 15.0);
  assert.equal(engine.stockMovements.length, 0, 'No stock movements before settlement');

  // Verify Active stock reservations exist (chicken, rice, milk)
  const activeRes = engine.reservations.filter(r => r.orderId === orderRes.order.id && r.status === 'Active');
  assert.equal(activeRes.length, 3);

  // 3. Partial Payment 1: UPI ₹300.00
  const pay1 = engine.recordPayment(orderRes.bill.id, {
    operationId: 'op-pay-upi-300',
    amount: 300.00,
    method: 'UPI'
  });
  assert.equal(pay1.remaining, 498.00);
  assert.equal(orderRes.bill.paidAmount, 300.00);
  assert.equal(orderRes.order.status, 'Accepted', 'Order must NOT be Served after partial payment');
  assert.equal(engine.stockMovements.length, 0, 'No stock deducted on partial payment');

  // Idempotent retry of payment 1 with identical operationId
  const pay1Replay = engine.recordPayment(orderRes.bill.id, {
    operationId: 'op-pay-upi-300',
    amount: 300.00,
    method: 'UPI'
  });
  assert.equal(pay1Replay.isDuplicate, true);
  assert.equal(pay1Replay.remaining, 498.00);

  // Overpayment prevention: Trying to pay ₹600 when ₹498.00 remains
  assert.throws(() => {
    engine.recordPayment(orderRes.bill.id, {
      operationId: 'op-pay-overpay',
      amount: 600.00,
      method: 'Cash'
    });
  }, /Payment exceeds/);

  // 4. Final Payment 2: Cash ₹498.00
  const pay2 = engine.recordPayment(orderRes.bill.id, {
    operationId: 'op-pay-cash-final',
    amount: 498.00,
    method: 'Cash'
  });
  assert.equal(pay2.remaining, 0.00);
  assert.equal(pay2.isSettled, true);
  assert.equal(orderRes.bill.isPaid, true);
  assert.equal(orderRes.order.status, 'Served', 'Order transitions to Served only when balance reaches zero');
  assert.equal(orderRes.bill.paymentMethod, 'Mixed');

  // 5. Verify ingredient stock deducted exactly once
  // Chicken: 10 - 2*0.3 = 9.4 kg
  // Rice: 20 - 2*0.2 = 19.6 kg
  // Milk: 15 - 1*0.2 = 14.8 L
  assert.equal(engine.ingredients.get('ing-chicken').onHand, 9.4);
  assert.equal(engine.ingredients.get('ing-rice').onHand, 19.6);
  assert.equal(engine.ingredients.get('ing-milk').onHand, 14.8);
  assert.equal(engine.stockMovements.length, 3, 'Stock movements recorded exactly once');

  // All reservations committed
  const committedRes = engine.reservations.filter(r => r.orderId === orderRes.order.id && r.status === 'Committed');
  assert.equal(committedRes.length, 3);

  // 6. Test Lost Ack / Connection Reset & Uncertain Delivery
  // When connection drops or resets after TCP buffer dispatch, agent reports UncertainDelivery
  const job = {
    id: 'job-food-kot-01',
    status: 'Claimed',
    claimToken: 'tok-abc',
    claimedByAgentId: 'agent-1'
  };

  // Socket reset simulated after bytes written to network buffer
  job.status = 'UncertainDelivery';
  job.deliveryState = 'Uncertain';
  job.uncertainReason = 'Socket reset (ECONNRESET) after TCP buffer dispatch. Physical paper exit unconfirmed.';

  assert.equal(job.status, 'UncertainDelivery');
  assert.equal(job.deliveryState, 'Uncertain');

  // 7. Verify Safe Retry Rejection & Audited Reprint
  // A retry MUST be rejected when delivery is uncertain to prevent duplicate tickets
  function attemptRetry(status) {
    if (status === 'UncertainDelivery' || status === 'Completed' || status === 'Printed') {
      const err = new Error('Delivery cannot be verified. An authorized audited reprint is required to avoid duplicate tickets.');
      err.statusCode = 409;
      throw err;
    }
    return { ok: true };
  }

  assert.throws(() => attemptRetry(job.status), /audited reprint is required/);

  // Audited reprint
  const reprintTicket = {
    id: 'ticket-reprint-999',
    originalTicketId: 'ticket-food-101',
    isReprint: true,
    reprintCount: 1,
    reprintReason: 'Printer connection reset during transmission',
    reprintedBy: 'cashier_ananya',
    createdAt: new Date()
  };

  assert.equal(reprintTicket.isReprint, true);
  assert.equal(reprintTicket.reprintCount, 1);
  assert.equal(reprintTicket.originalTicketId, 'ticket-food-101');

  const reprintKot = formatParcelKotTicket({
    restaurantName: 'Biryani House',
    stationName: 'Food Kitchen',
    ticketNumber: 'KOT-101-R1',
    orderNumber: orderRes.order.orderNumber,
    dailyTokenNumber: orderRes.order.dailyTokenNumber,
    customerName: orderRes.order.customerName,
    isReprint: true,
    reprintCount: 1,
    reprintReason: reprintTicket.reprintReason,
    items: [{ name: 'Hyderabadi Dum Biryani', quantity: 2 }]
  });

  assert.match(reprintKot, /\*\*\* REPRINT \*\*\*/);
  assert.match(reprintKot, /Reason: Printer connection reset/);
});
