import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import ts from "typescript";

async function load(path) {
  const source = readFileSync(new URL(path, import.meta.url), "utf8");
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ESNext },
  });
  return import(`data:text/javascript;base64,${Buffer.from(outputText).toString("base64")}`);
}

test("printed receipt escapes names, uses recorded amounts and excludes cancelled amounts", async () => {
  const { receiptHtml } = await load("../src/services/receipt-print.ts");
  const html = receiptHtml({
    id: "receipt-1",
    restaurantName: "<script>alert(1)</script>",
    status: "Settled",
    tableNumber: 4,
    totalAmount: 3,
    subtotal: 3,
    tax: 0,
    discount: 0,
    paymentMethod: "Cash",
    settledAtUtc: "2026-11-03T18:30:00Z",
    orders: [
      {
        id: "o",
        orderNumber: 1,
        status: "Served",
        items: [{ id: "i", name: "Coffee & milk", quantity: 1, unitPrice: 3 }],
      },
      {
        id: "x",
        orderNumber: 2,
        status: "Cancelled",
        items: [{ id: "j", name: "Excluded meal", quantity: 1, unitPrice: 999 }],
      },
    ],
  });
  assert.ok(!html.includes("<script>"));
  assert.ok(html.includes("&lt;script&gt;"));
  assert.ok(html.includes("Coffee &amp; milk"));
  assert.ok(!html.includes("Excluded meal"));
  assert.ok(html.includes("3.00"));
  assert.ok(html.includes("Asia/Kolkata"));
  assert.ok(!html.includes("Send items"));
  assert.ok(html.includes("Payment receipt"));
});

test("legacy receipt identifies missing historical profile rather than inventing it", async () => {
  const { receiptHtml } = await load("../src/services/receipt-print.ts");
  const html = receiptHtml({
    id: "old",
    status: "Settled",
    tableNumber: 1,
    totalAmount: 0,
    subtotal: 0,
    tax: 0,
    discount: 0,
    orders: [],
  });
  assert.ok(html.includes("Restaurant identity unavailable"));
});

test("unsettled bills cannot be printed as payment receipts", async () => {
  const { receiptHtml } = await load("../src/services/receipt-print.ts");
  assert.throws(() => receiptHtml({ status: "Open" }), /settled/i);
});
