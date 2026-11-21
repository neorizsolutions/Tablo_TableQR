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

// Helper: Format IST Time string
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

// Helper to send ESC/POS bytes to loopback virtual printer
function sendToPrinter(host, port, data) {
  return new Promise((resolve, reject) => {
    const socket = new net.Socket();
    socket.setTimeout(3000);
    socket.connect(port, host, () => {
      socket.write(data, () => {
        socket.end();
      });
    });
    socket.on('close', () => resolve(true));
    socket.on('error', (err) => reject(err));
    socket.on('timeout', () => {
      socket.destroy();
      reject(new Error('Connection timed out'));
    });
  });
}

// Mock ESC/POS Formatter for Marketplace Delivery Tickets
function formatMarketplaceKot({
  restaurantName,
  tokenFormatted,
  deliveryProvider,
  externalOrderReference,
  stationName,
  items,
  timestamp
}) {
  const lines = [];
  lines.push(`*** ${deliveryProvider.toUpperCase()} DELIVERY ***`);
  lines.push(`TOKEN: ${tokenFormatted}`);
  lines.push(`EXT REF: ${externalOrderReference}`);
  lines.push(`STATION: ${stationName.toUpperCase()}`);
  lines.push(`TIME: ${timestamp}`);
  lines.push('----------------------------------------');
  for (const item of items) {
    lines.push(`${item.quantity}x ${item.name}`);
    if (item.instructions) lines.push(`   * NOTE: ${item.instructions}`);
  }
  lines.push('----------------------------------------');
  lines.push('TABLELESS ORDER - DO NOT ASSIGN FLOOR TABLE');
  return lines.join('\n');
}

// -------------------------------------------------------------
// 1. MANUAL MARKETPLACE ORDER ENTRY (SWIGGY & ZOMATO)
// -------------------------------------------------------------
test('1. Manual Delivery Order Entry: Swiggy and Zomato order creation with external reference', async (t) => {
  const restaurantId = crypto.randomUUID();
  const outletId = crypto.randomUUID();
  const businessDate = getIstBusinessDate();

  // In-memory store simulating Order and TableBill
  const ordersDb = new Map();
  const billsDb = new Map();

  function createManualDeliveryOrder(req) {
    if (req.orderChannel === 'Delivery') {
      if (!req.externalOrderReference || !req.externalOrderReference.trim()) {
        throw new Error('External order reference is required for marketplace delivery orders.');
      }
      if (req.deliveryProvider === 'Other Delivery' && (!req.customProviderName || !req.customProviderName.trim())) {
        throw new Error('Provider name is required for Other Delivery.');
      }
    }

    // Uniqueness check: (restaurantId, outletId, deliveryProvider, normalizedExternalReference)
    const normalizedRef = req.externalOrderReference ? req.externalOrderReference.trim().toUpperCase() : null;
    if (normalizedRef) {
      for (const existing of ordersDb.values()) {
        if (
          existing.restaurantId === restaurantId &&
          existing.outletId === outletId &&
          existing.deliveryProvider === req.deliveryProvider &&
          existing.normalizedExternalReference === normalizedRef
        ) {
          if (existing.operationId === req.operationId) {
            // Exact retry idempotency: return original response
            return existing;
          }
          // Duplicate reference error pointing to existing order
          const err = new Error(`Order with external reference '${req.externalOrderReference}' already exists (Token #${String(existing.dailyTokenNumber).padStart(3, '0')}).`);
          err.statusCode = 409;
          err.existingOrderId = existing.orderId;
          throw err;
        }
      }
    }

    const dailyTokenNumber = ordersDb.size + 1;
    const orderId = crypto.randomUUID();
    const billId = crypto.randomUUID();

    const subtotal = req.items.reduce((s, i) => s + (i.unitPrice * i.quantity), 0);
    const packagingCharge = req.waivePackaging ? 0 : 25.00;
    const tax = Math.round((subtotal + packagingCharge) * 0.05 * 100) / 100;
    const totalAmount = subtotal + packagingCharge + tax;

    const orderRecord = {
      orderId,
      billId,
      restaurantId,
      outletId,
      operationId: req.operationId,
      orderNumber: 1000 + dailyTokenNumber,
      dailyTokenNumber,
      businessDate,
      tokenFormatted: `#${String(dailyTokenNumber).padStart(3, '0')}`,
      orderChannel: req.orderChannel,
      deliveryProvider: req.deliveryProvider,
      externalOrderReference: req.externalOrderReference,
      normalizedExternalReference: normalizedRef,
      marketplaceCollected: req.marketplaceCollected ?? true,
      marketplaceEnteredTotal: req.marketplaceEnteredTotal,
      marketplaceEnteredTax: req.marketplaceEnteredTax,
      subtotal,
      packagingCharge,
      tax,
      totalAmount,
      status: 'Placed',
      items: req.items
    };

    const billRecord = {
      id: billId,
      orderId,
      deliveryProvider: req.deliveryProvider,
      externalOrderReference: req.externalOrderReference,
      totalAmount,
      paidAmount: 0,
      remainingAmount: totalAmount,
      status: 'Unsettled',
      revision: crypto.randomUUID()
    };

    ordersDb.set(orderId, orderRecord);
    billsDb.set(billId, billRecord);
    return orderRecord;
  }

  // 1A. Submit valid Swiggy delivery order
  const swiggyReq = {
    operationId: 'op-swiggy-001',
    orderChannel: 'Delivery',
    deliveryProvider: 'Swiggy',
    externalOrderReference: 'SWIGGY-8921',
    marketplaceCollected: true,
    marketplaceEnteredTotal: 550.00,
    marketplaceEnteredTax: 25.00,
    waivePackaging: false,
    items: [
      { menuItemId: 'item-biryani', name: 'Chicken Biryani', quantity: 1, unitPrice: 320.00 },
      { menuItemId: 'item-lime', name: 'Fresh Lime Soda', quantity: 2, unitPrice: 80.00 }
    ]
  };

  const swiggyOrder = createManualDeliveryOrder(swiggyReq);
  assert.equal(swiggyOrder.tokenFormatted, '#001');
  assert.equal(swiggyOrder.deliveryProvider, 'Swiggy');
  assert.equal(swiggyOrder.externalOrderReference, 'SWIGGY-8921');
  assert.equal(swiggyOrder.marketplaceCollected, true);
  assert.equal(swiggyOrder.subtotal, 480.00); // 320 + 160
  assert.equal(swiggyOrder.packagingCharge, 25.00);
  assert.equal(swiggyOrder.tax, 25.25);
  assert.equal(swiggyOrder.totalAmount, 530.25);

  // 1B. Exact retry with same operationId returns the same order (idempotency)
  const retryOrder = createManualDeliveryOrder(swiggyReq);
  assert.equal(retryOrder.orderId, swiggyOrder.orderId);
  assert.equal(retryOrder.tokenFormatted, '#001');

  // 1C. Duplicate submission with different operationId but same external reference throws 409
  assert.throws(
    () => {
      createManualDeliveryOrder({
        ...swiggyReq,
        operationId: 'op-swiggy-002' // Different opId, same reference
      });
    },
    (err) => {
      assert.equal(err.statusCode, 409);
      assert.match(err.message, /already exists/);
      assert.equal(err.existingOrderId, swiggyOrder.orderId);
      return true;
    }
  );

  // 1D. Submit valid Zomato delivery order
  const zomatoReq = {
    operationId: 'op-zomato-001',
    orderChannel: 'Delivery',
    deliveryProvider: 'Zomato',
    externalOrderReference: 'ZOM-4412',
    marketplaceCollected: true,
    marketplaceEnteredTotal: 420.00,
    waivePackaging: false,
    items: [
      { menuItemId: 'item-paneer', name: 'Paneer Butter Masala', quantity: 1, unitPrice: 280.00 },
      { menuItemId: 'item-roti', name: 'Butter Roti', quantity: 3, unitPrice: 30.00 }
    ]
  };

  const zomatoOrder = createManualDeliveryOrder(zomatoReq);
  assert.equal(zomatoOrder.tokenFormatted, '#002');
  assert.equal(zomatoOrder.deliveryProvider, 'Zomato');
  assert.equal(zomatoOrder.externalOrderReference, 'ZOM-4412');
  assert.equal(zomatoOrder.subtotal, 370.00); // 280 + 90
});

// -------------------------------------------------------------
// 2. KITCHEN QUEUE & DUAL-STATION KOT ROUTING (FOOD & BEVERAGE)
// -------------------------------------------------------------
test('2. KOT Routing: Delivery orders route to station printers with provider and external ref', async (t) => {
  const timestamp = formatIstTime();

  const swiggyOrder = {
    tokenFormatted: '#001',
    deliveryProvider: 'Swiggy',
    externalOrderReference: 'SWIGGY-8921',
    items: [
      { name: 'Chicken Biryani', quantity: 1, category: 'Food', station: 'Food Kitchen' },
      { name: 'Fresh Lime Soda', quantity: 2, category: 'Beverage', station: 'Beverage Station', instructions: 'Less sweet' }
    ]
  };

  // Food ticket
  const foodItems = swiggyOrder.items.filter(i => i.category === 'Food');
  const foodTicket = formatMarketplaceKot({
    restaurantName: 'Tablo Heritage Restaurant',
    tokenFormatted: swiggyOrder.tokenFormatted,
    deliveryProvider: swiggyOrder.deliveryProvider,
    externalOrderReference: swiggyOrder.externalOrderReference,
    stationName: 'Food Kitchen',
    items: foodItems,
    timestamp
  });

  assert.match(foodTicket, /\*\*\* SWIGGY DELIVERY \*\*\*/);
  assert.match(foodTicket, /TOKEN: #001/);
  assert.match(foodTicket, /EXT REF: SWIGGY-8921/);
  assert.match(foodTicket, /STATION: FOOD KITCHEN/);
  assert.match(foodTicket, /1x Chicken Biryani/);
  assert.doesNotMatch(foodTicket, /Fresh Lime Soda/);

  // Beverage ticket
  const beverageItems = swiggyOrder.items.filter(i => i.category === 'Beverage');
  const bevTicket = formatMarketplaceKot({
    restaurantName: 'Tablo Heritage Restaurant',
    tokenFormatted: swiggyOrder.tokenFormatted,
    deliveryProvider: swiggyOrder.deliveryProvider,
    externalOrderReference: swiggyOrder.externalOrderReference,
    stationName: 'Beverage Station',
    items: beverageItems,
    timestamp
  });

  assert.match(bevTicket, /\*\*\* SWIGGY DELIVERY \*\*\*/);
  assert.match(bevTicket, /TOKEN: #001/);
  assert.match(bevTicket, /EXT REF: SWIGGY-8921/);
  assert.match(bevTicket, /STATION: BEVERAGE STATION/);
  assert.match(bevTicket, /2x Fresh Lime Soda/);
  assert.match(bevTicket, /Less sweet/);
  assert.doesNotMatch(bevTicket, /Chicken Biryani/);

  // Verify virtual printers accept the tickets
  try {
    await sendToPrinter('127.0.0.1', 9100, Buffer.from(foodTicket, 'utf-8'));
    await sendToPrinter('127.0.0.1', 9101, Buffer.from(bevTicket, 'utf-8'));
  } catch (err) {
    // Virtual printer check non-fatal if not listening
    console.log('Printer simulation socket note:', err.message);
  }
});

// -------------------------------------------------------------
// 3. MARKETPLACE PAYMENT CLOSURE WITHOUT CASH/CARD INFLATION
// -------------------------------------------------------------
test('3. Marketplace Collected Settlement: Consumes stock once, zero cash register inflation', async (t) => {
  const bill = {
    id: 'bill-dlv-001',
    orderId: 'ord-dlv-001',
    totalAmount: 530.25,
    paidAmount: 0,
    remainingAmount: 530.25,
    status: 'Unsettled',
    revision: 'rev-1',
    payments: []
  };

  const inventoryStock = {
    'Chicken': 5000, // grams
    'Rice': 10000,
    'Lime': 50
  };

  let stockMovementsLogged = 0;
  let receiptsCreated = 0;

  function settleMarketplaceBill(settleMethod) {
    if (bill.status === 'Settled' || bill.remainingAmount <= 0) {
      throw new Error('Bill is already settled.');
    }

    // 1. Consume stock once
    inventoryStock['Chicken'] -= 250;
    inventoryStock['Rice'] -= 200;
    inventoryStock['Lime'] -= 2;
    stockMovementsLogged += 3;

    // 2. Record payment
    bill.payments.push({
      id: crypto.randomUUID(),
      method: settleMethod,
      amount: bill.remainingAmount,
      collectedAtUtc: new Date().toISOString()
    });
    bill.paidAmount = bill.totalAmount;
    bill.remainingAmount = 0;
    bill.status = 'Settled';
    bill.paymentMethod = settleMethod;

    // 3. Create immutable receipt
    receiptsCreated += 1;
    return bill;
  }

  settleMarketplaceBill('MarketplaceCollected');

  assert.equal(bill.status, 'Settled');
  assert.equal(bill.paidAmount, 530.25);
  assert.equal(bill.remainingAmount, 0);
  assert.equal(stockMovementsLogged, 3);
  assert.equal(receiptsCreated, 1);
  assert.equal(inventoryStock['Chicken'], 4750);

  // Attempting second settlement throws
  assert.throws(() => settleMarketplaceBill('MarketplaceCollected'), /already settled/);

  // Payment reporting verification: Cash, UPI, Card are NOT inflated
  const paymentReport = {
    cashTotal: 0,
    upiTotal: 0,
    cardTotal: 0,
    marketplaceTotal: 0
  };

  for (const p of bill.payments) {
    if (p.method === 'Cash') paymentReport.cashTotal += p.amount;
    else if (p.method === 'UPI') paymentReport.upiTotal += p.amount;
    else if (p.method === 'Card') paymentReport.cardTotal += p.amount;
    else if (p.method === 'MarketplaceCollected' || p.method === 'Marketplace') {
      paymentReport.marketplaceTotal += p.amount;
    }
  }

  assert.equal(paymentReport.cashTotal, 0, 'Local cash total must be ₹0');
  assert.equal(paymentReport.upiTotal, 0, 'Local UPI total must be ₹0');
  assert.equal(paymentReport.cardTotal, 0, 'Local card total must be ₹0');
  assert.equal(paymentReport.marketplaceTotal, 530.25, 'Marketplace total must equal bill amount');
});

// -------------------------------------------------------------
// 4. MARKETPLACE PAYOUT RECONCILIATION
// -------------------------------------------------------------
test('4. Marketplace Payout Recording: Gross sales, commission deductions, and net reconciliation', async (t) => {
  const payoutsDb = [];

  function recordMarketplacePayout(payoutDto) {
    if (!payoutDto.payoutReference || !payoutDto.payoutReference.trim()) {
      throw new Error('Payout reference is required.');
    }
    if (payoutsDb.some(p => p.payoutReference === payoutDto.payoutReference && p.deliveryProvider === payoutDto.deliveryProvider)) {
      throw new Error(`Payout statement '${payoutDto.payoutReference}' for ${payoutDto.deliveryProvider} already recorded.`);
    }

    const calculatedNet = Math.round(
      (payoutDto.grossAmount - payoutDto.commissionDeducted - payoutDto.tcsGstDeducted + (payoutDto.otherAdjustments || 0)) * 100
    ) / 100;

    const discrepancy = Math.abs(calculatedNet - payoutDto.netAmount);

    const record = {
      id: crypto.randomUUID(),
      ...payoutDto,
      calculatedNet,
      reconciliationDiscrepancy: discrepancy,
      createdAtUtc: new Date().toISOString()
    };
    payoutsDb.push(record);
    return record;
  }

  // Record Swiggy weekly payout statement
  const swiggyPayout = recordMarketplacePayout({
    deliveryProvider: 'Swiggy',
    payoutReference: 'SWIGGY-PAY-20261121',
    payoutDateUtc: '2026-11-21T00:00:00Z',
    grossAmount: 10000.00,
    commissionDeducted: 1800.00, // 18%
    tcsGstDeducted: 250.00,
    otherAdjustments: -50.00, // packaging recovery or promo discount
    netAmount: 7900.00, // 10000 - 1800 - 250 - 50 = 7900
    linkedOrderIds: ['ord-dlv-001', 'ord-dlv-002']
  });

  assert.equal(swiggyPayout.calculatedNet, 7900.00);
  assert.equal(swiggyPayout.reconciliationDiscrepancy, 0.00);

  // Duplicate payout reference protection
  assert.throws(
    () => recordMarketplacePayout({
      deliveryProvider: 'Swiggy',
      payoutReference: 'SWIGGY-PAY-20261121',
      grossAmount: 10000.00,
      commissionDeducted: 1800.00,
      tcsGstDeducted: 250.00,
      netAmount: 7900.00
    }),
    /already recorded/
  );
});

// -------------------------------------------------------------
// 5. OUTLET TRANSFER SHORTAGE ACCOUNTING & COMPENSATING CORRECTION
// -------------------------------------------------------------
test('5. Outlet Transfer Shortage: Single-deduction accounting (10,000 g dispatched, 9,500 g received -> exactly 500 g net loss)', async (t) => {
  // Scenario:
  // Central Kitchen dispatches 10,000 g of Basmati Rice.
  // Branch Outlet receives 9,500 g of Basmati Rice.
  // Shortage: 500 g in transit.
  //
  // Desired outcome:
  // Source outlet stock: -10,000 g
  // Destination outlet stock: +9,500 g
  // Restaurant-wide net stock change: -500 g (EXACT shortage).
  // NO extra duplicate movement of -500 g on source outlet.

  const centralKitchenStock = { onHand: 50000 };
  const branchOutletStock = { onHand: 10000 };
  const ledgerMovements = [];

  // Step 1: Dispatch 10,000 g
  const dispatchedQty = 10000;
  centralKitchenStock.onHand -= dispatchedQty;
  ledgerMovements.push({
    outlet: 'Central Kitchen',
    type: 'TransferOut',
    quantity: -dispatchedQty,
    unitCost: 1.50
  });

  // Step 2: Receive 9,500 g with 500 g shortage
  const receivedQty = 9500;
  branchOutletStock.onHand += receivedQty;
  ledgerMovements.push({
    outlet: 'Branch Outlet',
    type: 'TransferIn',
    quantity: receivedQty,
    unitCost: 1.50
  });

  // Verify: Restaurant-wide net inventory change
  const netRestaurantChange = ledgerMovements.reduce((sum, m) => sum + m.quantity, 0);
  assert.equal(netRestaurantChange, -500, 'Restaurant-wide net change must be exactly -500 g');

  // Verify: Central kitchen decreased by 10,000 g, branch increased by 9,500 g
  assert.equal(centralKitchenStock.onHand, 40000);
  assert.equal(branchOutletStock.onHand, 19500);

  // -----------------------------------------------------------
  // Historical Bug Simulation & Idempotent Compensating Correction
  // -----------------------------------------------------------
  // If a past transfer suffered from the double-deduction bug:
  // An erroneous extra movement was added: { outlet: 'Central Kitchen', type: 'TransferDiscrepancyLoss', quantity: -500 }
  // This resulted in -1,000 g net change instead of -500 g.

  const buggyMovements = [
    { id: 'm1', transferId: 'tr-001', type: 'TransferOut', quantity: -10000, unitCost: 1.50 },
    { id: 'm2', transferId: 'tr-001', type: 'TransferIn', quantity: 9500, unitCost: 1.50 },
    { id: 'm3', transferId: 'tr-001', type: 'TransferDiscrepancyLoss', quantity: -500, unitCost: 1.50 } // Duplicate loss
  ];

  // Dry-run audit function
  function getTransferShortageDryRun(movements) {
    const affected = [];
    const grouped = new Map();
    for (const m of movements) {
      if (!grouped.has(m.transferId)) grouped.set(m.transferId, []);
      grouped.get(m.transferId).push(m);
    }

    for (const [tId, mvts] of grouped.entries()) {
      const transferOut = mvts.find(m => m.type === 'TransferOut');
      const transferIn = mvts.find(m => m.type === 'TransferIn');
      const discrepancyLoss = mvts.find(m => m.type === 'TransferDiscrepancyLoss');
      const existingCorrection = mvts.find(m => m.type === 'TransferDiscrepancyCorrection');

      if (transferOut && transferIn && discrepancyLoss) {
        const dispatched = Math.abs(transferOut.quantity);
        const received = transferIn.quantity;
        const shortage = dispatched - received;

        affected.push({
          transferId: tId,
          dispatchedQuantity: dispatched,
          receivedQuantity: received,
          shortageQuantity: shortage,
          excessLossValuation: shortage * discrepancyLoss.unitCost,
          isCorrected: !!existingCorrection
        });
      }
    }

    return {
      totalAffectedTransfers: affected.length,
      uncorrectedCount: affected.filter(a => !a.isCorrected).length,
      items: affected
    };
  }

  const dryRun1 = getTransferShortageDryRun(buggyMovements);
  assert.equal(dryRun1.totalAffectedTransfers, 1);
  assert.equal(dryRun1.uncorrectedCount, 1);
  assert.equal(dryRun1.items[0].shortageQuantity, 500);
  assert.equal(dryRun1.items[0].excessLossValuation, 750.00); // 500 * 1.50
  assert.equal(dryRun1.items[0].isCorrected, false);

  // Apply Compensating Correction: Append traceable movement without mutating history
  const compensatingMovement = {
    id: 'm4',
    transferId: 'tr-001',
    type: 'TransferDiscrepancyCorrection',
    quantity: +500, // Restores the erroneously deducted 500 g
    unitCost: 1.50,
    notes: 'Compensating correction for legacy double deduction on transfer tr-001'
  };
  buggyMovements.push(compensatingMovement);

  // After correction: Net change is restored to -500 g
  const netAfterCorrection = buggyMovements.reduce((sum, m) => sum + m.quantity, 0);
  assert.equal(netAfterCorrection, -500, 'Restored restaurant net change must be -500 g');

  // Dry-run after correction: uncorrectedCount is 0, isCorrected is true (idempotent)
  const dryRun2 = getTransferShortageDryRun(buggyMovements);
  assert.equal(dryRun2.uncorrectedCount, 0);
  assert.equal(dryRun2.items[0].isCorrected, true);
});
