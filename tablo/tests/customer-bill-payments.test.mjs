import test from 'node:test';
import assert from 'node:assert/strict';
import {
  generateUpiUri,
  shouldSuppressUpiQr,
  customerBillHtml,
  receiptHtml,
} from '../src/services/receipt-print.ts';

// Sample base bill fixture
const baseBill = {
  id: 'bill-101-uuid',
  restaurantName: 'Tablo Heritage Restaurant',
  tableNumber: 4,
  groupLabel: '',
  status: 'Open',
  subtotal: 500,
  tax: 25,
  discount: 0,
  totalAmount: 525,
  remainingAmount: 525,
  orders: [
    {
      id: 'ord-1',
      orderNumber: 12,
      status: 'Served',
      items: [
        { id: 'item-1', name: 'Paneer Butter Masala', quantity: 1, unitPrice: 350 },
        { id: 'item-2', name: 'Garlic Naan', quantity: 2, unitPrice: 75 },
      ],
    },
  ],
  payments: [],
};

test('1. Dine-In Unpaid Bill: Generates scannable UPI QR with balance due and payee details', () => {
  const html = customerBillHtml(baseBill, {
    paperWidth: '80mm',
    payeeName: 'Tablo Heritage Restaurant',
    payeeVpa: 'tablo@okhdfcbank',
    upiReference: 'BILL-101-REF',
    channel: 'DineIn',
  });

  assert.ok(html.includes('CUSTOMER BILL / ESTIMATE'), 'Contains customer bill header');
  assert.ok(html.includes('Table 4'), 'Identifies table number');
  assert.ok(html.includes('Paneer Butter Masala'), 'Lists ordered items');
  assert.ok(html.includes('525.00'), 'Lists amount due');
  assert.ok(html.includes('<svg'), 'Contains SVG QR element');
  assert.ok(html.includes('tablo@okhdfcbank'), 'Displays payee VPA');
  assert.ok(html.includes('Open UPI App'), 'Includes Open UPI App deep link');
  assert.ok(html.includes('Ref: BILL-101-REF'), 'Displays unique reference');
});

test('2. Split Share Unpaid Bill: Generates unique QR with allocated share and share description', () => {
  const splitShareBill = {
    ...baseBill,
    id: 'split-share-b',
    shareDescription: 'Share 2 of 2 (Group B · 50%)',
    totalAmount: 262.5,
    remainingAmount: 262.5,
    subtotal: 250,
    tax: 12.5,
  };

  const html = customerBillHtml(splitShareBill, {
    paperWidth: '80mm',
    payeeName: 'Tablo Heritage Restaurant',
    payeeVpa: 'tablo@okhdfcbank',
    upiReference: 'SPLIT-B-REF',
  });

  assert.ok(html.includes('Share 2 of 2'), 'Shows split share description');
  assert.ok(html.includes('262.50'), 'Shows fractional split balance');
  assert.ok(html.includes('Ref: SPLIT-B-REF'), 'Displays unique split reference');
  assert.ok(html.includes('<svg'), 'Renders distinct QR for split share');
});

test('3. Parcel Counter Unpaid Bill: Formats takeaway bill with packaging and channel badge', () => {
  const parcelBill = {
    ...baseBill,
    id: 'parcel-bill-77',
    tableNumber: undefined,
    shareDescription: 'Token #TK-042 · Takeaway',
  };

  const html = customerBillHtml(parcelBill, {
    channel: 'Takeaway',
    paperWidth: '58mm',
  });

  assert.ok(html.includes('Parcel Counter'), 'Displays Parcel Counter channel badge');
  assert.ok(html.includes('Token #TK-042'), 'Shows takeaway token reference');
  assert.ok(html.includes('width: 58mm'), 'Applies 58mm CSS styling');
});

test('4. Car-Side Delivery Unpaid Bill: Renders car-side channel and QR for kerbside collection', () => {
  const carSideBill = {
    ...baseBill,
    id: 'carside-bill-88',
    tableNumber: undefined,
    shareDescription: 'Car-side Slot #B4 · White Swift (MH12-AB-1234)',
  };

  const html = customerBillHtml(carSideBill, {
    channel: 'CarSide',
    paperWidth: '80mm',
  });

  assert.ok(html.includes('Car-Side Delivery'), 'Displays Car-Side Delivery channel badge');
  assert.ok(html.includes('White Swift'), 'Shows vehicle description');
  assert.ok(html.includes('<svg'), 'Renders UPI QR for kerbside scan');
});

test('5. Partial Payment Update: Updates balance due and regenerates UPI QR with remaining balance', () => {
  const partiallyPaidBill = {
    ...baseBill,
    totalAmount: 525,
    remainingAmount: 325,
    payments: [
      {
        id: 'pay-1',
        method: 'Cash',
        amount: 200,
        collectedAtUtc: new Date().toISOString(),
      },
    ],
  };

  const html = customerBillHtml(partiallyPaidBill, {
    payeeVpa: 'tablo@okhdfcbank',
    payeeName: 'Tablo Heritage Restaurant',
  });

  assert.ok(html.includes('Payments Recorded'), 'Contains Payments Recorded section');
  assert.ok(html.includes('Cash'), 'Lists Cash payment');
  assert.ok(html.includes('200.00'), 'Lists ₹200.00 partial payment collected');
  assert.ok(html.includes('325.00'), 'Displays updated remaining balance due of ₹325.00');
  assert.ok(html.includes('Pay ₹325.00 with UPI'), 'UPI box reflects updated remaining amount');
  assert.ok(html.includes('am=325.00'), 'UPI deep link URI has am=325.00');
});

test('6. QR Suppression on Settled Bill: Settled bill suppresses QR code and shows Settled badge', () => {
  const settledBill = {
    ...baseBill,
    status: 'Settled',
    remainingAmount: 0,
    paymentMethod: 'UPI',
    payments: [{ id: 'p-1', method: 'UPI', amount: 525, collectedAtUtc: new Date().toISOString() }],
  };

  assert.equal(shouldSuppressUpiQr(settledBill), true, 'Suppresses QR logic');
  const html = customerBillHtml(settledBill);
  assert.ok(!html.includes('<div class="upi-box">'), 'No UPI box in settled bill');
  assert.ok(!html.includes('Pay ₹ with UPI'), 'No pay CTA');
  assert.ok(html.includes('PAID IN FULL · SETTLED'), 'Displays paid in full settlement note');
});

test('7. QR Suppression on Cancelled Bill: Cancelled bill suppresses QR code', () => {
  const cancelledBill = {
    ...baseBill,
    status: 'Cancelled',
  };

  assert.equal(shouldSuppressUpiQr(cancelledBill), true);
  const html = customerBillHtml(cancelledBill);
  assert.ok(!html.includes('<div class="upi-box">'));
});

test('8. QR Suppression on Zero-Balance Bill: Bill with ₹0 balance suppresses QR code', () => {
  const zeroBalanceBill = {
    ...baseBill,
    remainingAmount: 0,
  };

  assert.equal(shouldSuppressUpiQr(zeroBalanceBill), true);
  const html = customerBillHtml(zeroBalanceBill);
  assert.ok(!html.includes('<div class="upi-box">'));
});

test('9. QR Suppression on Split Parent Bill: Split parent suppresses QR to avoid paying parent bill', () => {
  const splitParentBill = {
    ...baseBill,
    splitAtUtc: new Date().toISOString(),
    isSplitParent: true,
  };

  assert.equal(shouldSuppressUpiQr(splitParentBill), true);
  const html = customerBillHtml(splitParentBill);
  assert.ok(!html.includes('<div class="upi-box">'), 'Suppresses QR on parent bill');
});

test('10. QR Suppression on Marketplace-Prepaid Bill: Marketplace order suppresses local UPI QR', () => {
  const swiggyBill = {
    ...baseBill,
    paymentMethod: 'MarketplaceCollected',
    channel: 'Swiggy',
  };

  assert.equal(shouldSuppressUpiQr(swiggyBill, 'Swiggy'), true);
  const html = customerBillHtml(swiggyBill, { channel: 'Swiggy' });
  assert.ok(!html.includes('<div class="upi-box">'), 'No local UPI QR on Swiggy prepaid order');
});

test('11. 58mm and 80mm CSS Media Rules: Verifies thermal print constraints', () => {
  const html58 = customerBillHtml(baseBill, { paperWidth: '58mm' });
  assert.ok(html58.includes('size: 58mm auto'), 'Includes 58mm page size');
  assert.ok(html58.includes('width: 58mm'), 'Constrains width to 58mm');

  const html80 = customerBillHtml(baseBill, { paperWidth: '80mm' });
  assert.ok(html80.includes('size: 80mm auto'), 'Includes 80mm page size');
  assert.ok(html80.includes('width: 80mm'), 'Constrains width to 80mm');
});

test('12. Legacy receiptHtml Backward Compatibility: Enforces settled bills only unless isEstimate option is passed', () => {
  const settledBill = {
    ...baseBill,
    status: 'Settled',
    settledAtUtc: new Date().toISOString(),
    paymentMethod: 'Cash',
  };

  // Settled bill succeeds
  const receipt = receiptHtml(settledBill);
  assert.ok(receipt.includes('Payment receipt'));

  // Unsettled bill without isEstimate throws Error
  assert.throws(() => receiptHtml({ ...baseBill, status: 'Open' }), /settled/i);

  // Unsettled bill WITH isEstimate renders customerBillHtml
  const estimate = receiptHtml({ ...baseBill, status: 'Open' }, { isEstimate: true });
  assert.ok(estimate.includes('CUSTOMER BILL / ESTIMATE'));
});
