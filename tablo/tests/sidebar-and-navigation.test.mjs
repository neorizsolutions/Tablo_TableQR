import { test } from "node:test";
import assert from "node:assert/strict";
import { NAVIGATION_REGISTRY } from "../src/config/navigation.ts";

function computeActiveItem(pathname, search) {
  const currentTab = search?.tab;
  const activeItems = NAVIGATION_REGISTRY.filter((item) => {
    const isPathMatch = pathname === item.to;
    if (!isPathMatch) return false;
    if (item.tabParam) {
      return currentTab === item.tabParam || (!currentTab && !!item.isDefault);
    }
    return !currentTab || !!item.isDefault || !item.tabParam;
  });
  return activeItems;
}

test("1. Sidebar highlights exactly one item on /admin (default menu view)", () => {
  const active = computeActiveItem("/admin", {});
  assert.equal(active.length, 1);
  assert.equal(active[0].id, "menu-management");
  assert.equal(active[0].label, "Menu & Categories");
});

test("2. Sidebar highlights exactly one item on /admin?tab=tables", () => {
  const active = computeActiveItem("/admin", { tab: "tables" });
  assert.equal(active.length, 1);
  assert.equal(active[0].id, "tables-qr");
  assert.equal(active[0].label, "Tables & QR Codes");
});

test("3. Sidebar highlights exactly one item on /admin?tab=menu", () => {
  const active = computeActiveItem("/admin", { tab: "menu" });
  assert.equal(active.length, 1);
  assert.equal(active[0].id, "menu-management");
});

test("4. Sidebar highlights exactly one item on /inventory (default stock view)", () => {
  const active = computeActiveItem("/inventory", {});
  assert.equal(active.length, 1);
  assert.equal(active[0].id, "inventory-stock");
  assert.equal(active[0].label, "Ingredients & Stock");
});

test("Inventory preparations, suppliers and stock movements each have a direct sidebar entry", () => {
  for (const tab of ["preparations", "suppliers", "movements"]) {
    const active = computeActiveItem("/inventory", { tab });
    assert.equal(active.length, 1);
    assert.equal(active[0].search.tab, tab);
  }
});

test("5. Sidebar highlights exactly one item on /inventory?tab=purchasing", () => {
  const active = computeActiveItem("/inventory", { tab: "purchasing" });
  assert.equal(active.length, 1);
  assert.equal(active[0].id, "purchasing-orders");
  assert.equal(active[0].label, "Purchasing & POs");
});

test("6. Sidebar highlights exactly one item on /inventory?tab=lots", () => {
  const active = computeActiveItem("/inventory", { tab: "lots" });
  assert.equal(active.length, 1);
  assert.equal(active[0].id, "lots-expiry");
  assert.equal(active[0].label, "Lots & Expiry");
});

test("7. Sidebar highlights exactly one item on /inventory?tab=transfers", () => {
  const active = computeActiveItem("/inventory", { tab: "transfers" });
  assert.equal(active.length, 1);
  assert.equal(active[0].id, "outlet-transfers");
  assert.equal(active[0].label, "Outlet Transfers");
});

test("8. Sidebar highlights exactly one item on /dashboard (default Floor & Orders)", () => {
  const active = computeActiveItem("/dashboard", {});
  assert.equal(active.length, 1);
  assert.equal(active[0].id, "orders-servicedesk");
  assert.equal(active[0].label, "Floor & Orders");
});

test("9. Sidebar highlights dedicated Staff Order Taking on /dashboard?tab=order", () => {
  const active = computeActiveItem("/dashboard", { tab: "order" });
  assert.equal(active.length, 1);
  assert.equal(active[0].id, "staff-order-taking");
  assert.equal(active[0].label, "Staff Order Taking");
  assert.ok(active[0].requiredRoles.includes("Waiter"));
  assert.ok(active[0].requiredRoles.includes("Admin"));
});

test("10. Sidebar highlights Kitchen Queue on /dashboard?tab=kitchen", () => {
  const active = computeActiveItem("/dashboard", { tab: "kitchen" });
  assert.equal(active.length, 1);
  assert.equal(active[0].id, "kitchen-queue");
  assert.equal(active[0].label, "Kitchen Queue");
});

test("11. Sidebar highlights Payment Reports on /reports", () => {
  const active = computeActiveItem("/reports", {});
  assert.equal(active.length, 1);
  assert.equal(active[0].id, "financial-reports");
  assert.equal(active[0].label, "Payment Reports");
});
