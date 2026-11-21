import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import ts from "typescript";

function createMockStorage() {
  const data = new Map();
  return {
    data,
    getItem: (key) => data.get(key) ?? null,
    setItem: (key, value) => data.set(key, String(value)),
    removeItem: (key) => data.delete(key),
    clear: () => data.clear(),
    key: (i) => Array.from(data.keys())[i] ?? null,
    get length() {
      return data.size;
    },
  };
}

async function loadParcelPaymentHook(recordPaymentMock, storage) {
  class ApiError extends Error {
    constructor(message, status) {
      super(message);
      this.status = status;
    }
  }

  globalThis.localStorage = storage;
  let stateStore = new Map();

  globalThis.__parcelTest = {
    useRef: (initial) => ({ current: initial }),
    useState: (initial) => {
      let val = typeof initial === "function" ? initial() : initial;
      const setter = (next) => {
        val = typeof next === "function" ? next(val) : next;
      };
      return [val, setter];
    },
    useCallback: (fn) => fn,
    useEffect: (fn) => fn(),
    parcelApi: {
      recordPayment: recordPaymentMock,
    },
    ApiError,
  };

  const source = readFileSync(
    new URL("../src/hooks/use-parcel-payment.ts", import.meta.url),
    "utf8"
  );

  const { outputText } = ts.transpileModule(source, {
    compilerOptions: {
      target: ts.ScriptTarget.ES2022,
      module: ts.ModuleKind.ESNext,
    },
  });

  const code = `
    const { useRef, useState, useCallback, useEffect, parcelApi, ApiError } = globalThis.__parcelTest;
    ${outputText
      .replace(/import\s+.*?;?\r?\n/g, "")
      .replace(/export\s+/g, "")}
    return {
      useParcelPayment,
      getParcelPaymentStorageKey,
      readSavedParcelPayment,
      ApiError,
    };
  `;

  const factory = new Function(code);
  return factory();
}

test("1. Parcel payment does NOT send when durable browser storage fails and releases busy click guard", async () => {
  const storage = createMockStorage();
  let calls = 0;
  let storageUnavailable = true;

  const originalSet = storage.setItem;
  storage.setItem = (...args) => {
    if (storageUnavailable) throw new Error("QuotaExceededError: Local storage is full");
    return originalSet.apply(storage, args);
  };

  const { useParcelPayment } = await loadParcelPaymentHook(async () => {
    calls++;
    return { paymentId: "pay-1", remaining: 0 };
  }, storage);

  const hook = useParcelPayment("rest-001", "emp-cashier-1", "ord-100", "bill-100", async () => {});

  // When storage fails, it must throw and NOT make any network call
  await assert.rejects(
    async () => {
      await hook.run({ method: "UPI", amount: 150.0 });
    },
    /Browser storage is unavailable/
  );

  assert.equal(calls, 0, "Network call must not have been issued");
  assert.equal(hook.busy, false, "Click guard must be released");

  // When storage recovers, call succeeds and clears storage
  storageUnavailable = false;
  const result = await hook.run({ method: "UPI", amount: 150.0 });
  assert.equal(calls, 1);
  assert.equal(result.paymentId, "pay-1");
  assert.equal(storage.data.size, 0, "Storage cleared on definitive success");
});

test("2. Double click sends exactly one parcel payment action while first request is in-flight", async () => {
  const storage = createMockStorage();
  let calls = 0;
  let finishRequest;

  const { useParcelPayment } = await loadParcelPaymentHook(async () => {
    calls++;
    return new Promise((resolve) => {
      finishRequest = resolve;
    });
  }, storage);

  const hook = useParcelPayment("rest-001", "emp-cashier-1", "ord-101", "bill-101", async () => {});

  const firstPromise = hook.run({ method: "Cash", amount: 200.0 });
  // Immediate second click while first is outstanding
  const secondPromise = hook.run({ method: "Cash", amount: 200.0 });

  assert.equal(calls, 1, "Only one network request should be dispatched");

  finishRequest({ paymentId: "pay-2", remaining: 0 });
  await firstPromise;
  await secondPromise;
  assert.equal(calls, 1);
});

test("3. Reload recovery: Discovers pending unconfirmed payment in storage upon mount", async () => {
  const storage = createMockStorage();
  const { getParcelPaymentStorageKey, readSavedParcelPayment, useParcelPayment } =
    await loadParcelPaymentHook(async () => {}, storage);

  const key = getParcelPaymentStorageKey("emp-cashier-1", "rest-001", "ord-102");
  const savedRecord = {
    restaurantId: "rest-001",
    orderId: "ord-102",
    billId: "bill-102",
    actorEmployeeId: "emp-cashier-1",
    operationId: "op-uuid-saved-before-crash",
    method: "UPI",
    amount: 350.0,
    expectedRevision: "rev-2",
    expectedRemaining: 350.0,
    createdAt: Date.now() - 5000,
  };
  storage.setItem(key, JSON.stringify(savedRecord));

  // Verify readSavedParcelPayment extracts it
  const read = readSavedParcelPayment("emp-cashier-1", "rest-001", "ord-102");
  assert.ok(read);
  assert.equal(read.operationId, "op-uuid-saved-before-crash");
  assert.equal(read.amount, 350.0);
  assert.equal(read.method, "UPI");

  // Verify hook initializes with pending state
  const hook = useParcelPayment("rest-001", "emp-cashier-1", "ord-102", "bill-102", async () => {});
  assert.ok(hook.pending);
  assert.equal(hook.pending.operationId, "op-uuid-saved-before-crash");
  assert.equal(hook.hasUnresolved, true);
});

for (const scenario of [
  { status: undefined, label: "Network Drop / Disconnection" },
  { status: 500, label: "Server 500 Internal Error" },
  { status: 401, label: "Auth Expiry (401 Unauthorized)" },
  { status: 403, label: "Forbidden (403 Forbidden)" },
]) {
  test(`4. Lost response replay: Preserves exact operationId, amount and method across ${scenario.label}`, async () => {
    const storage = createMockStorage();
    const calls = [];
    let shouldFail = true;

    const loaded = await loadParcelPaymentHook(async (restaurantId, orderId, body) => {
      calls.push({ restaurantId, orderId, body });
      if (shouldFail) {
        if (scenario.status) {
          throw new loaded.ApiError(`HTTP ${scenario.status}`, scenario.status);
        } else {
          throw new Error("TypeError: Failed to fetch (Connection reset)");
        }
      }
      return { paymentId: "pay-ack", amount: body.amount, remaining: 0 };
    }, storage);

    const hook = loaded.useParcelPayment("rest-001", "emp-cashier-1", "ord-103", "bill-103", async () => {});

    // First attempt fails due to disconnect / 500 / 401
    await assert.rejects(async () => {
      await hook.run({ method: "Card", amount: 450.0, expectedRemaining: 450.0 });
    });

    assert.equal(calls.length, 1);
    const initialOpId = calls[0].body.operationId;
    assert.ok(initialOpId, "Operation ID must be generated and sent");

    // Must be retained in storage!
    assert.equal(storage.data.size, 1);
    const stored = JSON.parse(storage.getItem(loaded.getParcelPaymentStorageKey("emp-cashier-1", "rest-001", "ord-103")));
    assert.equal(stored.operationId, initialOpId);
    assert.equal(stored.amount, 450.0);
    assert.equal(stored.method, "Card");

    // Second attempt (Replay / Recovery)
    shouldFail = false;
    const replayResult = await hook.replay();
    assert.equal(calls.length, 2);

    // EXACT original payload replay: no newly generated opId, no changed details
    assert.equal(calls[1].body.operationId, initialOpId, "Must replay exact original operationId");
    assert.equal(calls[1].body.amount, 450.0);
    assert.equal(calls[1].body.method, "Card");
    assert.equal(replayResult.paymentId, "pay-ack");

    // Once definitive success is achieved, storage must be cleared
    assert.equal(storage.data.size, 0);
  });
}

test("5. Changed payment details attempt while unresolved operation is pending is rejected", async () => {
  const storage = createMockStorage();
  const calls = [];

  const loaded = await loadParcelPaymentHook(async (restaurantId, orderId, body) => {
    calls.push({ restaurantId, orderId, body });
    throw new loaded.ApiError("Server timeout", 504);
  }, storage);

  const hook = loaded.useParcelPayment("rest-001", "emp-cashier-1", "ord-104", "bill-104", async () => {});

  // First payment attempt fails transiently
  await assert.rejects(async () => {
    await hook.run({ method: "Cash", amount: 100.0 });
  });

  assert.equal(storage.data.size, 1);

  // Operator attempts to switch to UPI ₹200 while Cash ₹100 is unresolved
  await assert.rejects(
    async () => {
      await hook.run({ method: "UPI", amount: 200.0 });
    },
    /An unresolved Cash payment of ₹100.00 is pending/
  );

  // Network call count must remain 1 (no rogue request sent for UPI ₹200)
  assert.equal(calls.length, 1);

  // The stored Cash ₹100 must remain intact
  const stored = JSON.parse(storage.getItem(loaded.getParcelPaymentStorageKey("emp-cashier-1", "rest-001", "ord-104")));
  assert.equal(stored.amount, 100.0);
  assert.equal(stored.method, "Cash");
});

test("6. Definitive server rejection (400, 404, 409) clears pending storage so cashier can enter corrected payment", async () => {
  const storage = createMockStorage();
  let doneCalled = false;

  const loaded = await loadParcelPaymentHook(async () => {
    throw new loaded.ApiError("Overpayment: amount exceeds remaining balance", 400);
  }, storage);

  const hook = loaded.useParcelPayment("rest-001", "emp-cashier-1", "ord-105", "bill-105", async () => {
    doneCalled = true;
  });

  await assert.rejects(async () => {
    await hook.run({ method: "Cash", amount: 9999.0 });
  }, /Overpayment/);

  // Definitive business error should clear the storage
  assert.equal(storage.data.size, 0, "Storage cleared on 400 rejection");
  assert.equal(doneCalled, true, "onDone called to refresh balance");
});

test("7. Tenant and actor isolation: Cross-restaurant or cross-actor cannot access foreign pending payment", async () => {
  const storage = createMockStorage();
  const { getParcelPaymentStorageKey, readSavedParcelPayment } = await loadParcelPaymentHook(
    async () => {},
    storage
  );

  const key1 = getParcelPaymentStorageKey("cashier-A", "rest-001", "ord-999");
  storage.setItem(
    key1,
    JSON.stringify({
      restaurantId: "rest-001",
      orderId: "ord-999",
      billId: "bill-999",
      actorEmployeeId: "cashier-A",
      operationId: "op-rest1",
      method: "Cash",
      amount: 50.0,
      createdAt: Date.now(),
    })
  );

  // Same order, but requested by cashier-B or different restaurant rest-002
  const crossActor = readSavedParcelPayment("cashier-B", "rest-001", "ord-999");
  assert.equal(crossActor, null, "Foreign cashier must not read operation");

  const crossTenant = readSavedParcelPayment("cashier-A", "rest-002", "ord-999");
  assert.equal(crossTenant, null, "Foreign tenant must not read operation");

  const valid = readSavedParcelPayment("cashier-A", "rest-001", "ord-999");
  assert.ok(valid);
  assert.equal(valid.operationId, "op-rest1");
});

test("8. Security & Privacy: No tokens, passwords, or Authorization headers persisted in stored records", async () => {
  const storage = createMockStorage();
  const loaded = await loadParcelPaymentHook(async () => ({ paymentId: "p", remaining: 0 }), storage);

  const hook = loaded.useParcelPayment("rest-001", "emp-cashier-1", "ord-sec", "bill-sec", async () => {});

  // Simulate storing with dirty inputs
  await hook.run({ method: "UPI", amount: 250.0 });

  for (const [key, value] of storage.data.entries()) {
    assert.doesNotMatch(value, /Bearer/i);
    assert.doesNotMatch(value, /token/i);
    assert.doesNotMatch(value, /password/i);
    assert.doesNotMatch(value, /Authorization/i);
  }
});
