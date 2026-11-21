import { useRef, useState, useCallback, useEffect } from "react";
import { parcelApi, PaymentAcknowledgementDto } from "@/services/parcel";
import { ApiError } from "@/services/apiClient";

export interface SavedParcelPaymentOperation {
  restaurantId: string;
  orderId: string;
  billId: string;
  actorEmployeeId: string;
  operationId: string;
  method: "Cash" | "UPI" | "Card" | "MarketplaceCollected" | "Marketplace";
  amount: number;
  expectedRevision?: string;
  expectedRemaining?: number;
  createdAt: number;
}

export interface RecordPaymentParams {
  method: "Cash" | "UPI" | "Card" | "MarketplaceCollected" | "Marketplace";
  amount: number;
  expectedRevision?: string;
  expectedRemaining?: number;
  operationId?: string;
}

export function getParcelPaymentStorageKey(
  employeeId: string,
  restaurantId: string,
  orderId: string
): string {
  return `tablo_parcel_payment_v1_${employeeId}_${restaurantId}_${orderId}`;
}

export function readSavedParcelPayment(
  employeeId: string,
  restaurantId: string,
  orderId: string
): SavedParcelPaymentOperation | null {
  const key = getParcelPaymentStorageKey(employeeId, restaurantId, orderId);
  try {
    const raw = localStorage.getItem(key);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as SavedParcelPaymentOperation;
    // Validate required fields and tenant/actor match
    if (
      parsed &&
      parsed.operationId &&
      parsed.restaurantId === restaurantId &&
      parsed.orderId === orderId &&
      parsed.amount > 0 &&
      parsed.method
    ) {
      return parsed;
    }
    return null;
  } catch {
    return null;
  }
}

export function useParcelPayment(
  restaurantId: string,
  employeeId: string,
  orderId: string,
  billId: string,
  onDone: () => Promise<unknown>
) {
  const key = getParcelPaymentStorageKey(employeeId, restaurantId, orderId);

  const read = useCallback((): SavedParcelPaymentOperation | null => {
    return readSavedParcelPayment(employeeId, restaurantId, orderId);
  }, [employeeId, restaurantId, orderId]);

  const [pending, setPending] = useState<SavedParcelPaymentOperation | null>(read);
  const running = useRef(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lastAck, setLastAck] = useState<PaymentAcknowledgementDto | null>(null);

  // Sync state if order or restaurant changes
  useEffect(() => {
    setPending(read());
    setError(null);
    setLastAck(null);
  }, [read]);

  /**
   * Submit or replay a payment.
   * If an unresolved operation already exists in storage for this order,
   * caller cannot submit different payment details — they must replay the saved request.
   */
  async function run(params?: RecordPaymentParams): Promise<PaymentAcknowledgementDto | null> {
    if (running.current) return null;
    running.current = true;
    setBusy(true);
    setError(null);

    const saved = read();
    let toExecute: SavedParcelPaymentOperation;

    if (saved) {
      // If params are passed with changed details while an unresolved payment exists, block it!
      if (
        params &&
        ((params.method && params.method !== saved.method) ||
          (params.amount && Math.abs(params.amount - saved.amount) > 0.001))
      ) {
        running.current = false;
        setBusy(false);
        const conflictErr = `An unresolved ${saved.method} payment of ₹${saved.amount.toFixed(2)} is pending. You must replay or resolve it before initiating a different payment.`;
        setError(conflictErr);
        throw new Error(conflictErr);
      }
      toExecute = saved;
    } else {
      if (!params) {
        running.current = false;
        setBusy(false);
        return null;
      }

      toExecute = {
        restaurantId,
        orderId,
        billId,
        actorEmployeeId: employeeId,
        operationId: params.operationId || crypto.randomUUID(),
        method: params.method,
        amount: Math.round(params.amount * 100) / 100,
        expectedRevision: params.expectedRevision,
        expectedRemaining: params.expectedRemaining,
        createdAt: Date.now(),
      };

      // Persist before sending: fail-fast if storage fails
      try {
        localStorage.setItem(key, JSON.stringify(toExecute));
      } catch {
        running.current = false;
        setBusy(false);
        const storageErr = "Browser storage is unavailable. Enable storage before submitting this payment.";
        setError(storageErr);
        throw new Error(storageErr);
      }
      setPending(toExecute);
    }

    try {
      const ack = await parcelApi.recordPayment(restaurantId, orderId, {
        operationId: toExecute.operationId,
        method: toExecute.method,
        amount: toExecute.amount,
        expectedRevision: toExecute.expectedRevision,
        expectedRemaining: toExecute.expectedRemaining,
      });

      // Definitive success: clear storage and update
      localStorage.removeItem(key);
      setPending(null);
      setLastAck(ack);
      await onDone();
      return ack;
    } catch (e: any) {
      const status = e instanceof ApiError ? e.status : e?.status;

      // Definitive failure (400 Bad Request, 404 Not Found, 409 Conflict):
      // The server definitively rejected the payment; clear storage so user can correct input.
      if (status && [400, 404, 409].includes(status)) {
        localStorage.removeItem(key);
        setPending(null);
        await onDone();
      } else {
        // Transient failure (network drop, 500, 401, 403, undefined):
        // Retain saved operation in localStorage!
        setPending(toExecute);
      }

      const errMsg = e instanceof Error ? e.message : "Payment state is uncertain. Replay to verify.";
      setError(errMsg);
      throw e;
    } finally {
      running.current = false;
      setBusy(false);
    }
  }

  const replay = useCallback(async () => {
    return run();
  }, [run]);

  const clearPending = useCallback(() => {
    localStorage.removeItem(key);
    setPending(null);
    setError(null);
  }, [key]);

  return {
    pending,
    hasUnresolved: !!pending,
    busy,
    error,
    lastAck,
    run,
    replay,
    clearPending,
    refreshState: () => setPending(read()),
  };
}
