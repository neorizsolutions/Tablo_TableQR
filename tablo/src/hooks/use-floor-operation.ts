import { useRef, useState } from "react";
import { serviceDeskRequest, ServiceDeskError } from "@/services/service-desk";

export interface SavedFloorOperation {
  path: string;
  body: Record<string, unknown>;
}
export function useFloorOperation(
  restaurantId: string,
  employeeId: string,
  scope: string,
  onDone: () => Promise<unknown>,
) {
  const key = `tablo_floor_v1_${employeeId}_${restaurantId}_${scope}`;
  const read = () => {
    try {
      return JSON.parse(localStorage.getItem(key) || "null") as SavedFloorOperation | null;
    } catch {
      return null;
    }
  };
  const [pending, setPending] = useState<SavedFloorOperation | null>(read);
  const running = useRef(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  async function run(path: string, body: Record<string, unknown>) {
    if (running.current) return;
    running.current = true;
    const saved = read() || {
      path,
      body: { ...body, operationId: body.operationId || crypto.randomUUID() },
    };
    setBusy(true);
    setError("");
    try {
      // Persist before sending: an unavailable store must never create an
      // unrepeatable collection or split after a network failure.
      try {
        localStorage.setItem(key, JSON.stringify(saved));
      } catch {
        throw new Error(
          "Browser storage is unavailable. Enable storage before submitting this action.",
        );
      }
      setPending(saved);
      await serviceDeskRequest(restaurantId, saved.path, saved.body);
      localStorage.removeItem(key);
      setPending(null);
      await onDone();
      return true;
    } catch (e) {
      const status = e instanceof ServiceDeskError ? e.status : undefined;
      if (status && [400, 404, 409].includes(status)) {
        localStorage.removeItem(key);
        setPending(null);
        await onDone();
      }
      setError(
        e instanceof Error ? e.message : "Operation result is unknown. Retry the saved operation.",
      );
      return false;
    } finally {
      running.current = false;
      setBusy(false);
    }
  }
  return {
    pending,
    busy,
    error,
    run,
    retry: () => {
      const saved = read();
      if (saved) void run(saved.path, saved.body);
    },
  };
}
