import type { TableBill } from "./service-desk";

const escape = (value: unknown) =>
  String(value ?? "").replace(
    /[&<>"']/g,
    (c) =>
      ({
        "&": "&amp;",
        "<": "&lt;",
        ">": "&gt;",
        '"': "&quot;",
        "'": "&#39;",
      })[c]!,
  );
const money = (value: number) =>
  new Intl.NumberFormat("en-IN", { style: "currency", currency: "INR" }).format(value);

export function receiptHtml(bill: TableBill): string {
  if (bill.status !== "Settled") throw new Error("Only settled bills have payment receipts.");
  const orders = bill.orders.filter((o) => o.status !== "Cancelled" && o.status !== "Rejected");
  return `<!doctype html><html><head><meta charset="utf-8"><title>Payment receipt ${escape(bill.id)}</title>
  <style>body{font:14px system-ui,sans-serif;color:#111;background:white;margin:20px}main{max-width:650px;margin:auto}h1{font-size:22px}h2{font-size:17px}table{width:100%;border-collapse:collapse}td,th{padding:8px 2px;text-align:left;border-bottom:1px solid #ddd}td:last-child,th:last-child{text-align:right}.total{font-size:18px;font-weight:bold}.meta{overflow-wrap:anywhere} @media print{body{margin:0}main{max-width:none}tr{break-inside:avoid}}</style></head><body><main>
  <h1>${escape(bill.restaurantName || "Restaurant identity unavailable (legacy receipt)")}</h1><h2>Payment receipt</h2>
  <p class="meta">Receipt: ${escape(bill.id)}<br>Table ${escape(String(bill.tableNumber)+(bill.groupLabel||""))}<br>
  ${escape(bill.settledAtUtc ? new Date(bill.settledAtUtc).toLocaleString("en-IN", { timeZone: "Asia/Kolkata" }) : "Timestamp unavailable")} · Asia/Kolkata<br>
  Method: ${escape(bill.paymentMethod || "Unavailable")}<br>Recorded employee: ${escape(bill.settledByEmployeeId || "Unavailable (legacy receipt)")}</p>
  <p>${escape(bill.shareDescription||"")}</p>
  ${orders.map((o) => `<h2>ORD-${escape(String(o.orderNumber).padStart(4, "0"))}</h2><table><thead><tr><th>Item</th><th>Qty</th><th>Amount</th></tr></thead><tbody>${o.items.map((i) => `<tr><td>${escape(i.name)}</td><td>${escape(i.quantity)}</td><td>${escape(money(i.quantity * i.unitPrice))}</td></tr>`).join("")}</tbody></table>`).join("")}
  <table><tbody><tr><td>Subtotal</td><td>${escape(money(bill.subtotal))}</td></tr><tr><td>Tax (recorded per order)</td><td>${escape(money(bill.tax))}</td></tr><tr><td>Discount</td><td>${escape(money(bill.discount))}</td></tr><tr class="total"><td>Total collected</td><td>${escape(money(bill.totalAmount))}</td></tr></tbody></table>
  ${(bill.payments||[]).map(p=>`<p>${escape(p.method)}: ${escape(money(p.amount))} · ${escape(new Date(p.collectedAtUtc).toLocaleString("en-IN",{timeZone:"Asia/Kolkata"}))}</p>`).join("")}
  <p>Payment collected externally. This receipt records the settlement; it does not confirm payment-provider processing.</p></main></body></html>`;
}
