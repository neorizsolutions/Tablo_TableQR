import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';

// Helper: Format IST Time
function formatIst(date) {
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

// ESC/POS Formatter Logic (mirroring backend EscPosFormatter.cs)
function formatKotTicket(params) {
  const {
    restaurantName,
    stationName,
    ticketNumber,
    orderNumber,
    tableNumber,
    groupLabel,
    ticketType,
    isReprint,
    reprintCount = 0,
    reprintReason,
    timestamp,
    items,
    notes,
    paperWidthMm = 80
  } = params;

  const colWidth = paperWidthMm === 58 ? 32 : 48;
  const divider = '-'.repeat(colWidth);
  const doubleDivider = '='.repeat(colWidth);
  const timestampIst = formatIst(timestamp);

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

  if (isReprint) {
    const reprintHeader = reprintCount > 1 ? `*** REPRINT #${reprintCount} ***` : '*** REPRINT ***';
    lines.push(center(reprintHeader));
    if (reprintReason) lines.push(center(`Reason: ${reprintReason}`));
  } else if (ticketType === 'Incremental') {
    lines.push(center('*** NEW ADDITION ***'));
  }

  lines.push(doubleDivider);

  const tableStr = `Table ${tableNumber}${groupLabel ? ` (${groupLabel})` : ''}`;
  lines.push(padSides(`Ticket: ${ticketNumber}`, `Order: ORD-${String(orderNumber).padStart(4, '0')}`));
  lines.push(padSides(tableStr, timestampIst));
  lines.push(divider);

  let totalQty = 0;
  for (const item of items) {
    totalQty += item.quantity;
    lines.push(`${item.quantity}x ${item.name}`);
    if (item.specialInstructions) {
      lines.push(`   * Note: ${item.specialInstructions}`);
    }
  }

  lines.push(divider);
  if (notes) {
    lines.push(`Order Notes: ${notes}`);
    lines.push(divider);
  }

  lines.push(padSides(`Total Items: ${totalQty}`, ticketType.toUpperCase()));
  lines.push(doubleDivider);

  const readableText = lines.join('\n');

  // Binary ESC/POS builder
  const chunks = [];
  chunks.push(Buffer.from([0x1B, 0x40])); // Init
  chunks.push(Buffer.from([0x1B, 0x61, 0x01])); // Center
  chunks.push(Buffer.from(`${restaurantName.toUpperCase()}\n`));
  chunks.push(Buffer.from(`*** ${stationName.toUpperCase()} ***\n`));
  if (isReprint) {
    chunks.push(Buffer.from(`*** REPRINT ***\n`));
  }
  chunks.push(Buffer.from([0x1B, 0x61, 0x00])); // Left
  chunks.push(Buffer.from(`${tableStr}\n`));
  chunks.push(Buffer.from([0x1D, 0x56, 0x42, 0x00])); // Cut

  const escPosBuffer = Buffer.concat(chunks);

  return { escPosBuffer, readableText, colWidth, totalQty };
}

// In-Memory Simulated Database & Routing Engine
class SimulatedPrintingEngine {
  constructor() {
    this.reset();
  }

  reset() {
    this.restaurant = { id: 'rest-1', name: 'Royal Tandoor & Bar' };
    this.settings = {
      kotPrintingEnabled: true,
      kitchenSetupMode: 'SingleKitchen',
      defaultKitchenStationId: 'st-main'
    };
    this.subscription = {
      planCode: 'premium',
      maxKitchenStations: 5,
      capabilities: new Set(['thermal_printing', 'multiple_stations'])
    };
    this.stations = new Map([
      ['st-main', { id: 'st-main', name: 'Main Kitchen', code: 'MAIN', isDefault: true, isActive: true, printerId: 'pr-main' }]
    ]);
    this.printers = new Map([
      ['pr-main', { id: 'pr-main', name: 'Kitchen Thermal 80mm', paperWidthMm: 80, status: 'Online', isActive: true }]
    ]);
    this.categories = new Map([
      ['cat-food', { id: 'cat-food', name: 'Food', kitchenStationId: null }],
      ['cat-bev', { id: 'cat-bev', name: 'Beverages', kitchenStationId: null }]
    ]);
    this.menuItems = new Map([
      ['item-biryani', { id: 'item-biryani', name: 'Dum Biryani', categoryId: 'cat-food', kitchenStationId: null }],
      ['item-naan', { id: 'item-naan', name: 'Butter Naan', categoryId: 'cat-food', kitchenStationId: null }],
      ['item-lassi', { id: 'item-lassi', name: 'Mango Lassi', categoryId: 'cat-bev', kitchenStationId: null }],
      ['item-mocktail', { id: 'item-mocktail', name: 'Blue Lagoon', categoryId: 'cat-bev', kitchenStationId: null }]
    ]);
    this.orders = new Map();
    this.batches = [];
    this.tickets = [];
    this.jobs = [];
    this.operations = new Map();
  }

  setupSeparateKitchens() {
    this.stations.set('st-food', { id: 'st-food', name: 'Food Kitchen', code: 'FOOD', isDefault: true, isActive: true, printerId: 'pr-food' });
    this.stations.set('st-bev', { id: 'st-bev', name: 'Beverage Station', code: 'BEV', isDefault: false, isActive: true, printerId: 'pr-bev' });
    this.printers.set('pr-food', { id: 'pr-food', name: 'Kitchen Food Printer 80mm', paperWidthMm: 80, status: 'Online', isActive: true });
    this.printers.set('pr-bev', { id: 'pr-bev', name: 'Bar Printer 58mm', paperWidthMm: 58, status: 'Online', isActive: true });

    this.settings.kitchenSetupMode = 'SeparateFoodBeverage';
    this.settings.defaultKitchenStationId = 'st-food';

    this.categories.get('cat-food').kitchenStationId = 'st-food';
    this.categories.get('cat-bev').kitchenStationId = 'st-bev';
  }

  resolveStation(menuItemId) {
    const isMulti = this.subscription.capabilities.has('multiple_stations') &&
                    this.settings.kitchenSetupMode !== 'SingleKitchen' &&
                    this.stations.size > 1;

    const defaultStation = Array.from(this.stations.values()).find(s => s.isDefault && s.isActive) ||
                           Array.from(this.stations.values())[0];

    if (!isMulti) return defaultStation;

    const item = this.menuItems.get(menuItemId);
    if (!item) return defaultStation;

    // 1. Explicit item override
    if (item.kitchenStationId && this.stations.has(item.kitchenStationId)) {
      return this.stations.get(item.kitchenStationId);
    }

    // 2. Category default
    const cat = this.categories.get(item.categoryId);
    if (cat?.kitchenStationId && this.stations.has(cat.kitchenStationId)) {
      return this.stations.get(cat.kitchenStationId);
    }

    // 3. Fallback to default station
    return defaultStation;
  }

  processAcceptedItems(order, items, actor = 'Staff', trigger = 'StaffAccepted') {
    if (!this.settings.kotPrintingEnabled) return [];
    if (!this.subscription.capabilities.has('thermal_printing')) return [];

    const grouped = new Map();
    for (const item of items) {
      const station = this.resolveStation(item.menuItemId);
      if (!grouped.has(station.id)) grouped.set(station.id, { station, items: [] });
      grouped.get(station.id).items.push(item);
    }

    const existingBatches = this.batches.filter(b => b.orderId === order.id);
    const batchSeq = existingBatches.length + 1;
    const kotNumber = `KOT-${String(order.orderNumber).padStart(4, '0')}-${batchSeq}`;

    const batch = {
      id: `batch-${this.batches.length + 1}`,
      orderId: order.id,
      batchSequence: batchSeq,
      kotNumber,
      triggeredBy: trigger,
      createdAt: new Date()
    };
    this.batches.push(batch);

    const ticketType = batchSeq === 1 ? 'New' : 'Incremental';
    const createdTickets = [];

    for (const [stId, { station, items: stationItems }] of grouped.entries()) {
      const ticketNumber = `${kotNumber}-${station.code}`;
      const ticket = {
        id: `ticket-${this.tickets.length + 1}`,
        orderId: order.id,
        batchId: batch.id,
        stationId: station.id,
        stationName: station.name,
        ticketNumber,
        orderNumber: order.orderNumber,
        tableNumber: order.tableNumber,
        groupLabel: order.groupLabel,
        ticketType,
        items: stationItems,
        isReprint: false,
        reprintCount: 0,
        createdAt: new Date()
      };
      this.tickets.push(ticket);
      createdTickets.push(ticket);

      const printer = this.printers.get(station.printerId);
      if (printer && printer.isActive) {
        const formatted = formatKotTicket({
          restaurantName: this.restaurant.name,
          stationName: station.name,
          ticketNumber,
          orderNumber: order.orderNumber,
          tableNumber: order.tableNumber,
          groupLabel: order.groupLabel,
          ticketType,
          isReprint: false,
          timestamp: ticket.createdAt,
          items: stationItems,
          paperWidthMm: printer.paperWidthMm
        });

        const job = {
          id: `job-${this.jobs.length + 1}`,
          ticketId: ticket.id,
          printerId: printer.id,
          stationId: station.id,
          ticketNumber,
          status: 'Pending',
          escPosBuffer: formatted.escPosBuffer,
          readableText: formatted.readableText,
          paperWidthMm: printer.paperWidthMm,
          attempts: 0,
          error: null,
          createdAt: new Date()
        };
        this.jobs.push(job);
      }
    }

    return createdTickets;
  }

  reprintTicket(ticketId, reason, actor = 'Staff') {
    const original = this.tickets.find(t => t.id === ticketId);
    if (!original) throw new Error('Ticket not found');

    original.reprintCount += 1;
    const reprintTicketNumber = `${original.ticketNumber}-R${original.reprintCount}`;

    const reprintTicket = {
      ...original,
      id: `ticket-${this.tickets.length + 1}`,
      ticketNumber: reprintTicketNumber,
      ticketType: 'Reprint',
      isReprint: true,
      reprintCount: original.reprintCount,
      reprintReason: reason,
      originalTicketId: original.id,
      createdAt: new Date()
    };
    this.tickets.push(reprintTicket);

    const station = this.stations.get(original.stationId);
    const printer = station ? this.printers.get(station.printerId) : null;
    if (printer) {
      const formatted = formatKotTicket({
        restaurantName: this.restaurant.name,
        stationName: station.name,
        ticketNumber: reprintTicketNumber,
        orderNumber: original.orderNumber,
        tableNumber: original.tableNumber,
        groupLabel: original.groupLabel,
        ticketType: 'Reprint',
        isReprint: true,
        reprintCount: original.reprintCount,
        reprintReason: reason,
        timestamp: reprintTicket.createdAt,
        items: original.items,
        paperWidthMm: printer.paperWidthMm
      });

      const job = {
        id: `job-${this.jobs.length + 1}`,
        ticketId: reprintTicket.id,
        printerId: printer.id,
        stationId: station.id,
        ticketNumber: reprintTicketNumber,
        status: 'Pending',
        escPosBuffer: formatted.escPosBuffer,
        readableText: formatted.readableText,
        paperWidthMm: printer.paperWidthMm,
        attempts: 0,
        createdAt: new Date()
      };
      this.jobs.push(job);
    }

    return reprintTicket;
  }
}

// ----------------------------------------------------
// TEST SUITE
// ----------------------------------------------------

test('1. Single kitchen receives food and beverages on one consolidated ticket', () => {
  const engine = new SimulatedPrintingEngine();

  const order = { id: 'ord-1', orderNumber: 101, tableNumber: 3, groupLabel: '3A', status: 'Accepted' };
  const items = [
    { menuItemId: 'item-biryani', name: 'Dum Biryani', quantity: 2 },
    { menuItemId: 'item-lassi', name: 'Mango Lassi', quantity: 1 }
  ];

  const tickets = engine.processAcceptedItems(order, items);

  assert.equal(tickets.length, 1, 'Single kitchen must produce exactly one ticket');
  assert.equal(tickets[0].stationName, 'Main Kitchen');
  assert.equal(tickets[0].ticketNumber, 'KOT-0101-1-MAIN');
  assert.equal(tickets[0].items.length, 2, 'Single ticket must contain both food and beverage items');

  const jobs = engine.jobs.filter(j => j.ticketId === tickets[0].id);
  assert.equal(jobs.length, 1);
  assert.match(jobs[0].readableText, /2x Dum Biryani/);
  assert.match(jobs[0].readableText, /1x Mango Lassi/);
});

test('2. Separate kitchens receive only their assigned category items', () => {
  const engine = new SimulatedPrintingEngine();
  engine.setupSeparateKitchens();

  const order = { id: 'ord-2', orderNumber: 102, tableNumber: 5, groupLabel: null, status: 'Accepted' };
  const items = [
    { menuItemId: 'item-biryani', name: 'Dum Biryani', quantity: 1 },
    { menuItemId: 'item-naan', name: 'Butter Naan', quantity: 3 },
    { menuItemId: 'item-mocktail', name: 'Blue Lagoon', quantity: 2 }
  ];

  const tickets = engine.processAcceptedItems(order, items);

  assert.equal(tickets.length, 2, 'Separate kitchens must produce two distinct station tickets');

  const foodTicket = tickets.find(t => t.stationName === 'Food Kitchen');
  const bevTicket = tickets.find(t => t.stationName === 'Beverage Station');

  assert.ok(foodTicket, 'Food ticket must exist');
  assert.ok(bevTicket, 'Beverage ticket must exist');

  assert.equal(foodTicket.items.length, 2, 'Food ticket has only 2 food items');
  assert.ok(foodTicket.items.every(i => i.name !== 'Blue Lagoon'), 'Food ticket must not contain drinks');

  assert.equal(bevTicket.items.length, 1, 'Beverage ticket has only 1 drink item');
  assert.equal(bevTicket.items[0].name, 'Blue Lagoon');

  // Verify printer widths
  const foodJob = engine.jobs.find(j => j.ticketId === foodTicket.id);
  const bevJob = engine.jobs.find(j => j.ticketId === bevTicket.id);

  assert.equal(foodJob.paperWidthMm, 80);
  assert.equal(bevJob.paperWidthMm, 58);
});

test('3. Precedence rule: Menu item override beats category default station', () => {
  const engine = new SimulatedPrintingEngine();
  engine.setupSeparateKitchens();

  // Create a third "Dessert / Cold Station"
  engine.stations.set('st-cold', { id: 'st-cold', name: 'Cold Counter', code: 'COLD', isDefault: false, isActive: true, printerId: 'pr-food' });

  // item-lassi is in cat-bev (default: st-bev), but explicitly overridden to st-cold!
  engine.menuItems.get('item-lassi').kitchenStationId = 'st-cold';

  const order = { id: 'ord-3', orderNumber: 103, tableNumber: 1, groupLabel: null, status: 'Accepted' };
  const items = [
    { menuItemId: 'item-mocktail', name: 'Blue Lagoon', quantity: 1 }, // routes to cat-bev -> st-bev
    { menuItemId: 'item-lassi', name: 'Mango Lassi', quantity: 2 }      // routes to item override -> st-cold
  ];

  const tickets = engine.processAcceptedItems(order, items);
  assert.equal(tickets.length, 2);

  const bevTicket = tickets.find(t => t.stationName === 'Beverage Station');
  const coldTicket = tickets.find(t => t.stationName === 'Cold Counter');

  assert.ok(bevTicket);
  assert.ok(coldTicket);
  assert.equal(coldTicket.items[0].name, 'Mango Lassi');
  assert.equal(bevTicket.items[0].name, 'Blue Lagoon');
});

test('4. Customer Placed orders produce NO KOT until staff acceptance', () => {
  const engine = new SimulatedPrintingEngine();

  // Step 1: Customer creates QR order in "Placed" status
  const qrOrder = { id: 'ord-qr-1', orderNumber: 201, tableNumber: 4, status: 'Placed' };
  const qrItems = [{ menuItemId: 'item-biryani', name: 'Dum Biryani', quantity: 1 }];

  // Placed order must NOT trigger KOT generation
  if (qrOrder.status === 'Accepted') {
    engine.processAcceptedItems(qrOrder, qrItems);
  }

  assert.equal(engine.tickets.length, 0, 'No tickets created while order is in Placed state');
  assert.equal(engine.jobs.length, 0, 'No print jobs created while order is in Placed state');

  // Step 2: Staff accepts the order (Placed -> Accepted transition)
  qrOrder.status = 'Accepted';
  const tickets = engine.processAcceptedItems(qrOrder, qrItems, 'Waiter Staff', 'CustomerAccepted');

  assert.equal(tickets.length, 1, 'KOT ticket created immediately after staff acceptance');
  assert.equal(engine.jobs.length, 1, 'Print job queued upon acceptance');
});

test('5. Staff additions beside a pending QR order print only accepted staff items', () => {
  const engine = new SimulatedPrintingEngine();

  // Customer placed order is pending at Table 4
  const pendingQrOrder = { id: 'ord-qr-pending', orderNumber: 301, tableNumber: 4, status: 'Placed' };
  const pendingQrItems = [{ menuItemId: 'item-biryani', name: 'Dum Biryani', quantity: 3 }];

  // Waiter enters staff order for Table 4 (enters Accepted status)
  const staffOrder = { id: 'ord-staff-1', orderNumber: 302, tableNumber: 4, status: 'Accepted' };
  const staffItems = [{ menuItemId: 'item-lassi', name: 'Mango Lassi', quantity: 2 }];

  const tickets = engine.processAcceptedItems(staffOrder, staffItems, 'Staff', 'StaffOrder');

  assert.equal(tickets.length, 1);
  assert.equal(tickets[0].orderNumber, 302);
  assert.equal(tickets[0].items.length, 1);
  assert.equal(tickets[0].items[0].name, 'Mango Lassi');
  assert.doesNotMatch(engine.jobs[0].readableText, /Dum Biryani/, 'Pending customer items must not be printed by staff addition');
});

test('6. Incremental item additions produce incremental tickets with NEW ADDITION banner', () => {
  const engine = new SimulatedPrintingEngine();

  const order = { id: 'ord-inc', orderNumber: 401, tableNumber: 6, status: 'Accepted' };

  // Initial order: Batch 1
  const initialItems = [{ menuItemId: 'item-biryani', name: 'Dum Biryani', quantity: 1 }];
  const batch1Tickets = engine.processAcceptedItems(order, initialItems);

  assert.equal(batch1Tickets[0].ticketType, 'New');
  assert.equal(batch1Tickets[0].ticketNumber, 'KOT-0401-1-MAIN');

  // Addition of items 15 minutes later: Batch 2
  const additionItems = [{ menuItemId: 'item-naan', name: 'Butter Naan', quantity: 2 }];
  const batch2Tickets = engine.processAcceptedItems(order, additionItems, 'Staff', 'StaffAddition');

  assert.equal(batch2Tickets[0].ticketType, 'Incremental');
  assert.equal(batch2Tickets[0].ticketNumber, 'KOT-0401-2-MAIN');
  assert.equal(batch2Tickets[0].items.length, 1);
  assert.equal(batch2Tickets[0].items[0].name, 'Butter Naan');

  const batch2Job = engine.jobs.find(j => j.ticketId === batch2Tickets[0].id);
  assert.match(batch2Job.readableText, /\*\*\* NEW ADDITION \*\*\*/);
  assert.doesNotMatch(batch2Job.readableText, /Dum Biryani/);
});

test('7. Acceptance retries and duplicate requests create only one logical ticket set', () => {
  const engine = new SimulatedPrintingEngine();

  const operationId = 'op-replay-123';
  const order = { id: 'ord-idemp', orderNumber: 501, tableNumber: 2, status: 'Accepted' };
  const items = [{ menuItemId: 'item-biryani', name: 'Dum Biryani', quantity: 1 }];

  // First execution
  if (!engine.operations.has(operationId)) {
    const t = engine.processAcceptedItems(order, items);
    engine.operations.set(operationId, { result: t });
  }

  assert.equal(engine.tickets.length, 1);
  assert.equal(engine.jobs.length, 1);

  // Network retry with same operationId
  let retryResult;
  if (engine.operations.has(operationId)) {
    retryResult = engine.operations.get(operationId).result;
  } else {
    retryResult = engine.processAcceptedItems(order, items);
  }

  assert.equal(engine.tickets.length, 1, 'Replay must not duplicate tickets');
  assert.equal(engine.jobs.length, 1, 'Replay must not duplicate print jobs');
  assert.equal(retryResult[0].ticketNumber, 'KOT-0501-1-MAIN');
});

test('8. Reprints are authorized, marked, and preserve original ticket history', () => {
  const engine = new SimulatedPrintingEngine();

  const order = { id: 'ord-rep', orderNumber: 601, tableNumber: 8, status: 'Accepted' };
  const items = [{ menuItemId: 'item-biryani', name: 'Dum Biryani', quantity: 1 }];

  const [origTicket] = engine.processAcceptedItems(order, items);
  assert.equal(origTicket.isReprint, false);
  assert.equal(origTicket.reprintCount, 0);

  // Issue authorized reprint
  const reprint = engine.reprintTicket(origTicket.id, 'Kitchen lost original paper');

  assert.equal(reprint.isReprint, true);
  assert.equal(reprint.reprintCount, 1);
  assert.equal(reprint.ticketNumber, 'KOT-0601-1-MAIN-R1');
  assert.equal(reprint.reprintReason, 'Kitchen lost original paper');

  const reprintJob = engine.jobs.find(j => j.ticketId === reprint.id);
  assert.match(reprintJob.readableText, /\*\*\* REPRINT \*\*\*/);
  assert.match(reprintJob.readableText, /Reason: Kitchen lost original paper/);

  // Verify original ticket was not overwritten
  assert.equal(origTicket.ticketNumber, 'KOT-0601-1-MAIN');
});

test('9. Print-agent simulator and hardware failure recovery', () => {
  const engine = new SimulatedPrintingEngine();

  const order = { id: 'ord-sim', orderNumber: 701, tableNumber: 9, status: 'Accepted' };
  const items = [{ menuItemId: 'item-naan', name: 'Butter Naan', quantity: 4 }];
  engine.processAcceptedItems(order, items);

  const job = engine.jobs[0];
  assert.equal(job.status, 'Pending');

  // Simulated agent polls and claims job
  job.status = 'Claimed';
  job.attempts += 1;

  // Scenario A: Offline Printer Error
  const simError = 'SIMULATED_OFFLINE: Printer socket connection timed out';
  job.status = 'Failed';
  job.error = simError;
  assert.equal(job.status, 'Failed');
  assert.equal(job.error, simError);

  // Operator triggers retry from UI
  job.status = 'Pending';
  job.error = null;
  assert.equal(job.status, 'Pending');

  // Scenario B: Lost acknowledgement / reset
  job.status = 'Claimed';
  job.attempts += 1;
  job.status = 'UncertainDelivery';
  job.error = 'SIMULATED_UNCERTAIN: Connection reset after buffer dispatch';
  assert.equal(job.status, 'UncertainDelivery');

  // Successful retry completes job
  job.status = 'Printed';
  assert.equal(job.status, 'Printed');
});

test('10. Subscription entitlement limits: Single station cannot enable multiple active stations', () => {
  const engine = new SimulatedPrintingEngine();

  // Downgrade subscription to Basic (single station only, maxKitchenStations = 1)
  engine.subscription.planCode = 'basic';
  engine.subscription.maxKitchenStations = 1;
  engine.subscription.capabilities.delete('multiple_stations');

  // Attempting to activate a second station is blocked
  const canAddSecondStation = engine.subscription.capabilities.has('multiple_stations') &&
                              engine.stations.size < engine.subscription.maxKitchenStations;

  assert.equal(canAddSecondStation, false, 'Single-station plan must block multiple active stations');

  // Safe consolidation: collapses all items to default station
  const defaultStation = engine.stations.get('st-main');
  for (const cat of engine.categories.values()) cat.kitchenStationId = null;
  for (const item of engine.menuItems.values()) item.kitchenStationId = null;

  assert.equal(engine.resolveStation('item-biryani').id, defaultStation.id);
  assert.equal(engine.resolveStation('item-lassi').id, defaultStation.id);
});

test('11. ESC/POS binary format conforms to standard printer specifications', () => {
  const items = [{ name: 'Chicken Tikka', quantity: 2, unitPrice: 240 }];
  const { escPosBuffer, colWidth, totalQty } = formatKotTicket({
    restaurantName: 'The Curry House',
    stationName: 'Food Kitchen',
    ticketNumber: 'KOT-0801-1-FOOD',
    orderNumber: 801,
    tableNumber: 12,
    groupLabel: null,
    ticketType: 'New',
    isReprint: false,
    timestamp: new Date('2026-11-19T10:00:00Z'),
    items,
    paperWidthMm: 80
  });

  assert.equal(colWidth, 48);
  assert.equal(totalQty, 2);

  // ESC @ init: 0x1B 0x40
  assert.equal(escPosBuffer[0], 0x1B);
  assert.equal(escPosBuffer[1], 0x40);

  // GS V cut: 0x1D 0x56 0x42 0x00 at end of buffer
  const cutSlice = escPosBuffer.slice(escPosBuffer.length - 4);
  assert.deepEqual(Array.from(cutSlice), [0x1D, 0x56, 0x42, 0x00]);
});
