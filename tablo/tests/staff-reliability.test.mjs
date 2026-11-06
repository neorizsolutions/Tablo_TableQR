import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import ts from "typescript";
import { QueryClient, QueryObserver } from "@tanstack/react-query";

// Load the real type-only helper modules in memory; no emitted files or added test dependencies.
async function productionModule(path) {
  const source = readFileSync(new URL(path, import.meta.url), "utf8");
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ESNext },
  });
  return import(`data:text/javascript;base64,${Buffer.from(outputText).toString("base64")}`);
}

for (const status of [401, 403, 500, undefined]) {
  test(`uncertain operation keeps its exact saved request after ${status ?? "network failure"}`, async () => {
    const { clearDefinitivelyFailedOperation } = await productionModule(
      "../src/services/pending-operation.ts",
    );
    const saved = JSON.stringify({
      operationId: "original-operation",
      items: [{ menuItemId: "meal", quantity: 2 }],
    });
    const data = new Map([["pending", saved]]);
    const cleared = clearDefinitivelyFailedOperation(
      { removeItem: (key) => data.delete(key) },
      "pending",
      status,
    );
    assert.equal(cleared, false);
    assert.equal(data.get("pending"), saved);
  });
}

test("a definitive validation rejection releases the draft for correction", async () => {
  const { clearDefinitivelyFailedOperation } = await productionModule(
    "../src/services/pending-operation.ts",
  );
  const data = new Map([["pending", "invalid draft"]]);
  assert.equal(
    clearDefinitivelyFailedOperation({ removeItem: (key) => data.delete(key) }, "pending", 400),
    true,
  );
  assert.equal(data.has("pending"), false);
});

test("staff update refetches customer quantities and total instead of overwriting only status", async () => {
  const { subscribeCustomerOrder } = await productionModule(
    "../src/services/customer-order-events.ts",
  );
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  let serverOrder = { status: "accepted", total: 105, items: [{ quantity: 1 }] };
  let event;
  let reconnect;
  const joined = [];
  const connection = {
    on: (name, handler) => {
      assert.equal(name, "OrderStatusUpdated");
      event = handler;
    },
    onreconnected: (handler) => {
      reconnect = handler;
    },
    invoke: async (...args) => joined.push(args),
  };
  const observer = new QueryObserver(client, {
    queryKey: ["order", "order-1"],
    queryFn: async () => serverOrder,
    staleTime: Infinity,
  });
  const unsubscribe = observer.subscribe(() => {});
  try {
    await observer.refetch();
    subscribeCustomerOrder(connection, client, "order-1");
    serverOrder = { status: "preparing", total: 210, items: [{ quantity: 2 }] };
    await event("ORDER-1", "Placed"); // A delayed status payload must not regress the server snapshot.
    assert.deepEqual(client.getQueryData(["order", "order-1"]), serverOrder);
    serverOrder = { status: "ready", total: 315, items: [{ quantity: 3 }] };
    await reconnect();
    assert.deepEqual(joined, [["JoinOrderGroup", "order-1"]]);
    assert.deepEqual(client.getQueryData(["order", "order-1"]), serverOrder);
  } finally {
    unsubscribe();
    observer.destroy();
    client.clear();
  }
});

test("another order's event does not refetch this customer's order", async () => {
  const { subscribeCustomerOrder } = await productionModule(
    "../src/services/customer-order-events.ts",
  );
  let event;
  let invalidations = 0;
  subscribeCustomerOrder(
    {
      on: (_, handler) => {
        event = handler;
      },
      onreconnected: () => {},
      invoke: async () => {},
    },
    {
      invalidateQueries: async () => {
        invalidations++;
      },
    },
    "order-1",
  );
  await event("order-2", "Ready");
  assert.equal(invalidations, 0);
});
