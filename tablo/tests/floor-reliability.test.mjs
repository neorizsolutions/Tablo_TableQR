import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import ts from "typescript";

async function loadHook(request, storage) {
  class ServiceDeskError extends Error { constructor(status) { super(`HTTP ${status}`); this.status = status; } }
  globalThis.localStorage = storage;
  globalThis.__floorTest = {
    useRef: value => ({ current: value }),
    useState: value => [typeof value === "function" ? value() : value, () => {}],
    serviceDeskRequest: request,
    ServiceDeskError,
  };
  const source = readFileSync(new URL("../src/hooks/use-floor-operation.ts", import.meta.url), "utf8");
  const { outputText } = ts.transpileModule(source, { compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ESNext } });
  const code = `const {useRef,useState,serviceDeskRequest,ServiceDeskError}=globalThis.__floorTest;\n${outputText.replace(/^import .*;\r?$/gm, "")}\n// ${Math.random()}`;
  return { ...(await import(`data:text/javascript;base64,${Buffer.from(code).toString("base64")}`)), ServiceDeskError };
}
function store() {
  const data = new Map();
  return { data, getItem: key => data.get(key) ?? null, setItem: (key, value) => data.set(key, value), removeItem: key => data.delete(key) };
}

test("floor action does not send when durable browser storage fails and releases its click guard", async () => {
  const storage = store(); let calls = 0; let unavailable = true;
  const set = storage.setItem;
  storage.setItem = (...args) => { if (unavailable) throw new Error("Quota exceeded"); set(...args); };
  const { useFloorOperation } = await loadHook(async () => { calls++; }, storage);
  const hook = useFloorOperation("restaurant", "employee", "bill", async () => {});
  await hook.run("bills/one/payments", { amount: 20 });
  assert.equal(calls, 0);
  unavailable = false;
  await hook.run("bills/one/payments", { amount: 20 });
  assert.equal(calls, 1);
  assert.equal(storage.data.size, 0);
});

for (const status of [401, 403, 500, undefined]) {
  test(`floor recovery replays exact saved request after ${status ?? "network failure"}`, async () => {
    const storage = store(); const calls = []; let failure;
    const loaded = await loadHook(async (restaurant, path, body) => { calls.push({ restaurant, path, body }); if (calls.length === 1) throw failure; }, storage);
    failure = status ? new loaded.ServiceDeskError(status) : new Error("Disconnected");
    const hook = loaded.useFloorOperation("restaurant", "employee", "bill", async () => {});
    await hook.run("bills/one/payments", { amount: 20, method: "Cash" });
    assert.equal(storage.data.size, 1);
    await hook.run("bills/one/payments", { amount: 99, method: "Card" });
    assert.deepEqual(calls[1], calls[0]);
    assert.ok(calls[0].body.operationId);
    assert.equal(storage.data.size, 0);
  });
}

test("double click sends one floor action while the first request is outstanding", async () => {
  const storage = store(); let calls = 0; let finish;
  const { useFloorOperation } = await loadHook(() => { calls++; return new Promise(resolve => { finish = resolve; }); }, storage);
  const hook = useFloorOperation("restaurant", "employee", "bill", async () => {});
  const first = hook.run("bills/one/payments", { amount: 20 });
  await hook.run("bills/one/payments", { amount: 20 });
  assert.equal(calls, 1);
  finish(); await first;
});
