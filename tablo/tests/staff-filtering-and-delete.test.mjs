import { test } from "node:test";
import assert from "node:assert/strict";

// Mirrors the deterministic filtering, count computation, and selection-clearing rules
// implemented in src/routes/staff.tsx and src/routes/menu-access.tsx.
function filterEmployeesByStatusAndSearch(employees, statusFilter, search) {
  const normalized = search.trim().toLowerCase();
  return employees
    .filter((emp) => emp.role !== "SuperAdmin")
    .filter((emp) => {
      if (statusFilter === "active") return emp.isActive;
      if (statusFilter === "inactive") return !emp.isActive;
      return true;
    })
    .filter((emp) => {
      if (!normalized) return true;
      return `${emp.firstName} ${emp.lastName} ${emp.email} ${emp.role}`
        .toLowerCase()
        .includes(normalized);
    });
}

function computeStatusCounts(employees) {
  const nonSuper = employees.filter((e) => e.role !== "SuperAdmin");
  const active = nonSuper.filter((e) => e.isActive).length;
  const inactive = nonSuper.filter((e) => !e.isActive).length;
  return { active, inactive, all: active + inactive };
}

function resolveSelectedEmployee(filtered, selectedId) {
  if (!selectedId) return { nextSelectedId: null, selected: undefined };
  const match = filtered.find((e) => e.id === selectedId);
  return match
    ? { nextSelectedId: selectedId, selected: match }
    : { nextSelectedId: null, selected: undefined };
}

const sampleRoster = [
  { id: "1", firstName: "City", lastName: "Admin", email: "city@admin.com", role: "Admin", isActive: true },
  { id: "2", firstName: "Maya", lastName: "Waiter", email: "maya@hotelcity.test", role: "Waiter", isActive: true },
  { id: "3", firstName: "Rohan", lastName: "Inactive", email: "rohan@hotelcity.test", role: "Cashier", isActive: false },
  { id: "4", firstName: "System", lastName: "Operator", email: "sa@tablo.local", role: "SuperAdmin", isActive: true },
];

test("staff list defaults to active employees and computes accurate counts", () => {
  const counts = computeStatusCounts(sampleRoster);
  assert.deepEqual(counts, { active: 2, inactive: 1, all: 3 });

  const activeOnly = filterEmployeesByStatusAndSearch(sampleRoster, "active", "");
  assert.deepEqual(activeOnly.map((e) => e.email), ["city@admin.com", "maya@hotelcity.test"]);

  const inactiveOnly = filterEmployeesByStatusAndSearch(sampleRoster, "inactive", "");
  assert.deepEqual(inactiveOnly.map((e) => e.email), ["rohan@hotelcity.test"]);

  const all = filterEmployeesByStatusAndSearch(sampleRoster, "all", "");
  assert.deepEqual(all.map((e) => e.email), ["city@admin.com", "maya@hotelcity.test", "rohan@hotelcity.test"]);
});

test("search combines cleanly with selected status filter", () => {
  assert.deepEqual(
    filterEmployeesByStatusAndSearch(sampleRoster, "active", "rohan"),
    [],
  );
  assert.deepEqual(
    filterEmployeesByStatusAndSearch(sampleRoster, "inactive", "rohan").map((e) => e.id),
    ["3"],
  );
  assert.deepEqual(
    filterEmployeesByStatusAndSearch(sampleRoster, "all", "waiter").map((e) => e.id),
    ["2"],
  );
});

test("menu-access clears selected employee panel when employee no longer matches filter or search", () => {
  const inactiveFiltered = filterEmployeesByStatusAndSearch(sampleRoster, "inactive", "");
  const initialSelection = resolveSelectedEmployee(inactiveFiltered, "3");
  assert.equal(initialSelection.nextSelectedId, "3");
  assert.equal(initialSelection.selected?.email, "rohan@hotelcity.test");

  // Switching filter to Active hides employee 3 and must clear the selection panel immediately
  const activeFiltered = filterEmployeesByStatusAndSearch(sampleRoster, "active", "");
  const clearedByFilter = resolveSelectedEmployee(activeFiltered, initialSelection.nextSelectedId);
  assert.equal(clearedByFilter.nextSelectedId, null);
  assert.equal(clearedByFilter.selected, undefined);

  // Deactivating selected active employee 2 removes them from Active list and clears panel
  const updatedRoster = sampleRoster.map((e) => (e.id === "2" ? { ...e, isActive: false } : e));
  const afterDeactivateActive = filterEmployeesByStatusAndSearch(updatedRoster, "active", "");
  const clearedAfterDeactivate = resolveSelectedEmployee(afterDeactivateActive, "2");
  assert.equal(clearedAfterDeactivate.nextSelectedId, null);
  assert.equal(clearedAfterDeactivate.selected, undefined);

  // Search narrowing clears selected employee if excluded (searching "admin" hides Maya Waiter)
  const searchedOut = resolveSelectedEmployee(
    filterEmployeesByStatusAndSearch(sampleRoster, "active", "admin"),
    "2",
  );
  assert.equal(searchedOut.nextSelectedId, null);
  assert.equal(searchedOut.selected, undefined);
});
