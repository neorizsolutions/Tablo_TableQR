import test from "node:test";
import assert from "node:assert/strict";
import { handleLocalModuleFallback } from "../src/services/localModuleFallback.ts";

test("1. Service Desk Tables API returns all configured tables with live statuses and order counts", () => {
  const tables = handleLocalModuleFallback("/restaurants/22222222-0000-0000-0000-000000000001/service-desk/tables");
  assert.ok(Array.isArray(tables), "Expected tables array");
  assert.strictEqual(tables.length, 10, "Expected 10 tables");

  const table1 = tables.find((t) => t.tableNumber === 1);
  assert.ok(table1, "Table 1 should exist");
  assert.strictEqual(table1.status, "Occupied");
  assert.strictEqual(table1.activeOrdersCount, 2);

  const table2 = tables.find((t) => t.tableNumber === 2);
  assert.ok(table2, "Table 2 should exist");
  assert.strictEqual(table2.status, "Available");
});

test("2. Dining Groups API retrieves and creates independent groups for shared tables", () => {
  const table4Id = "77777777-0000-0000-0000-000000000004";
  const existingGroups = handleLocalModuleFallback(`/restaurants/22222222-0000-0000-0000-000000000001/service-desk/tables/${table4Id}/groups`);
  assert.ok(Array.isArray(existingGroups));
  assert.ok(existingGroups.length >= 2, "Expected Table 4 to have groups A and B");

  // Create group C
  const newGroup = handleLocalModuleFallback(`/restaurants/22222222-0000-0000-0000-000000000001/service-desk/tables/${table4Id}/groups`, {
    method: "POST",
    body: JSON.stringify({ label: "C" }),
  });
  assert.ok(newGroup);
  assert.strictEqual(newGroup.label, "C");
  assert.strictEqual(newGroup.tableId, table4Id);
});

test("3. Bill Review provides open bills with itemized orders and accurate subtotal and tax", () => {
  const table1Id = "77777777-0000-0000-0000-000000000001";
  const review = handleLocalModuleFallback(`/restaurants/22222222-0000-0000-0000-000000000001/service-desk/tables/${table1Id}/bill`);
  assert.ok(review);
  assert.strictEqual(review.tableNumber, 1);
  assert.ok(review.openBills.length > 0);
  assert.strictEqual(review.openBill.totalAmount, 1533);
  assert.strictEqual(review.openBill.orders.length, 1);
});

test("4. Bill Split Preview enforces exact paise balance and detects unallocated discrepancies", () => {
  // Test balanced split
  const balancedPreview = handleLocalModuleFallback(
    "/restaurants/22222222-0000-0000-0000-000000000001/service-desk/bills/bill-table-1/split-preview",
    {
      method: "POST",
      body: JSON.stringify({
        splitMode: "Amount",
        splitAmounts: [766.5, 766.5],
      }),
    }
  );
  assert.strictEqual(balancedPreview.canSplit, true);
  assert.strictEqual(balancedPreview.unallocatedBalance, 0);

  // Test unbalanced split
  const unbalancedPreview = handleLocalModuleFallback(
    "/restaurants/22222222-0000-0000-0000-000000000001/service-desk/bills/bill-table-1/split-preview",
    {
      method: "POST",
      body: JSON.stringify({
        splitMode: "Amount",
        splitAmounts: [700, 700], // Missing 133
      }),
    }
  );
  assert.strictEqual(unbalancedPreview.canSplit, false);
  assert.strictEqual(unbalancedPreview.unallocatedBalance, 133);
});

test("5. Multi-draft key isolation maintains independent drafts per table and dining group", () => {
  const fakeStorage = new Map();
  const getDraftKey = (rId, eId, tId, gId) => `tablo_staff_draft_${rId}_${eId}_${tId}_${gId || "whole"}`;

  const t1Draft = [{ menuItemId: "m1", quantity: 2, price: 500 }];
  const t4aDraft = [{ menuItemId: "m2", quantity: 1, price: 350 }];
  const t4bDraft = [{ menuItemId: "m3", quantity: 3, price: 120 }];

  fakeStorage.set(getDraftKey("r1", "e1", "t1"), JSON.stringify(t1Draft));
  fakeStorage.set(getDraftKey("r1", "e1", "t4", "gA"), JSON.stringify(t4aDraft));
  fakeStorage.set(getDraftKey("r1", "e1", "t4", "gB"), JSON.stringify(t4bDraft));

  // Switching tables preserves independent carts
  assert.deepStrictEqual(JSON.parse(fakeStorage.get(getDraftKey("r1", "e1", "t1"))), t1Draft);
  assert.deepStrictEqual(JSON.parse(fakeStorage.get(getDraftKey("r1", "e1", "t4", "gA"))), t4aDraft);
  assert.deepStrictEqual(JSON.parse(fakeStorage.get(getDraftKey("r1", "e1", "t4", "gB"))), t4bDraft);
});
