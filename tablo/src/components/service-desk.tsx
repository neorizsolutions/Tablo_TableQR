import { useEffect, useRef, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { HubConnectionBuilder } from "@microsoft/signalr";
import { useMenu } from "@/hooks/use-menu";
import { API_BASE_URL } from "@/services/apiClient";
import {
  getBillReview,
  getDiningGroups,
  getServiceTables,
  submitStaffOrder,
  ServiceDeskError,
  type StaffOrderRequest,
} from "@/services/service-desk";
import { FloorRecovery } from "./floor-recovery";
import { FloorBills } from "./floor-bills";
import { formatINR } from "@/lib/mock-data";
import { clearDefinitivelyFailedOperation } from "@/services/pending-operation";
interface Line {
  menuItemId: string;
  name: string;
  quantity: number;
  price: number;
}
interface Pending {
  request: StaffOrderRequest;
  lines: Line[];
}
function saved(key: string): Pending | null {
  try {
    return JSON.parse(localStorage.getItem(key) || "null");
  } catch {
    return null;
  }
}
const control = "rounded-xl border bg-background px-3 py-2 text-sm";
const button = "rounded-full border px-4 py-2 text-sm font-semibold disabled:opacity-40";

export function ServiceDesk({
  restaurantId,
  employeeId,
}: {
  restaurantId: string;
  employeeId: string;
}) {
  const cache = useQueryClient();
  const [tableId, setTable] = useState("");
  const [groupId, setGroup] = useState("");
  const [cart, setCart] = useState<Line[]>([]);
  const [pending, setPending] = useState<Pending | null>(null);
  const [search, setSearch] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [auth, setAuth] = useState(false);
  const running = useRef(false);
  const tables = useQuery({
    queryKey: ["serviceTables", restaurantId],
    queryFn: () => getServiceTables(restaurantId),
    refetchInterval: 15000,
  });
  const groups = useQuery({
    queryKey: ["diningGroups", restaurantId, tableId],
    queryFn: () => getDiningGroups(restaurantId, tableId),
    enabled: !!tableId,
    refetchInterval: 15000,
  });
  const review = useQuery({
    queryKey: ["tableBill", restaurantId, tableId, groupId],
    queryFn: () => getBillReview(restaurantId, tableId, groupId || undefined),
    enabled: !!tableId,
    refetchInterval: 15000,
  });
  const menu = useMenu(restaurantId);
  const table = tables.data?.find((t) => t.id === tableId);
  const key = `tablo_staff_order_${employeeId}_${restaurantId}_${tableId}${groupId ? `_${groupId}` : ""}`;
  useEffect(() => {
    const old = saved(key);
    setPending(old);
    setCart(old?.lines || []);
    setError("");
    setAuth(false);
  }, [key]);
  useEffect(() => {
    let disposed = false;
    const connection = new HubConnectionBuilder()
      .withUrl(API_BASE_URL.replace(/\/api\/?$/, "/hubs/order"), {
        accessTokenFactory: () => localStorage.getItem("tablo_token") || "",
      })
      .withAutomaticReconnect()
      .build();
    const refresh = () => {
      for (const queryKey of [
        ["tableBill", restaurantId],
        ["diningGroups", restaurantId],
        ["orders"],
        ["serviceRequests"],
        ["paymentReports", restaurantId],
      ])
        void cache.invalidateQueries({ queryKey });
    };
    const join = async () => {
      await connection.invoke("JoinRestaurantGroup");
      refresh();
    };
    for (const event of ["BillUpdated", "NewOrder", "OrderStatusUpdated"])
      connection.on(event, refresh);
    connection.onreconnected(() => join().catch(() => undefined));
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
  const groupRequired = !!groups.data?.length && !groupId;
  function change(id: string, delta: number) {
    if (pending || busy) return;
    const item = menu.data?.find((i) => i.id === id);
    if (!item) return;
    setCart((lines) => {
      const old = lines.find((i) => i.menuItemId === id);
      return old
        ? lines
            .map((i) => (i.menuItemId === id ? { ...i, quantity: i.quantity + delta } : i))
            .filter((i) => i.quantity > 0)
        : delta > 0
          ? [...lines, { menuItemId: id, name: item.name, price: item.price, quantity: 1 }]
          : lines;
    });
  }
  async function place() {
    if (running.current || !tableId || !cart.length) return;
    running.current = true;
    setBusy(true);
    setError("");
    try {
      const operation = pending || {
        lines: cart,
        request: {
          operationId: crypto.randomUUID(),
          tableId,
          diningGroupId: groupId || undefined,
          items: cart.map((i) => ({ menuItemId: i.menuItemId, quantity: i.quantity })),
        },
      };
      localStorage.setItem(key, JSON.stringify(operation));
      setPending(operation);
      await submitStaffOrder(restaurantId, operation.request);
      localStorage.removeItem(key);
      setPending(null);
      setCart([]);
      await Promise.all([
        cache.invalidateQueries({ queryKey: ["orders"] }),
        cache.invalidateQueries({ queryKey: ["tableBill", restaurantId] }),
      ]);
    } catch (e) {
      setAuth(e instanceof ServiceDeskError && [401, 403].includes(e.status));
      if (
        clearDefinitivelyFailedOperation(
          localStorage,
          key,
          e instanceof ServiceDeskError ? e.status : undefined,
        )
      )
        setPending(null);
      setError(e instanceof Error ? e.message : "Submission result is unknown. Retry it.");
    } finally {
      running.current = false;
      setBusy(false);
    }
  }
  return (
    <section aria-labelledby="service-desk-heading" className="mt-5 rounded-3xl border bg-card p-5">
      <h2 id="service-desk-heading" className="font-display text-2xl font-bold">
        Orders & billing
      </h2>
      <p className="my-2 text-sm text-muted-foreground">
        Take orders by table and group, split bills, record collections or transfer a visit.
      </p>
      <FloorRecovery restaurantId={restaurantId} employeeId={employeeId} />
      <div className="flex flex-wrap gap-3">
        <label className="grid text-sm">
          Table
          <select
            aria-label="Select table"
            className={control}
            value={tableId}
            disabled={busy || !!pending}
            onChange={(e) => {
              setTable(e.target.value);
              setGroup("");
            }}
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
        {tableId && (
          <label className="grid text-sm">
            Dining group
            <select
              aria-label="Dining group"
              className={control}
              value={groupId}
              disabled={busy || !!pending}
              onChange={(e) => setGroup(e.target.value)}
            >
              <option value="">Whole table / legacy bills</option>
              {groups.data?.map((g) => (
                <option key={g.id} value={g.id}>
                  Table {table?.tableNumber}
                  {g.label}
                </option>
              ))}
            </select>
          </label>
        )}
        <button
          className={button}
          onClick={() => {
            void cache.invalidateQueries({ queryKey: ["tableBill", restaurantId] });
            void cache.invalidateQueries({ queryKey: ["diningGroups", restaurantId] });
          }}
        >
          Refresh
        </button>
      </div>
      {tables.isLoading && <p>Loading tables…</p>}
      {tables.error && <p role="alert">{tables.error.message}</p>}
      {groups.error && <p role="alert">{groups.error.message}</p>}
      {review.error && <p role="alert">{review.error.message}</p>}
      {tableId && (
        <div className="mt-5 grid gap-6 xl:grid-cols-2">
          <div>
            <h3 className="font-semibold">
              New items · Table {table?.tableNumber}
              {groups.data?.find((g) => g.id === groupId)?.label}
            </h3>
            {groupRequired && (
              <p className="mt-2 text-sm">
                Select a dining group before adding food. Whole table remains available for
                reviewing earlier bills.
              </p>
            )}
            {groupId && groups.data && !groups.data.some((g) => g.id === groupId) && (
              <p role="alert">This group moved or closed. Select the current table and group.</p>
            )}
            <input
              aria-label="Search menu"
              className={`${control} my-3 w-full`}
              value={search}
              placeholder="Search menu"
              onChange={(e) => setSearch(e.target.value)}
            />
            {menu.isLoading && <p>Loading menu…</p>}
            {menu.error && <p role="alert">{menu.error.message}</p>}
            <div className="max-h-96 space-y-2 overflow-y-auto">
              {menu.data
                ?.filter(
                  (i) =>
                    i.available &&
                    `${i.name} ${i.category}`.toLowerCase().includes(search.toLowerCase()),
                )
                .map((i) => {
                  const quantity = cart.find((l) => l.menuItemId === i.id)?.quantity || 0;
                  return (
                    <div
                      key={i.id}
                      className="flex items-center justify-between gap-3 rounded-xl border p-3"
                    >
                      <div>
                        <p className="font-semibold">{i.name}</p>
                        <p className="text-xs">
                          {i.category} · {formatINR(i.price)}
                        </p>
                      </div>
                      <div className="flex items-center gap-2">
                        <button
                          aria-label={`Remove ${i.name}`}
                          className={button}
                          disabled={!quantity || busy || !!pending}
                          onClick={() => change(i.id, -1)}
                        >
                          −
                        </button>
                        <span>{quantity}</span>
                        <button
                          aria-label={`Add ${i.name}`}
                          className={button}
                          disabled={!table?.isActive || groupRequired || busy || !!pending}
                          onClick={() => change(i.id, 1)}
                        >
                          +
                        </button>
                      </div>
                    </div>
                  );
                })}
            </div>
            {!!cart.length && (
              <div className="mt-3 rounded-xl bg-secondary p-3">
                {cart.map((i) => (
                  <p key={i.menuItemId}>
                    {i.quantity} × {i.name}
                  </p>
                ))}
                <p>
                  Items subtotal {formatINR(cart.reduce((sum, i) => sum + i.price * i.quantity, 0))}
                  ; server calculates tax.
                </p>
              </div>
            )}
            {pending && (
              <p className="mt-3">Submission awaiting confirmation. Retry before editing.</p>
            )}
            <button
              className={`${button} mt-3 bg-primary text-primary-foreground`}
              disabled={busy || !cart.length || (!pending && (!table?.isActive || groupRequired))}
              onClick={() => void place()}
            >
              {busy ? "Sending…" : pending ? "Retry saved submission" : "Send items to kitchen"}
            </button>
            {error && (
              <p role="alert" className="mt-3 text-destructive">
                {error}
              </p>
            )}
            {auth && (
              <a
                className="mt-2 block underline"
                href="/auth"
                onClick={() => localStorage.removeItem("tablo_token")}
              >
                Sign in again with the same account, then retry the saved submission
              </a>
            )}
          </div>
          <FloorBills
            key={`${tableId}_${groupId}`}
            restaurantId={restaurantId}
            employeeId={employeeId}
            tableId={tableId}
            groupId={groupId}
            review={review.data}
            tables={tables.data || []}
            onTransferred={setTable}
          />
        </div>
      )}
    </section>
  );
}
