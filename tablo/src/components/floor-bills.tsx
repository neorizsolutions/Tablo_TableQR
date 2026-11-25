import { useState } from "react";
import { QRCodeSVG } from "qrcode.react";
import { parcelApi, type UpiQrResponseDto } from "@/services/parcel";
import { useQueryClient } from "@tanstack/react-query";
import { useFloorOperation } from "@/hooks/use-floor-operation";
import {
  serviceDeskRequest,
  type BillReview,
  type TableBill,
  type ServiceTable,
} from "@/services/service-desk";
import { PaymentReceipt } from "./payment-receipt";
import { UnpaidBillPreview } from "./unpaid-bill-preview";
import { formatINR } from "@/lib/mock-data";
const input = "rounded-xl border bg-background px-3 py-2 text-sm";
const button = "rounded-full border px-4 py-2 text-sm font-semibold disabled:opacity-40";

export function FloorBills({
  restaurantId,
  employeeId,
  tableId,
  groupId,
  review,
  tables,
  onTransferred,
  billingOnly = false,
}: {
  restaurantId: string;
  employeeId: string;
  tableId: string;
  groupId: string;
  review?: BillReview;
  tables: ServiceTable[];
  onTransferred: (id: string) => void;
  billingOnly?: boolean;
}) {
  const cache = useQueryClient();
  const [label, setLabel] = useState("");
  const [target, setTarget] = useState("");
  const [confirmTransfer, setConfirmTransfer] = useState(false);
  const refresh = async () => {
    await Promise.all([
      cache.invalidateQueries({ queryKey: ["tableBill", restaurantId] }),
      cache.invalidateQueries({ queryKey: ["diningGroups", restaurantId] }),
      cache.invalidateQueries({ queryKey: ["orders"] }),
      cache.invalidateQueries({ queryKey: ["paymentReports", restaurantId] }),
    ]);
  };
  const op = useFloorOperation(restaurantId, employeeId, `${tableId}_${groupId}_visit`, refresh);
  const base = `tables/${tableId}`;
  return (
    <div className="space-y-5">
      {!billingOnly && <section className="rounded-2xl border p-4">
        <h3 className="font-semibold">Dining groups and transfers</h3>
        <p className="my-2 text-xs text-muted-foreground">
          Groups A/B have independent orders and bills. Existing whole-table bills stay under Whole
          table.
        </p>
        <div className="flex flex-wrap gap-2">
          <input
            aria-label="New group label"
            placeholder="A, B…"
            className={input}
            value={label}
            maxLength={8}
            onChange={(e) => setLabel(e.target.value.toUpperCase())}
          />
          <button
            className={button}
            disabled={!label || op.busy || !!op.pending}
            onClick={() => void op.run(`${base}/groups`, { label })}
          >
            Create group
          </button>
          {groupId && (
            <button
              className={button}
              disabled={op.busy || !!op.pending}
              onClick={() => void op.run(`${base}/groups/${groupId}/close`, {})}
            >
              Close settled group
            </button>
          )}
        </div>
        <div className="mt-3 flex flex-wrap gap-2">
          <label className="grid gap-1 text-sm">
            Transfer destination
            <select
              className={input}
              value={target}
              onChange={(e) => {
                setTarget(e.target.value);
                setConfirmTransfer(false);
              }}
            >
              <option value="">Choose empty table</option>
              {tables
                .filter((t) => t.isActive && t.id !== tableId)
                .map((t) => (
                  <option key={t.id} value={t.id}>
                    Table {t.tableNumber}
                  </option>
                ))}
            </select>
          </label>
          <button
            className={button}
            disabled={!target || op.busy || !!op.pending}
            onClick={() => setConfirmTransfer(true)}
          >
            Review transfer
          </button>
        </div>
        {confirmTransfer && (
          <div className="mt-3 rounded-xl bg-secondary p-3">
            <p>
              Move this {groupId ? "group" : "whole-table visit"}, its open bills, food and service
              requests to Table {tables.find((t) => t.id === target)?.tableNumber}? An occupied
              destination will be rejected.
            </p>
            <button
              className={`${button} mt-2`}
              disabled={op.busy || !!op.pending}
              onClick={async () => {
                const completed = await op.run(`${base}/transfer`, {
                  destinationTableId: target,
                  diningGroupId: groupId || null,
                });
                if (completed) onTransferred(target);
                setConfirmTransfer(false);
              }}
            >
              Confirm transfer
            </button>
            <button className={button} onClick={() => setConfirmTransfer(false)}>
              Cancel
            </button>
          </div>
        )}
        {op.error && (
          <p role="alert" className="mt-3 text-destructive">
            {op.error}
          </p>
        )}
        {op.pending && (
          <p className="mt-3">
            This operation may already have completed.{" "}
            <button className={button} disabled={op.busy} onClick={op.retry}>
              Retry saved action
            </button>
          </p>
        )}
      </section>}
      <h3 className="font-semibold">Bill review</h3>
      {!review?.openBills.length && <p>No open bill for this selection.</p>}
      {review?.openBills.map((bill) => (
        <BillCard
          key={bill.id}
          bill={bill}
          restaurantId={restaurantId}
          employeeId={employeeId}
          refresh={refresh}
        />
      ))}
      {!!review?.unbilledActiveOrderCount && (
        <button
          className={button}
          onClick={() =>
            void op.run(`${base}/bill/open${groupId ? `?diningGroupId=${groupId}` : ""}`, {})
          }
        >
          Include current active orders in bill
        </button>
      )}
      <details>
        <summary className="cursor-pointer font-semibold">Recent settled bills</summary>
        {review?.recentSettlements.map((bill) => (
          <div key={bill.id} className="mt-3 rounded-xl border p-3">
            <p>
              {formatINR(bill.totalAmount)} · {bill.paymentMethod} ·{" "}
              {bill.settledAtUtc && new Date(bill.settledAtUtc).toLocaleString()}
            </p>
            <PaymentReceipt bill={bill} />
          </div>
        ))}
      </details>
      <p className="text-xs text-muted-foreground">
        After a transfer, select the destination table to continue service. Partial payments are
        externally collected funds, not payment-provider transactions.
      </p>
    </div>
  );
}

function BillCard({
  bill,
  restaurantId,
  employeeId,
  refresh,
}: {
  bill: TableBill;
  restaurantId: string;
  employeeId: string;
  refresh: () => Promise<unknown>;
}) {
  const [method, setMethod] = useState("Cash");
  const [amount, setAmount] = useState("");
  const [confirm, setConfirm] = useState(false);
  const [mode, setMode] = useState("amount");
  const [shares, setShares] = useState("");
  const [quantities, setQuantities] = useState<Record<string, number>>({});
  const [preview, setPreview] = useState<TableBill[] | null>(null);
  const [splitRequest, setSplitRequest] = useState<Record<string, unknown> | null>(null);
  const [previewError, setPreviewError] = useState("");
  const [previewBusy, setPreviewBusy] = useState(false);
  const [upiRequest, setUpiQr] = useState<UpiQrResponseDto | null>(null);
  const [upiBusy, setUpiBusy] = useState(false);
  const [upiError, setUpiError] = useState("");
  const [leftoverBusy, setLeftoverBusy] = useState(false);
  const op = useFloorOperation(restaurantId, employeeId, `bill_${bill.id}`, refresh);
  const remaining = Math.max(0, bill.remainingAmount);
  const paying = Number(amount || remaining);
  const valid =
    Number.isFinite(paying) &&
    paying > 0 &&
    paying <= remaining &&
    (!amount || /^\d+(\.\d{1,2})?$/.test(amount));
  const upiQr = method === "UPI" && valid && !op.pending && !bill.settledAtUtc &&
    upiRequest?.billId === bill.id && upiRequest.amount === paying &&
    upiRequest.remainingBalance === remaining ? upiRequest : null;
  const items = bill.orders
    .filter((o) => !["Cancelled", "Rejected"].includes(o.status))
    .flatMap((o) => o.items);
  async function showPreview() {
    setPreviewError("");
    setPreviewBusy(true);
    try {
      const allocated = items
        .filter((i) => (quantities[i.id] || 0) > 0)
        .map((i) => [i.id, quantities[i.id]]);
      const rest = items
        .filter((i) => i.quantity - (quantities[i.id] || 0) > 0)
        .map((i) => [i.id, i.quantity - (quantities[i.id] || 0)]);
      const request = {
        operationId: crypto.randomUUID(),
        expectedRevision: bill.revision,
        amounts: mode === "amount" ? shares.split(",").map((s) => Number(s.trim())) : null,
        items: mode === "items" ? [Object.fromEntries(allocated), Object.fromEntries(rest)] : null,
      };
      setPreview(
        await serviceDeskRequest<TableBill[]>(
          restaurantId,
          `bills/${bill.id}/split-preview`,
          request,
        ),
      );
      setSplitRequest(request);
    } catch (e) {
      setPreviewError(e instanceof Error ? e.message : "Preview failed");
    } finally {
      setPreviewBusy(false);
    }
  }

  async function handleAddLeftoverContainer() {
    if (leftoverBusy || bill.settledAtUtc) return;
    setLeftoverBusy(true);
    try {
      await serviceDeskRequest(restaurantId, `parcel/bills/${bill.id}/leftover-packing`, {
        operationId: crypto.randomUUID(),
        containerCount: 1,
        containerCharge: 10
      });
      await refresh();
    } catch (e) {
      console.error("Failed to add leftover container charge:", e);
    } finally {
      setLeftoverBusy(false);
    }
  }

  async function handleFetchUpiQr() {
    setUpiQr(null);
    setUpiError("");
    setUpiBusy(true);
    try {
      const qr = await parcelApi.getBillUpiQr(restaurantId, bill.id, paying);
      if (qr.remainingBalance !== remaining || qr.amount !== paying) {
        await refresh();
        setUpiError("The bill balance changed. Review the amount and generate a new QR.");
        return;
      }
      setUpiQr(qr);
    } catch (e) {
      setUpiError(e instanceof Error ? e.message : "Could not generate the UPI QR.");
    } finally {
      setUpiBusy(false);
    }
  }

  return (
    <article className="rounded-2xl border p-4">
      {remaining > 0 && !bill.settledAtUtc && (
        <UnpaidBillPreview restaurantId={restaurantId} bill={bill} />
      )}
      <h4 className="font-semibold">
        Table {bill.tableNumber}
        {bill.groupLabel} · {bill.shareDescription || "Open bill"}
      </h4>
      <p className="break-all text-xs text-muted-foreground">{bill.id}</p>
      <ul className="my-3 space-y-1 text-sm">
        {bill.orders.map((o) => (
          <li key={o.id}>
            <strong>
              ORD-{o.orderNumber} · {o.status}
            </strong>
            {o.items.map((i) => (
              <p key={i.id}>
                {i.quantity} × {i.name} · {formatINR(i.unitPrice * i.quantity)}
              </p>
            ))}
          </li>
        ))}
      </ul>
      <div className="mt-4 grid grid-cols-3 gap-2 rounded-2xl bg-secondary/40 p-3 text-center border border-border/60">
        <div className="p-2">
          <p className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">Total Bill</p>
          <p className="mt-1 font-display text-lg font-bold">{formatINR(bill.totalAmount)}</p>
          <p className="text-[10px] text-muted-foreground">Sub {formatINR(bill.subtotal)} + Tax {formatINR(bill.tax)}</p>
        </div>
        <div className="p-2 border-x border-border/60">
          <p className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">Paid Received</p>
          <p className="mt-1 font-display text-lg font-bold text-emerald-700 dark:text-emerald-400">{formatINR(bill.paidAmount)}</p>
          <p className="text-[10px] text-muted-foreground">{bill.payments?.length || 0} payment(s)</p>
        </div>
        <div className="p-2">
          <p className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">Balance Due</p>
          <p className={`mt-1 font-display text-lg font-black ${remaining > 0 ? "text-amber-600 dark:text-amber-400" : "text-emerald-600"}`}>
            {formatINR(remaining)}
          </p>
          <p className="text-[10px] text-muted-foreground">{remaining > 0 ? "Pending collection" : "Fully Settled ✓"}</p>
        </div>
      </div>
      {!!bill.packagingCharge && bill.packagingCharge > 0 && (
        <p className="text-xs text-muted-foreground mt-2">Packaging charge: {formatINR(bill.packagingCharge)}</p>
      )}
      {!!bill.leftoverPackingCharge && bill.leftoverPackingCharge > 0 && (
        <p className="text-xs text-muted-foreground mt-1">Leftover containers: {formatINR(bill.leftoverPackingCharge)}</p>
      )}
      {!bill.settledAtUtc && (
        <div className="mt-2">
          <button
            type="button"
            className="rounded-full border border-primary/40 bg-primary/10 px-3 py-1 text-xs font-semibold text-primary hover:bg-primary/20"
            disabled={leftoverBusy || op.busy || !!op.pending}
            onClick={handleAddLeftoverContainer}
          >
            {leftoverBusy ? "Adding..." : "+ Leftover Container (₹10)"}
          </button>
        </div>
      )}
      {bill.payments?.map((p) => (
        <p key={p.id} className="text-sm mt-1">
          {p.method} {formatINR(p.amount)} · {new Date(p.collectedAtUtc).toLocaleString()}
        </p>
      ))}
      {!bill.canSettle && (
        <p className="mt-2 text-sm text-amber-700 dark:text-amber-400 font-medium">Serve all chargeable food before payment or splitting.</p>
      )}
      <div className="mt-3 flex flex-wrap gap-2 items-end">
        <label className="grid text-sm">
          Collected by
          <select
            className={input}
            value={method}
            onChange={(e) => {
              setMethod(e.target.value);
              setConfirm(false);
            }}
          >
            {["Cash", "UPI", "Card"].map((m) => (
              <option key={m}>{m}</option>
            ))}
          </select>
        </label>
        <label className="grid text-sm">
          Amount collected
          <input
            aria-label={`Payment amount ${bill.id}`}
            className={input}
            type="number"
            min="0.01"
            step="0.01"
            value={amount}
            placeholder={remaining.toFixed(2)}
            onChange={(e) => {
              setAmount(e.target.value);
              setConfirm(false);
            }}
          />
        </label>
        {remaining > 0 && !amount && (
          <button
            type="button"
            className="rounded-full border border-border bg-card px-3 py-2 text-xs font-semibold hover:bg-secondary"
            onClick={() => {
              setAmount(remaining.toFixed(2));
              setConfirm(false);
            }}
          >
            Fill Full ({formatINR(remaining)})
          </button>
        )}
        <button
          className={button}
          disabled={
            !bill.canSettle || op.busy || !!op.pending || (!valid && bill.totalAmount !== 0)
          }
          onClick={() => setConfirm(true)}
        >
          Review payment
        </button>
        {method === "UPI" && remaining > 0 && (
          <button
            type="button"
            className="rounded-full border border-purple-400 bg-purple-50 text-purple-700 px-3 py-2 text-sm font-semibold hover:bg-purple-100"
            disabled={upiBusy || !valid || !!op.pending}
            onClick={handleFetchUpiQr}
          >
            {upiBusy ? "Loading QR..." : "Dynamic UPI QR"}
          </button>
        )}
      </div>
      {method === "UPI" && upiError && <p role="alert" className="mt-2 text-sm text-destructive">{upiError}</p>}
      {upiQr && (
        <div className="mt-4 rounded-xl border border-purple-200 bg-purple-50/50 p-4 space-y-2">
          <div className="flex justify-between items-center">
            <h5 className="font-bold text-sm text-purple-900">Dynamic UPI QR Payment</h5>
            <button
              type="button"
              className="text-xs text-muted-foreground hover:text-foreground font-bold"
              onClick={() => setUpiQr(null)}
            >
              ✕ Close
            </button>
          </div>
          <div className="text-xs space-y-1">
            <p><strong>Payee:</strong> {upiQr.payeeName} ({upiQr.payeeVpa})</p>
            <p><strong>Amount:</strong> {formatINR(upiQr.amount)}</p>
            <p><strong>Reference:</strong> {upiQr.reference}</p>
          </div>
          <div className="w-fit rounded-lg border bg-white">
            <QRCodeSVG value={upiQr.upiUri} size={160} marginSize={4} title="Scan to pay with UPI" />
          </div>
          <div className="flex gap-2 pt-1">
            <a
              href={upiQr.upiUri}
              target="_blank"
              rel="noreferrer"
              className="rounded-full bg-purple-600 text-white px-4 py-1.5 text-xs font-bold hover:bg-purple-700"
            >
              Open UPI App
            </a>
            <button
              type="button"
              className="rounded-full border px-4 py-1.5 text-xs font-semibold"
              onClick={() => {
                setUpiQr(null);
                setConfirm(true);
              }}
            >
              Confirm Received
            </button>
          </div>
        </div>
      )}
      {confirm && (
        <div className="mt-3 rounded-xl bg-secondary p-3">
          <p>
            Record {formatINR(bill.totalAmount === 0 ? 0 : paying)} already collected by {method}?
            Remaining afterwards: {formatINR(Math.max(0, remaining - paying))}.
          </p>
          <button
            className={button}
            disabled={op.busy || !!op.pending}
            onClick={async () => {
              await op.run(
                `bills/${bill.id}/${bill.totalAmount === 0 ? "settle" : "payments"}`,
                bill.totalAmount === 0
                  ? { expectedRevision: bill.revision, expectedTotal: 0, paymentMethod: method }
                  : {
                      amount: paying,
                      method,
                      expectedRevision: bill.revision,
                      expectedRemaining: remaining,
                    },
              );
              setConfirm(false);
              setAmount("");
            }}
          >
            Confirm collection
          </button>
          <button className={button} onClick={() => setConfirm(false)}>
            Cancel
          </button>
        </div>
      )}
      {!bill.parentBillId && bill.paidAmount === 0 && (
        <details className="mt-4">
          <summary className="cursor-pointer font-semibold">Split this bill</summary>
          <label className="mt-2 grid text-sm">
            Split type
            <select
              className={input}
              value={mode}
              onChange={(e) => {
                setMode(e.target.value);
                setPreview(null);
              }}
            >
              <option value="amount">Guest amounts</option>
              <option value="items">Items / quantities (two bills)</option>
            </select>
          </label>
          {mode === "amount" ? (
            <label className="mt-2 grid text-sm">
              Guest amounts, separated by commas
              <input
                className={input}
                value={shares}
                placeholder="50.00, 55.00"
                onChange={(e) => {
                  setShares(e.target.value);
                  setPreview(null);
                }}
              />
            </label>
          ) : (
            <div className="mt-2">
              {items.map((i) => (
                <label key={i.id} className="mb-2 flex items-center justify-between gap-2 text-sm">
                  {i.name} · {i.quantity} ordered · quantity for first bill
                  <input
                    className={`${input} w-20`}
                    type="number"
                    min="0"
                    max={i.quantity}
                    step="1"
                    value={quantities[i.id] || 0}
                    onChange={(e) => {
                      setQuantities((v) => ({ ...v, [i.id]: Number(e.target.value) }));
                      setPreview(null);
                    }}
                  />
                </label>
              ))}
              <p className="text-xs">
                Unselected quantities go to the second bill. Original kitchen orders stay intact.
              </p>
            </div>
          )}
          <button
            className={`${button} mt-2`}
            disabled={!bill.canSettle || op.busy || !!op.pending || previewBusy}
            onClick={() => void showPreview()}
          >
            Preview split
          </button>
          {previewError && <p role="alert">{previewError}</p>}
          {preview && (
            <div className="mt-2 rounded-xl bg-secondary p-3">
              {preview.map((b, n) => (
                <p key={b.id}>
                  Share {n + 1}: {formatINR(b.totalAmount)} (tax {formatINR(b.tax)})
                </p>
              ))}
              <button
                className={button}
                disabled={op.busy || !!op.pending}
                onClick={async () => {
                  if (splitRequest) await op.run(`bills/${bill.id}/split`, splitRequest);
                  setPreview(null);
                }}
              >
                Confirm split
              </button>
            </div>
          )}
        </details>
      )}
      {op.error && (
        <p role="alert" className="mt-2 text-destructive">
          {op.error}
        </p>
      )}
      {op.pending && (
        <div className="mt-2">
          <p>Result may already be recorded. Retry the original saved action.</p>
          <button className={button} disabled={op.busy} onClick={op.retry}>
            Retry saved bill action
          </button>
          <a
            href="/auth"
            className="ml-3 underline"
            onClick={() => localStorage.removeItem("tablo_token")}
          >
            Sign in again
          </a>
        </div>
      )}
    </article>
  );
}
