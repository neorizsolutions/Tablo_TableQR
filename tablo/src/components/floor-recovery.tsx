import { useEffect, useRef, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { serviceDeskRequest, ServiceDeskError } from "@/services/service-desk";
import type { SavedFloorOperation } from "@/hooks/use-floor-operation";

export function FloorRecovery({
  restaurantId,
  employeeId,
}: {
  restaurantId: string;
  employeeId: string;
}) {
  const [saved, setSaved] = useState<{ key: string; operation: SavedFloorOperation }[]>([]);
  const [error, setError] = useState("");
  const running = useRef(false);
  const [busy, setBusy] = useState(false);
  const cache = useQueryClient();
  useEffect(() => {
    const scan = () => {
      const result: { key: string; operation: SavedFloorOperation }[] = [];
      try {
        for (let i = 0; i < localStorage.length; i++) {
          const key = localStorage.key(i);
          if (!key?.startsWith(`tablo_floor_v1_${employeeId}_${restaurantId}_`)) continue;
          try {
            const operation = JSON.parse(localStorage.getItem(key) || "null");
            if (operation?.path && operation?.body?.operationId) result.push({ key, operation });
          } catch {
            /* Ignore unrelated/corrupt drafts. */
          }
        }
      } catch {
        // Storage may be disabled by browser policy. Keep the service desk usable.
        return;
      }
      setSaved(result);
    };
    scan();
    const interval = setInterval(scan, 2000);
    return () => clearInterval(interval);
  }, [restaurantId, employeeId]);
  async function retry(entry: { key: string; operation: SavedFloorOperation }) {
    if (running.current) return;
    running.current = true;
    setBusy(true);
    setError("");
    try {
      await serviceDeskRequest(restaurantId, entry.operation.path, entry.operation.body);
      localStorage.removeItem(entry.key);
      setSaved((old) => old.filter((s) => s.key !== entry.key));
      await cache.invalidateQueries();
    } catch (e) {
      if (e instanceof ServiceDeskError && [400, 404, 409].includes(e.status)) {
        localStorage.removeItem(entry.key);
        setSaved((old) => old.filter((s) => s.key !== entry.key));
        await cache.invalidateQueries();
      }
      setError(e instanceof Error ? e.message : "Result unknown. Retry the same operation.");
    } finally {
      running.current = false;
      setBusy(false);
    }
  }
  if (!saved.length) return null;
  return (
    <aside
      aria-label="Saved operation recovery"
      className="my-4 rounded-xl border border-warning p-3"
    >
      <h3 className="font-semibold">Saved actions awaiting confirmation</h3>
      <p className="text-sm">
        An action may already have completed. Retry its original ID to retrieve the result safely.
      </p>
      {saved.map((entry) => (
        <div key={entry.key} className="mt-2 flex flex-wrap items-center gap-2">
          <span className="break-all text-xs">{entry.operation.path}</span>
          <button
            disabled={busy}
            onClick={() => void retry(entry)}
            className="rounded-full border px-3 py-1 text-sm"
          >
            Retry original action
          </button>
        </div>
      ))}
      {error && <p role="alert">{error}</p>}
      <a
        className="mt-2 block text-sm underline"
        href="/auth"
        onClick={() => localStorage.removeItem("tablo_token")}
      >
        Sign in again with the same account
      </a>
    </aside>
  );
}
