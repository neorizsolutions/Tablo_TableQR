import { useEffect, useRef, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { HubConnectionBuilder } from "@microsoft/signalr";
import { Loader2, Minus, Plus, Receipt, RefreshCw, Search, ShoppingBag } from "lucide-react";
import { toast } from "sonner";
import { useMenu } from "@/hooks/use-menu";
import { Dialog, DialogContent, DialogDescription, DialogTitle } from "@/components/ui/dialog";
import { formatINR } from "@/lib/mock-data";
import { API_BASE_URL } from "@/services/apiClient";
import { clearDefinitivelyFailedOperation } from "@/services/pending-operation";
import { PaymentReceipt } from "@/components/payment-receipt";
import {
  getBillReview,
  getServiceTables,
  openTableBill,
  ServiceDeskError,
  settleTableBill,
  submitStaffOrder,
  type SettlementRequest,
  type StaffOrderRequest,
  type TableBill,
} from "@/services/service-desk";

interface CartLine {
  menuItemId: string;
  name: string;
  quantity: number;
  price: number;
}
interface PendingOrder {
  request: StaffOrderRequest;
  lines: CartLine[];
}
interface PendingSettlement {
  bill: TableBill;
  request: SettlementRequest;
}

function readSaved<T>(key: string): T | null {
  try {
    return JSON.parse(localStorage.getItem(key) || "null") as T | null;
  } catch {
    return null;
  }
}

export function ServiceDesk({
  restaurantId,
  employeeId,
}: {
  restaurantId: string;
  employeeId: string;
}) {
  const cache = useQueryClient();
  const [tableId, setTableId] = useState("");
  const tables = useQuery({
    queryKey: ["serviceTables", restaurantId],
    queryFn: () => getServiceTables(restaurantId),
    refetchInterval: 15000,
  });
  const { data: menu = [], isLoading: menuLoading, error: menuError } = useMenu(restaurantId);
  const billQuery = useQuery({
    queryKey: ["tableBill", restaurantId, tableId],
    queryFn: () => getBillReview(restaurantId, tableId),
    enabled: !!tableId,
    refetchInterval: 15000,
  });
  const [search, setSearch] = useState("");
  const [cart, setCart] = useState<CartLine[]>([]);
  const [pendingOrder, setPendingOrder] = useState<PendingOrder | null>(null);
  const [settlement, setSettlement] = useState<PendingSettlement | null>(null);
  const [receipt, setReceipt] = useState<TableBill | null>(null);
  const [method, setMethod] = useState("Cash");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [needsSignIn, setNeedsSignIn] = useState(false);
  const [uncertainSettlement, setUncertainSettlement] = useState(false);
  const running = useRef(false);
  const orderKey = `tablo_staff_order_${employeeId}_${restaurantId}_${tableId}`;
  const settlementKey = `tablo_settlement_${employeeId}_${restaurantId}_${tableId}`;
  const table = tables.data?.find((t) => t.id === tableId);
  const bill = billQuery.data?.openBill;

  useEffect(() => {
    const saved = readSaved<PendingOrder>(orderKey);
    const payment = readSaved<PendingSettlement>(settlementKey);
    setPendingOrder(saved);
    setCart(saved?.lines || []);
    setSettlement(payment);
    setUncertainSettlement(!!payment);
    setReceipt(null);
    setError("");
    setNeedsSignIn(false);
  }, [orderKey, settlementKey]);

  useEffect(() => {
    let disposed = false;
    const connection = new HubConnectionBuilder()
      .withUrl(API_BASE_URL.replace(/\/api\/?$/, "/hubs/order"), {
        accessTokenFactory: () => localStorage.getItem("tablo_token") || "",
      })
      .withAutomaticReconnect()
      .build();
    const refresh = () => {
      void cache.invalidateQueries({ queryKey: ["tableBill", restaurantId] });
      void cache.invalidateQueries({ queryKey: ["orders", restaurantId] });
      void cache.invalidateQueries({ queryKey: ["serviceRequests", restaurantId] });
      void cache.invalidateQueries({ queryKey: ["paymentReports", restaurantId] });
    };
    const join = async () => {
      await connection.invoke("JoinRestaurantGroup");
      refresh();
    };
    connection.on("BillUpdated", refresh);
    connection.on("NewOrder", refresh);
    connection.on("OrderStatusUpdated", refresh);
    connection.onreconnected(() => {
      void join().catch(() => undefined);
    });
    void connection
      .start()
      .then(() => {
        if (!disposed) return join();
      })
      .catch(() => undefined);
    return () => {
      disposed = true;
      void connection.stop();
    };
  }, [restaurantId, cache]);

  const refresh = async () => {
    await Promise.all([
      cache.invalidateQueries({ queryKey: ["tableBill", restaurantId] }),
      cache.invalidateQueries({ queryKey: ["orders"] }),
      cache.invalidateQueries({ queryKey: ["serviceRequests"] }),
      cache.invalidateQueries({ queryKey: ["paymentReports", restaurantId] }),
    ]);
  };

  const changeQuantity = (id: string, delta: number) => {
    if (pendingOrder || busy) return;
    const item = menu.find((m) => m.id === id);
    if (!item) return;
    setCart((lines) => {
      const existing = lines.find((l) => l.menuItemId === id);
      return existing
        ? lines
            .map((l) => (l.menuItemId === id ? { ...l, quantity: l.quantity + delta } : l))
            .filter((l) => l.quantity > 0)
        : delta > 0
          ? [...lines, { menuItemId: id, name: item.name, price: item.price, quantity: 1 }]
          : lines;
    });
  };

  const place = async () => {
    if (running.current || !tableId || !cart.length) return;
    running.current = true;
    setBusy(true);
    setError("");
    try {
      const pending = pendingOrder || {
        lines: cart,
        request: {
          operationId: crypto.randomUUID(),
          tableId,
          items: cart.map((l) => ({ menuItemId: l.menuItemId, quantity: l.quantity })),
        },
      };
      // Persist before sending, so timeout/refresh retries reuse the exact submission.
      localStorage.setItem(orderKey, JSON.stringify(pending));
      setPendingOrder(pending);
      const result = await submitStaffOrder(restaurantId, pending.request);
      localStorage.removeItem(orderKey);
      setPendingOrder(null);
      setCart([]);
      toast.success(`Order ORD-${String(result.orderNumber).padStart(4, "0")} sent to the kitchen`);
      await refresh();
    } catch (e) {
      setNeedsSignIn(e instanceof ServiceDeskError && [401, 403].includes(e.status));
      const definitive = clearDefinitivelyFailedOperation(
        localStorage,
        orderKey,
        e instanceof ServiceDeskError ? e.status : undefined,
      );
      if (definitive) {
        setPendingOrder(null);
      }
      setError(
        e instanceof ServiceDeskError && [401, 403].includes(e.status)
          ? "Sign in again with the same staff account. Your original submission is saved; select this table and retry it."
          : e instanceof Error
            ? e.message
            : "Could not send order. Retry the saved submission.",
      );
    } finally {
      running.current = false;
      setBusy(false);
    }
  };

  const settle = async () => {
    if (running.current || !settlement) return;
    running.current = true;
    setBusy(true);
    setError("");
    try {
      localStorage.setItem(settlementKey, JSON.stringify(settlement));
      setUncertainSettlement(true);
      const result = await settleTableBill(restaurantId, settlement.bill.id, settlement.request);
      localStorage.removeItem(settlementKey);
      setUncertainSettlement(false);
      setSettlement(null);
      setReceipt(result);
      toast.success("Payment recorded and bill settled");
      await refresh();
    } catch (e) {
      setNeedsSignIn(e instanceof ServiceDeskError && [401, 403].includes(e.status));
      if (
        clearDefinitivelyFailedOperation(
          localStorage,
          settlementKey,
          e instanceof ServiceDeskError ? e.status : undefined,
        )
      ) {
        setUncertainSettlement(false);
        setSettlement(null);
        await refresh();
      }
      setError(
        e instanceof ServiceDeskError && [401, 403].includes(e.status)
          ? "Sign in again with the same staff account. The payment confirmation is saved; retry it rather than recording a new payment."
          : e instanceof Error
            ? e.message
            : "Payment result is unknown. Retry to confirm the saved operation.",
      );
    } finally {
      running.current = false;
      setBusy(false);
    }
  };

  const subtotal = cart.reduce((sum, l) => sum + l.price * l.quantity, 0);
  const shown = menu.filter((m) =>
    `${m.name} ${m.category}`.toLowerCase().includes(search.toLowerCase()),
  );
  const inputClass =
    "rounded-xl border border-border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring";
  const actionClass =
    "inline-flex items-center justify-center gap-2 rounded-full bg-primary px-4 py-2 text-sm font-semibold text-primary-foreground disabled:opacity-50";

  return (
    <section
      aria-labelledby="service-desk-heading"
      className="mt-6 rounded-3xl border border-border bg-card p-5 shadow-soft"
    >
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 id="service-desk-heading" className="font-display text-xl font-bold">
            Orders & billing
          </h2>
          <p className="text-sm text-muted-foreground">
            Take a table order, review the bill and record payment.
          </p>
        </div>
        <button
          type="button"
          onClick={() => void refresh()}
          className="inline-flex items-center gap-2 rounded-full border px-3 py-2 text-sm"
        >
          <RefreshCw className="h-4 w-4" /> Refresh
        </button>
      </div>
      <label className="mt-4 block text-sm font-semibold">
        Table
        <select
          aria-label="Select table"
          value={tableId}
          disabled={busy}
          onChange={(e) => setTableId(e.target.value)}
          className={`${inputClass} ml-3 max-w-full`}
        >
          <option value="">Select a table</option>
          {tables.data?.map((t) => (
            <option key={t.id} value={t.id}>
              Table {t.tableNumber}
              {!t.isActive ? " (inactive)" : ""}
            </option>
          ))}
        </select>
      </label>
      {tables.isLoading && (
        <p role="status" className="mt-3 text-sm">
          Loading tables…
        </p>
      )}
      {tables.error && (
        <p role="alert" className="mt-3 text-sm text-destructive">
          {tables.error.message}
        </p>
      )}
      {error && (
        <p role="alert" className="mt-4 rounded-xl bg-destructive/10 p-3 text-sm text-destructive">
          {error}
        </p>
      )}
      {needsSignIn && (
        <button
          type="button"
          className={`${actionClass} mt-3`}
          onClick={() => {
            localStorage.removeItem("tablo_token");
            window.location.assign("/auth");
          }}
        >
          Sign in again
        </button>
      )}
      {tableId && (
        <div className="mt-5 grid gap-6 xl:grid-cols-2">
          <div>
            <h3 className="flex items-center gap-2 font-semibold">
              <ShoppingBag className="h-4 w-4" /> New items · Table {table?.tableNumber}
            </h3>
            {!table?.isActive && (
              <p className="mt-2 text-sm text-warning">
                This table is inactive. Existing bills can still be reviewed.
              </p>
            )}
            {pendingOrder && (
              <p role="status" className="mt-2 rounded-xl bg-warning/10 p-3 text-sm">
                A submission is awaiting confirmation. Retry it before changing these items.
              </p>
            )}
            <label className="relative mt-3 block">
              <Search className="absolute left-3 top-3 h-4 w-4 text-muted-foreground" />
              <input
                aria-label="Search menu"
                placeholder="Search menu or category"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                className={`${inputClass} w-full pl-9`}
              />
            </label>
            {menuLoading && (
              <p role="status" className="mt-3 text-sm">
                Loading menu…
              </p>
            )}
            {menuError && (
              <p role="alert" className="mt-3 text-sm text-destructive">
                {menuError.message}
              </p>
            )}
            <div className="mt-3 max-h-64 space-y-2 overflow-y-auto">
              {!menuLoading && !menuError && shown.length === 0 && (
                <p className="text-sm text-muted-foreground">No available items match.</p>
              )}
              {shown.map((item) => {
                const qty = cart.find((l) => l.menuItemId === item.id)?.quantity || 0;
                return (
                  <div
                    key={item.id}
                    className="flex items-center justify-between gap-3 rounded-xl border border-border p-3"
                  >
                    <div>
                      <p className="text-sm font-semibold">{item.name}</p>
                      <p className="text-xs text-muted-foreground">
                        {item.category} · {formatINR(item.price)}
                      </p>
                    </div>
                    <div className="flex items-center gap-2">
                      <button
                        type="button"
                        aria-label={`Remove ${item.name}`}
                        disabled={!qty || !!pendingOrder || busy}
                        onClick={() => changeQuantity(item.id, -1)}
                        className="rounded-full border p-1.5 disabled:opacity-40"
                      >
                        <Minus className="h-4 w-4" />
                      </button>
                      <span className="min-w-5 text-center text-sm">{qty}</span>
                      <button
                        type="button"
                        aria-label={`Add ${item.name}`}
                        disabled={!table?.isActive || !!pendingOrder || busy}
                        onClick={() => changeQuantity(item.id, 1)}
                        className="rounded-full border p-1.5 disabled:opacity-40"
                      >
                        <Plus className="h-4 w-4" />
                      </button>
                    </div>
                  </div>
                );
              })}
            </div>
            {cart.length > 0 && (
              <div className="mt-4 rounded-xl bg-secondary/50 p-3 text-sm">
                <ul className="space-y-1">
                  {cart.map((l) => (
                    <li key={l.menuItemId} className="flex justify-between gap-3">
                      <span>
                        {l.quantity} × {l.name}
                      </span>
                      <span>{formatINR(l.quantity * l.price)}</span>
                    </li>
                  ))}
                </ul>
                <p className="mt-3 text-xs text-muted-foreground">
                  Estimated new-items total: {formatINR(subtotal + Math.round(subtotal * 0.05))}.
                  Final prices and order tax are calculated by the server.
                </p>
              </div>
            )}
            <button
              type="button"
              onClick={() => void place()}
              disabled={busy || !cart.length || (!table?.isActive && !pendingOrder)}
              className={`${actionClass} mt-4`}
            >
              {busy && <Loader2 className="h-4 w-4 animate-spin" />}
              {pendingOrder ? "Retry saved submission" : "Send items to kitchen"}
            </button>
          </div>
          <div>
            <h3 className="flex items-center gap-2 font-semibold">
              <Receipt className="h-4 w-4" /> Bill review
            </h3>
            {billQuery.isLoading && (
              <p role="status" className="mt-3 text-sm">
                Loading bill…
              </p>
            )}
            {billQuery.error && (
              <p role="alert" className="mt-3 text-sm text-destructive">
                {billQuery.error.message}
              </p>
            )}
            {!billQuery.isLoading && !billQuery.error && !bill && (
              <p className="mt-3 text-sm text-muted-foreground">
                No open bill. New orders start one automatically.
              </p>
            )}
            {!!billQuery.data?.unbilledActiveOrderCount && (
              <button
                type="button"
                disabled={busy}
                onClick={async () => {
                  if (running.current) return;
                  running.current = true;
                  setBusy(true);
                  try {
                    await openTableBill(restaurantId, tableId);
                    await refresh();
                  } catch (e) {
                    setError(e instanceof Error ? e.message : "Could not start bill");
                  } finally {
                    running.current = false;
                    setBusy(false);
                  }
                }}
                className={`${actionClass} mt-3`}
              >
                Include current active orders in bill
              </button>
            )}
            {bill && (
              <>
                <BillContents bill={bill} />
                <p className="mt-3 text-xs text-muted-foreground">
                  {bill.canSettle
                    ? "Confirm payment collected before recording settlement."
                    : "Serve all chargeable orders before settlement."}
                </p>
                <label className="mt-3 block text-sm font-semibold">
                  Payment method{" "}
                  <select
                    aria-label="Payment method"
                    value={method}
                    onChange={(e) => setMethod(e.target.value)}
                    className={`${inputClass} ml-2`}
                  >
                    <option>Cash</option>
                    <option>UPI</option>
                    <option>Card</option>
                  </select>
                </label>
                <button
                  type="button"
                  disabled={
                    busy ||
                    !bill.canSettle ||
                    !!billQuery.error ||
                    !!billQuery.data?.unbilledActiveOrderCount ||
                    !!pendingOrder
                  }
                  onClick={() => {
                    setError("");
                    setUncertainSettlement(false);
                    setSettlement({
                      bill,
                      request: {
                        operationId: crypto.randomUUID(),
                        expectedRevision: bill.revision,
                        expectedTotal: bill.totalAmount,
                        paymentMethod: method,
                      },
                    });
                  }}
                  className={`${actionClass} mt-3`}
                >
                  Review settlement
                </button>
              </>
            )}
            {receipt && (
              <div
                role="status"
                className="mt-4 rounded-xl border border-success bg-success/10 p-3"
              >
                <p className="font-semibold">Bill settled · {formatINR(receipt.totalAmount)}</p>
                <p className="text-sm">
                  {receipt.paymentMethod} ·{" "}
                  {receipt.settledAtUtc && new Date(receipt.settledAtUtc).toLocaleString()}
                </p>
                <PaymentReceipt bill={receipt} />
              </div>
            )}
            {!!billQuery.data?.recentSettlements.length && (
              <details className="mt-5">
                <summary className="cursor-pointer text-sm font-semibold">
                  Recent settled bills
                </summary>
                {billQuery.data.recentSettlements.map((paid) => (
                  <details key={paid.id} className="mt-3 rounded-xl border p-3">
                    <summary className="cursor-pointer text-sm">
                      {paid.settledAtUtc && new Date(paid.settledAtUtc).toLocaleString()} ·{" "}
                      {formatINR(paid.totalAmount)} · {paid.paymentMethod}
                    </summary>
                    <BillContents bill={paid} />
                    <PaymentReceipt bill={paid} />
                    <p className="mt-2 break-all text-xs text-muted-foreground">
                      Receipt {paid.id}
                    </p>
                  </details>
                ))}
              </details>
            )}
            <p className="mt-4 text-xs text-muted-foreground">
              Historical served orders are not imported automatically. Payment is collected
              externally; this panel records full settlement.
            </p>
          </div>
        </div>
      )}
      {settlement && (
        <Dialog
          open
          onOpenChange={(open) => {
            if (!open && !busy && !uncertainSettlement) setSettlement(null);
          }}
        >
          <DialogContent className="max-h-[90vh] w-[calc(100%-2rem)] max-w-lg overflow-y-auto rounded-3xl bg-card p-6 shadow-glow">
            <DialogTitle className="font-display text-xl font-bold">
              Settle Table {settlement.bill.tableNumber}
            </DialogTitle>
            <DialogDescription className="mt-2 text-sm text-muted-foreground">
              Record {formatINR(settlement.request.expectedTotal)} collected by{" "}
              {settlement.request.paymentMethod}. This does not process a card or UPI payment.
            </DialogDescription>
            <BillContents bill={settlement.bill} />
            {uncertainSettlement && (
              <p className="mt-3 text-sm text-warning">
                This saved operation may already have completed. Retry to retrieve its result
                safely.
              </p>
            )}
            {error && (
              <p role="alert" className="mt-3 text-sm text-destructive">
                {error}
              </p>
            )}
            {needsSignIn && (
              <button
                type="button"
                className={actionClass}
                onClick={() => {
                  localStorage.removeItem("tablo_token");
                  window.location.assign("/auth");
                }}
              >
                Sign in again
              </button>
            )}
            <div className="mt-5 flex justify-end gap-2">
              <button
                type="button"
                disabled={busy || uncertainSettlement}
                onClick={() => setSettlement(null)}
                className="rounded-full border px-4 py-2 text-sm"
              >
                Back
              </button>
              <button
                autoFocus
                type="button"
                disabled={busy}
                onClick={() => void settle()}
                className={actionClass}
              >
                {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
                {uncertainSettlement ? "Confirm saved settlement" : "Confirm payment collected"}
              </button>
            </div>
          </DialogContent>
        </Dialog>
      )}
    </section>
  );
}

function BillContents({ bill }: { bill: TableBill }) {
  return (
    <div className="mt-3 text-sm">
      <div className="max-h-64 space-y-3 overflow-y-auto">
        {bill.orders.map((order) => {
          const excluded = order.status === "Cancelled" || order.status === "Rejected";
          return (
            <div
              key={order.id}
              className={`rounded-xl border border-border p-3 ${excluded ? "text-muted-foreground" : ""}`}
            >
              <p className="mb-2 text-xs font-semibold">
                ORD-{String(order.orderNumber).padStart(4, "0")} · {order.status}
                {excluded ? " · Excluded from bill" : ""}
              </p>
              <ul className="space-y-1">
                {order.items.map((item) => (
                  <li
                    key={item.id}
                    className={`flex justify-between gap-3 ${excluded ? "line-through" : ""}`}
                  >
                    <span>
                      {item.quantity} × {item.name}
                    </span>
                    <span>{formatINR(item.quantity * item.unitPrice)}</span>
                  </li>
                ))}
              </ul>
            </div>
          );
        })}
      </div>
      <dl className="mt-3 space-y-1 border-t pt-3">
        <div className="flex justify-between">
          <dt>Subtotal</dt>
          <dd>{formatINR(bill.subtotal)}</dd>
        </div>
        <div className="flex justify-between">
          <dt>GST (5%, rounded per order)</dt>
          <dd>{formatINR(bill.tax)}</dd>
        </div>
        {bill.discount !== 0 && (
          <div className="flex justify-between">
            <dt>Discount</dt>
            <dd>{formatINR(bill.discount)}</dd>
          </div>
        )}
        <div className="flex justify-between pt-2 text-base font-bold">
          <dt>Total payable</dt>
          <dd>{formatINR(bill.totalAmount)}</dd>
        </div>
      </dl>
    </div>
  );
}
