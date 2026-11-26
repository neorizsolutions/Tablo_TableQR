import { test } from "node:test";
import assert from "node:assert/strict";
import {
  NAVIGATION_REGISTRY,
  getEmployeeNavItems,
} from "../src/config/navigation.ts";

export const EXPECTED_ROLE_DEFAULTS = {
  Waiter: ["overview", "orders-servicedesk", "staff-order-taking", "table-billing"],
  Kitchen: ["overview", "kitchen-queue"],
  Cashier: ["cashier-billing"],
  Admin: NAVIGATION_REGISTRY.map((item) => item.id),
  Owner: NAVIGATION_REGISTRY.map((item) => item.id),
};

test("role defaults without custom overrides match baseline source of truth", () => {
  for (const [role, expectedIds] of Object.entries(EXPECTED_ROLE_DEFAULTS)) {
    const actualIds = getEmployeeNavItems(role, null).map((item) => item.id);
    assert.deepEqual(
      actualIds,
      expectedIds,
      `Expected default menus for role ${role} when allowedMenuIds is null`,
    );
  }
});

test("role defaults remain distinct per operational role and exclude platform menus", () => {
  for (const role of ["Waiter", "Kitchen", "Cashier", "Admin", "Owner"]) {
    const ids = getEmployeeNavItems(role, null).map((item) => item.id);
    assert.ok(!ids.some((id) => id.startsWith("sa-")), `${role} must never receive Super Admin menus`);
  }
  assert.ok(!getEmployeeNavItems("Waiter", null).some((item) => item.id === "staff-management"));
  assert.ok(!getEmployeeNavItems("Waiter", null).some((item) => item.id === "staff-menu-access"));
  assert.ok(!getEmployeeNavItems("Waiter", null).some((item) => item.id === "support"));
  assert.ok(!getEmployeeNavItems("Kitchen", null).some((item) => item.id === "staff-management"));
  assert.ok(!getEmployeeNavItems("Cashier", null).some((item) => item.id === "staff-management"));
});
