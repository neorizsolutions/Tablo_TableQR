import { test } from "node:test";
import assert from "node:assert/strict";
import {
  MENU_SECTIONS,
  MENU_SECTION_LABELS,
  NAVIGATION_REGISTRY,
  SUPER_ADMIN_NAVIGATION,
  getDashboardTab,
  getEmployeeNavItems,
  isNavigationItemActive,
} from "../src/config/navigation.ts";

function computeActiveItem(pathname, search) {
  return NAVIGATION_REGISTRY.filter((item) => isNavigationItemActive(item, pathname, search.tab));
}

test("Kitchen staff land in the kitchen queue on the dashboard", () => {
  const tab = getDashboardTab(undefined, "Kitchen");
  assert.equal(tab, "kitchen");
  assert.equal(computeActiveItem("/dashboard", { tab })[0].id, "kitchen-queue");
});

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

test("12. Sidebar and navigation registry follow daily operational priority with zero duplicates", () => {
  const ids = NAVIGATION_REGISTRY.map((item) => item.id);
  assert.equal(new Set(ids).size, ids.length, "NAVIGATION_REGISTRY must not contain duplicate IDs");

  assert.deepEqual(MENU_SECTIONS, ["operations", "inventory", "analytics", "management", "utility"]);
  assert.equal(MENU_SECTION_LABELS.operations, "Daily Operations");
  assert.equal(MENU_SECTION_LABELS.management, "Restaurant Administration");

  const expectedOrderedIds = [
    // Daily Operations
    "overview",
    "staff-order-taking",
    "orders-servicedesk",
    "kitchen-queue",
    "cashier-billing",
    "table-billing",
    "parcel-counter",
    "marketplace-automation",
    // Inventory & Stock
    "inventory-stock",
    "inventory-recipes",
    "inventory-preparations",
    "purchasing-orders",
    "inventory-suppliers",
    "inventory-movements",
    "lots-expiry",
    "outlet-transfers",
    "inventory-stocktakes",
    "central-kitchen",
    // Reports & Analytics
    "financial-reports",
    "stock-valuation-report",
    "gross-profit-report",
    // Restaurant Administration
    "menu-management",
    "tables-qr",
    "crm-loyalty",
    "storefront-settings",
    "staff-management",
    "staff-menu-access",
    // Bottom utility
    "support",
  ];
  assert.deepEqual(ids, expectedOrderedIds);
});

test("13. Custom overrides and role defaults respect operational priority and Super Admin separation", () => {
  const customWaiter = getEmployeeNavItems("Waiter", [
    "menu-management",
    "cashier-billing",
    "staff-order-taking",
  ]);
  assert.deepEqual(
    customWaiter.map((x) => x.id),
    ["staff-order-taking", "cashier-billing", "menu-management"],
  );
  assert.ok(SUPER_ADMIN_NAVIGATION.every((item) => item.section === "platform"));
});
