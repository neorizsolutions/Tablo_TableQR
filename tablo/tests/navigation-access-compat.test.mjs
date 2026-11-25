import { test } from "node:test";
import assert from "node:assert/strict";
import { getNavigationAccess } from "../src/services/navigation-access.ts";
test("legacy navigation endpoint 404 retains role defaults but other errors fail closed", async () => {
  const previous = globalThis.fetch;
  try {
    globalThis.fetch = async () => new Response("{}", { status: 404 });
    assert.deepEqual(await getNavigationAccess(), { allowedMenuIds: null });
    for (const status of [401, 403, 500]) {
      globalThis.fetch = async () => new Response("{}", { status });
      await assert.rejects(getNavigationAccess(), (error) => error.status === status);
    }
    globalThis.fetch = async () => {
      throw new TypeError("Network unavailable");
    };
    await assert.rejects(getNavigationAccess(), /Network unavailable/);
  } finally {
    globalThis.fetch = previous;
  }
});
