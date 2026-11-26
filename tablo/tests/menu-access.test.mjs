import { test } from "node:test";
import assert from "node:assert/strict";
import {
  ADMIN_ONLY_MENU_IDS,
  NAVIGATION_REGISTRY,
  getAssignableMenuItems,
  getDashboardTab,
  getEmployeeNavItems,
  getMenuRequiredPlanLabel,
  getRouteMenuItem,
} from "../src/config/navigation.ts";

test("custom access permits cross-role assignable menus while blocking admin-only and preserving recovery", () => {
  assert.deepEqual(
    getEmployeeNavItems("Waiter", []).map((x) => x.id),
    [],
  );
  assert.deepEqual(
    getEmployeeNavItems("Waiter", ["staff-management", "kitchen-queue", "table-billing", "support"]).map(
      (x) => x.id,
    ),
    ["kitchen-queue", "table-billing"],
  );
  assert.deepEqual(
    getEmployeeNavItems("Admin", []).map((x) => x.id),
    ["staff-management", "staff-menu-access"],
  );
  assert.ok(getEmployeeNavItems("Waiter", null).some((x) => x.id === "orders-servicedesk"));
});

test("route checks resolve default tabs, explicit custom tabs for Kitchen/Waiter, and reject unknown tabs", () => {
  assert.equal(getRouteMenuItem("/dashboard", undefined, "Kitchen")?.id, "kitchen-queue");
  assert.equal(getRouteMenuItem("/dashboard", "order", "Kitchen", ["kitchen-queue", "staff-order-taking"])?.id, "staff-order-taking");
  assert.equal(getRouteMenuItem("/dashboard", undefined, "Kitchen", ["table-billing"])?.id, "table-billing");
  assert.equal(getDashboardTab("floor", "Kitchen", ["orders-servicedesk"]), "floor");
  assert.equal(getRouteMenuItem("/admin", undefined, "Admin")?.id, "menu-management");
  assert.equal(getRouteMenuItem("/admin", "unknown", "Admin"), undefined);
  assert.equal(getRouteMenuItem("/dashboard", "billing", "Waiter")?.id, "table-billing");
});

test("every registered direct menu route resolves to its menu and assignable list covers full restaurant workspace", () => {
  for (const item of NAVIGATION_REGISTRY) {
    const role = item.requiredRoles?.[0] || "Waiter";
    assert.equal(getRouteMenuItem(item.to, item.tabParam, role)?.id, item.id);
  }
  const waiterAssignable = getAssignableMenuItems("Waiter").map((i) => i.id);
  const kitchenAssignable = getAssignableMenuItems("Kitchen").map((i) => i.id);
  const cashierAssignable = getAssignableMenuItems("Cashier").map((i) => i.id);
  const expectedOperational = NAVIGATION_REGISTRY
    .filter((item) => !ADMIN_ONLY_MENU_IDS.has(item.id))
    .map((item) => item.id);
  assert.deepEqual(waiterAssignable, expectedOperational);
  assert.deepEqual(kitchenAssignable, expectedOperational);
  assert.deepEqual(cashierAssignable, expectedOperational);
  assert.deepEqual(
    getEmployeeNavItems("Owner", []).map((x) => x.id),
    ["staff-management", "staff-menu-access"],
  );
  assert.equal(getRouteMenuItem("/inventory", "not-a-menu", "Admin"), undefined);
});

test("dedicated staff menu access and support are restaurant-admin only", () => {
  assert.equal(getRouteMenuItem("/menu-access", undefined, "Admin")?.id, "staff-menu-access");
  assert.ok(getEmployeeNavItems("Admin", null).some((item) => item.id === "staff-menu-access"));
  assert.ok(!getEmployeeNavItems("Waiter", null).some((item) => item.id === "staff-menu-access"));
  assert.deepEqual(
    getEmployeeNavItems("Waiter", ["staff-menu-access", "staff-management", "support"]),
    [],
  );
});

test("Cashier defaults only to cashier billing while allowing explicit cross-role operational grants", () => {
  assert.deepEqual(
    getEmployeeNavItems("Cashier", null).map((item) => item.id),
    ["cashier-billing"],
  );
  assert.deepEqual(
    getEmployeeNavItems("Cashier", [
      "overview",
      "staff-order-taking",
      "parcel-counter",
      "cashier-billing",
    ]).map((item) => item.id),
    ["overview", "cashier-billing", "staff-order-taking", "parcel-counter"],
  );
  assert.deepEqual(
    getEmployeeNavItems("Cashier", []).map((item) => item.id),
    [],
  );
  assert.equal(getRouteMenuItem("/cashier", undefined, "Cashier")?.id, "cashier-billing");
  assert.ok(getEmployeeNavItems("Admin", null).some((item) => item.id === "cashier-billing"));
  assert.ok(!getEmployeeNavItems("Waiter", null).some((item) => item.id === "cashier-billing"));
});

test("plan requirements are accurately labeled for gated menus", () => {
  const parcel = NAVIGATION_REGISTRY.find((x) => x.id === "parcel-counter");
  const inv = NAVIGATION_REGISTRY.find((x) => x.id === "inventory-stock");
  const overview = NAVIGATION_REGISTRY.find((x) => x.id === "overview");
  assert.equal(getMenuRequiredPlanLabel(parcel), "Requires Basic plan");
  assert.equal(getMenuRequiredPlanLabel(inv), "Requires Premium plan");
  assert.equal(getMenuRequiredPlanLabel(overview), null);
});
