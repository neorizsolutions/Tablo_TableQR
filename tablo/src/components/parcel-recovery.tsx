import { useEffect, useRef, useState } from "react";
import { parcelApi } from "@/services/parcel";
import { ApiError } from "@/services/apiClient";
import type { SavedParcelPaymentOperation } from "@/hooks/use-parcel-payment";
import { AlertCircle, RefreshCw } from "lucide-react";
import { Button } from "@/components/ui/button";

export function ParcelRecovery({
  restaurantId,
  employeeId,
  onResolved,
}: {
  restaurantId: string;
  employeeId: string;
  onResolved?: () => void;
}) {
  const [saved, setSaved] = useState<{ key: string; operation: SavedParcelPaymentOperation }[]>([]);
  const [error, setError] = useState("");
  const running = useRef(false);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    const scan = () => {
      const result: { key: string; operation: SavedParcelPaymentOperation }[] = [];
      try {
        const prefix = `tablo_parcel_payment_v1_${employeeId}_${restaurantId}_`;
        for (let i = 0; i < localStorage.length; i++) {
          const key = localStorage.key(i);
          if (!key?.startsWith(prefix)) continue;
          try {
            const raw = localStorage.getItem(key);
            if (!raw) continue;
            const operation = JSON.parse(raw);
            if (operation?.orderId && operation?.operationId && operation?.amount > 0) {
              result.push({ key, operation });
            }
          } catch {
            // Ignore corrupted drafts
          }
        }
      } catch {
        return;
      }
      setSaved(result);
    };

    scan();
    const interval = setInterval(scan, 2000);
    return () => clearInterval(interval);
  }, [restaurantId, employeeId]);

  async function retry(entry: { key: string; operation: SavedParcelPaymentOperation }) {
    if (running.current) return;
    running.current = true;
    setBusy(true);
    setError("");
    try {
      await parcelApi.recordPayment(restaurantId, entry.operation.orderId, {
        operationId: entry.operation.operationId,
        method: entry.operation.method,
        amount: entry.operation.amount,
        expectedRevision: entry.operation.expectedRevision,
        expectedRemaining: entry.operation.expectedRemaining,
      });
      localStorage.removeItem(entry.key);
      setSaved((old) => old.filter((s) => s.key !== entry.key));
      onResolved?.();
    } catch (e: any) {
      const status = e instanceof ApiError ? e.status : e?.status;
      if (status && [400, 404, 409].includes(status)) {
        localStorage.removeItem(entry.key);
        setSaved((old) => old.filter((s) => s.key !== entry.key));
        onResolved?.();
      }
      setError(e instanceof Error ? e.message : "Payment confirmation uncertain. Retry the same payment.");
    } finally {
      running.current = false;
      setBusy(false);
    }
  }

  if (!saved.length) return null;

  return (
    <aside
      aria-label="Unresolved parcel payment recovery"
      className="my-3 rounded-lg border border-amber-500/40 bg-amber-500/10 p-4 text-amber-900 dark:text-amber-200"
    >
      <div className="flex items-center gap-2">
        <AlertCircle className="h-5 w-5 text-amber-600 dark:text-amber-400 shrink-0" />
        <h3 className="font-semibold text-sm">Unresolved Payment Actions Awaiting Server Confirmation</h3>
      </div>
      <p className="text-xs text-muted-foreground mt-1">
        A payment request was in flight when the network or browser interrupted. Replaying will safely retrieve the authoritative result without collecting twice.
      </p>
      <div className="mt-3 space-y-2">
        {saved.map((entry) => (
          <div
            key={entry.key}
            className="flex flex-wrap items-center justify-between gap-3 bg-background/80 p-2.5 rounded-md border text-foreground"
          >
            <div className="text-xs space-y-0.5">
              <span className="font-semibold">
                Order #{entry.operation.orderId.slice(0, 8)}: {entry.operation.method} ₹{entry.operation.amount.toFixed(2)}
              </span>
              <p className="text-muted-foreground font-mono text-[10px]">OpID: {entry.operation.operationId}</p>
            </div>
            <Button
              size="sm"
              variant="outline"
              disabled={busy}
              onClick={() => void retry(entry)}
              className="h-8 gap-1.5 text-xs"
            >
              <RefreshCw className={`h-3.5 w-3.5 ${busy ? "animate-spin" : ""}`} />
              Replay Payment
            </Button>
          </div>
        ))}
      </div>
      {error && <p className="text-xs text-destructive mt-2" role="alert">{error}</p>}
    </aside>
  );
}
