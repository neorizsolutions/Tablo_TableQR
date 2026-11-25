import { test } from "node:test";
import assert from "node:assert/strict";
import {
  NAVIGATION_REGISTRY,
  getEmployeeNavItems,
  getRouteMenuItem,
} from "../src/config/navigation.ts";
test("custom access intersects roles and preserves management recovery", () => {
  assert.deepEqual(
    getEmployeeNavItems("Waiter", []).map((x) => x.id),
    [],
  );
  assert.deepEqual(
    getEmployeeNavItems("Waiter", ["staff-management", "kitchen-queue", "table-billing"]).map(
      (x) => x.id,
    ),
    ["table-billing"],
  );
  assert.deepEqual(
    getEmployeeNavItems("Admin", []).map((x) => x.id),
    ["staff-management"],
  );
  assert.ok(getEmployeeNavItems("Waiter", null).some((x) => x.id === "orders-servicedesk"));
});
test("route checks resolve default tabs and reject unknown tabs", () => {
  assert.equal(getRouteMenuItem("/dashboard", undefined, "Kitchen")?.id, "kitchen-queue");
  assert.equal(getRouteMenuItem("/admin", undefined, "Admin")?.id, "menu-management");
  assert.equal(getRouteMenuItem("/admin", "unknown", "Admin"), undefined);
  assert.equal(getRouteMenuItem("/dashboard", "billing", "Waiter")?.id, "table-billing");
});
test("every registered direct menu route resolves to its menu and role restrictions apply", () => {
  for (const item of NAVIGATION_REGISTRY) {
    const role = item.requiredRoles?.[0] || "Waiter";
    assert.equal(getRouteMenuItem(item.to, item.tabParam, role)?.id, item.id);
  }
  assert.ok(
    getEmployeeNavItems("Kitchen", null).every(
      (item) => !item.requiredRoles || item.requiredRoles.includes("Kitchen"),
    ),
  );
  assert.deepEqual(
    getEmployeeNavItems("Owner", []).map((x) => x.id),
    ["staff-management"],
  );
  assert.equal(getRouteMenuItem("/inventory", "not-a-menu", "Admin"), undefined);
});
