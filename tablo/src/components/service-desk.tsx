import { useEffect, useMemo, useRef, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { HubConnectionBuilder } from "@microsoft/signalr";
import { toast } from "sonner";
import { useMenu } from "@/hooks/use-menu";
import { API_BASE_URL } from "@/services/apiClient";
import {
  getBillReview,
  getDiningGroups,
  getServiceTables,
  submitStaffOrder,
  createDiningGroup,
  splitTableBill,
  getBillSplitPreview,
  ServiceDeskError,
  type StaffOrderRequest,
  type ServiceTable,
  type DiningGroup,
  type TableBill,
  type BillReview,
} from "@/services/service-desk";
import { FloorRecovery } from "./floor-recovery";
import { FloorBills } from "./floor-bills";
import { formatINR } from "@/lib/mock-data";
import { clearDefinitivelyFailedOperation } from "@/services/pending-operation";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  Search,
  Filter,
  Plus,
  ArrowLeft,
  RotateCw,
  CheckCircle2,
  Split,
  Users,
  Utensils,
  AlertCircle,
  Loader2,
  Sparkles,
  Clock,
  Receipt,
  ShoppingBag,
  Flame,
  Pause,
} from "lucide-react";

interface Line {
  lineId: string;
  menuItemId: string;
  name: string;
  quantity: number;
  price: number;
  releaseState?: "Fired" | "Held";
  courseName?: string;
  packingIntent?: "DineIn" | "Takeaway";
  selectedModifiers?: { modifierGroupId: string; modifierOptionId: string; optionName: string; priceDelta: number }[];
  specialInstructions?: string;
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

function getDraftKey(restaurantId: string, employeeId: string, tableId: string, groupId?: string): string {
  return `tablo_staff_draft_${restaurantId}_${employeeId}_${tableId}_${groupId || "whole"}`;
}

function getTableDraftItemsCount(restaurantId: string, employeeId: string, tableId: string): number {
  let count = 0;
  try {
    for (let i = 0; i < localStorage.length; i++) {
      const k = localStorage.key(i);
      if (k && k.startsWith(`tablo_staff_draft_${restaurantId}_${employeeId}_${tableId}`)) {
        const d = JSON.parse(localStorage.getItem(k) || "[]");
        if (Array.isArray(d)) {
          count += d.reduce((s: number, item: any) => s + (item.quantity || 0), 0);
        }
      }
    }
  } catch {
    // Ignored
  }
  return count;
}

const control = "rounded-xl border bg-background px-3 py-2 text-sm";
const button = "rounded-full border px-4 py-2 text-sm font-semibold disabled:opacity-40";

export function ServiceDesk({
  restaurantId,
  employeeId,
  initialTableId,
  initialGroupId,
  mode = "all",
}: {
  restaurantId: string;
  employeeId: string;
  initialTableId?: string;
  initialGroupId?: string;
  mode?: "all" | "order-only" | "bills-only";
}) {
  const cache = useQueryClient();
  const [tableId, setTable] = useState(initialTableId || "");
  const [groupId, setGroup] = useState(initialGroupId || "");
  const [tableSearch, setTableSearch] = useState("");
  const [tableStatusFilter, setTableStatusFilter] = useState<"all" | "available" | "active" | "drafts">("all");
  const [isNewGroupModalOpen, setIsNewGroupModalOpen] = useState(false);
  const [isSplitBillModalOpen, setIsSplitBillModalOpen] = useState(false);

  useEffect(() => {
    setTable(initialTableId || "");
    setGroup(initialGroupId || "");
  }, [initialTableId, initialGroupId]);

  const [loadedDraftKey, setLoadedDraftKey] = useState("");
  const [cart, setCart] = useState<Line[]>([]);
  const [pending, setPending] = useState<Pending | null>(null);
  const [search, setSearch] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [auth, setAuth] = useState(false);
  const [customizingItem, setCustomizingItem] = useState<any | null>(null);
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

  // Multi-draft persistence: Load draft when switching table or dining group
  useEffect(() => {
    if (!tableId) {
      setCart([]);
      setPending(null);
      return;
    }
    const currentKey = getDraftKey(restaurantId, employeeId, tableId, groupId);
    const oldPending = saved(key);
    setPending(oldPending);
    if (oldPending) {
      setCart(oldPending.lines || []);
    } else {
      try {
        const savedLines = JSON.parse(localStorage.getItem(currentKey) || "[]");
        if (Array.isArray(savedLines)) {
          setCart(savedLines);
        } else {
          setCart([]);
        }
      } catch {
        setCart([]);
      }
    }
    setError("");
    setAuth(false);
    setLoadedDraftKey(key);
  }, [key, restaurantId, employeeId, tableId, groupId]);

  // Persist cart to draft localStorage on change
  useEffect(() => {
    if (mode === "bills-only" || !tableId || pending || loadedDraftKey !== key) return;
    const currentKey = getDraftKey(restaurantId, employeeId, tableId, groupId);
    if (cart.length > 0) {
      localStorage.setItem(currentKey, JSON.stringify(cart));
    } else {
      localStorage.removeItem(currentKey);
    }
  }, [cart, tableId, groupId, restaurantId, employeeId, pending, loadedDraftKey, key, mode]);

  // SignalR real-time updates
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
        ["serviceTables", restaurantId],
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

  const activeGroup = groups.data?.find((g) => g.id === groupId);
  const groupRequired = !!groups.data?.length && !groupId;
  const groupUnavailable = !!groupId && !activeGroup;
  const orderEntryBlocked = groupRequired || groupUnavailable || !groups.data;

  function addCustomLine(lineData: Omit<Line, "lineId">) {
    const modKey = (lineData.selectedModifiers || [])
      .map((m) => m.modifierOptionId)
      .sort()
      .join("|");
    const lineId = `${lineData.menuItemId}_${modKey}_${lineData.courseName || ""}_${lineData.releaseState || "Fired"}_${(lineData.specialInstructions || "").toLowerCase()}`;

    setCart((prev) => {
      const idx = prev.findIndex((l) => l.lineId === lineId);
      if (idx !== -1) {
        return prev.map((l, i) => (i === idx ? { ...l, quantity: l.quantity + lineData.quantity } : l));
      }
      return [...prev, { ...lineData, lineId }];
    });
  }

  function updateLineQty(lineId: string, delta: number) {
    if (pending || busy) return;
    setCart((lines) => {
      const target = lines.find((l) => l.lineId === lineId);
      if (!target) return lines;
      const next = target.quantity + delta;
      if (next <= 0) return lines.filter((l) => l.lineId !== lineId);
      return lines.map((l) => (l.lineId === lineId ? { ...l, quantity: next } : l));
    });
  }

  function change(menuItemId: string, delta: number) {
    if (pending || busy) return;
    const item = menu.data?.find((i) => i.id === menuItemId);
    if (!item) return;

    setCart((lines) => {
      const line = lines.find((l) => l.menuItemId === menuItemId && !l.selectedModifiers?.length && !l.courseName);
      if (!line && delta > 0) {
        return [
          ...lines,
          {
            lineId: `${menuItemId}_standard`,
            menuItemId,
            name: item.name,
            quantity: 1,
            price: item.price,
            releaseState: "Fired",
            packingIntent: "DineIn",
          },
        ];
      }
      if (!line) return lines;
      const next = line.quantity + delta;
      if (next <= 0) return lines.filter((l) => l !== line);
      return lines.map((l) => (l === line ? { ...l, quantity: next } : l));
    });
  }

  function updateLineCourse(lineId: string, courseName: string) {
    if (pending || busy) return;
    setCart((lines) =>
      lines.map((i) => (i.lineId === lineId ? { ...i, courseName: courseName || undefined } : i)),
    );
  }

  function toggleLineRelease(lineId: string) {
    if (pending || busy) return;
    setCart((lines) =>
      lines.map((i) =>
        i.lineId === lineId ? { ...i, releaseState: i.releaseState === "Held" ? "Fired" : "Held" } : i,
      ),
    );
  }

  function toggleLinePacking(lineId: string) {
    if (pending || busy) return;
    setCart((lines) =>
      lines.map((i) =>
        i.lineId === lineId ? { ...i, packingIntent: i.packingIntent === "Takeaway" ? "DineIn" : "Takeaway" } : i,
      ),
    );
  }

  async function place() {
    if (running.current || !tableId || !cart.length || (!pending && orderEntryBlocked)) return;
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
          items: cart.map((i) => ({
            menuItemId: i.menuItemId,
            quantity: i.quantity,
            releaseState: i.releaseState || "Fired",
            courseName: i.courseName || undefined,
            packingIntent: i.packingIntent || "DineIn",
            specialInstructions: i.specialInstructions || undefined,
            selectedModifiers: i.selectedModifiers?.map((m) => ({
              modifierGroupId: m.modifierGroupId,
              modifierOptionId: m.modifierOptionId,
            })),
          })),
        },
      };
      localStorage.setItem(key, JSON.stringify(operation));
      setPending(operation);
      await submitStaffOrder(restaurantId, operation.request);
      localStorage.removeItem(key);
      const currentDraftKey = getDraftKey(restaurantId, employeeId, tableId, groupId);
      localStorage.removeItem(currentDraftKey);
      setPending(null);
      setCart([]);
      toast.success(`Order placed successfully for Table #${table?.tableNumber}!`);
      await Promise.all([
        cache.invalidateQueries({ queryKey: ["orders"] }),
        cache.invalidateQueries({ queryKey: ["tableBill", restaurantId] }),
        cache.invalidateQueries({ queryKey: ["serviceTables", restaurantId] }),
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

  // Filtered tables list for the touch-friendly grid
  const filteredTables = useMemo(() => {
    const list = tables.data || [];
    return list.filter((t) => {
      const matchesSearch =
        t.tableNumber.toString().includes(tableSearch.trim()) ||
        `Table ${t.tableNumber}`.toLowerCase().includes(tableSearch.toLowerCase().trim());
      if (!matchesSearch) return false;

      if (tableStatusFilter === "available") {
        return t.status ? t.status === "Available" : !t.activeOrdersCount && !t.openBillTotal;
      }
      if (tableStatusFilter === "active") {
        return t.status === "Occupied" || t.status === "Bill Ready" || !!t.activeOrdersCount;
      }
      if (tableStatusFilter === "drafts") {
        return getTableDraftItemsCount(restaurantId, employeeId, t.id) > 0;
      }
      return true;
    });
  }, [tables.data, tableSearch, tableStatusFilter, restaurantId, employeeId]);


  return (
    <section aria-labelledby="service-desk-heading" className="mt-4 rounded-3xl border bg-card p-4 sm:p-6 shadow-xs">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-3 border-b border-border/60">
        <div>
          <h2 id="service-desk-heading" className="font-display text-xl sm:text-2xl font-bold">
            {mode === "bills-only" ? "Table Billing & Settlement" : "Staff Order Taking"}
          </h2>
          <p className="text-xs text-muted-foreground mt-0.5">
            {mode === "bills-only" ? "Select a table to review bills, collect payments and print receipts." : "Select a table to start an order, switch between guest drafts without losing work, and split bills accurately."}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            className="text-xs gap-1.5"
            onClick={() => {
              void cache.invalidateQueries({ queryKey: ["serviceTables", restaurantId] });
              void cache.invalidateQueries({ queryKey: ["tableBill", restaurantId] });
              void cache.invalidateQueries({ queryKey: ["diningGroups", restaurantId] });
            }}
          >
            <RotateCw className="h-3.5 w-3.5" />
            Refresh Floor
          </Button>
        </div>
      </div>

      {mode !== "bills-only" && <FloorRecovery restaurantId={restaurantId} employeeId={employeeId} />}

      {/* =========================================================================
          SECTION 1: SEARCHABLE TOUCH-FRIENDLY TABLE GRID (When No Table Selected)
          ========================================================================= */}
      {!tableId ? (
        <div className="mt-5 space-y-4">
          {mode === "bills-only" && tables.data?.some((t) => t.billReadyGroups?.length) && (
            <section className="rounded-2xl border border-primary/25 bg-primary/5 p-4" aria-labelledby="bills-ready-heading">
              <h3 id="bills-ready-heading" className="font-display text-base font-bold">Bills ready to print</h3>
              <div className="mt-3 flex flex-wrap gap-2">
                {tables.data.flatMap((t) => (t.billReadyGroups ?? []).map((group) => (
                  <Button
                    key={`${t.id}_${group.diningGroupId ?? "whole"}`}
                    variant="outline"
                    className="rounded-full bg-card"
                    onClick={() => {
                      setTable(t.id);
                      setGroup(group.diningGroupId ?? "");
                    }}
                  >
                    Table {t.tableNumber}{group.groupLabel}
                  </Button>
                )))}
              </div>
            </section>
          )}
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
            <div className="relative flex-1 max-w-md">
              <Search className="absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                aria-label="Search tables"
                value={tableSearch}
                onChange={(e) => setTableSearch(e.target.value)}
                placeholder="Search table number (e.g. 1, 4, 10)..."
                className="pl-9 text-xs font-medium"
              />
            </div>

            {/* Status Filter Chips */}
            <div className="flex items-center gap-1.5 overflow-x-auto text-xs pb-1">
              <button
                type="button"
                onClick={() => setTableStatusFilter("all")}
                className={`px-3 py-1.5 rounded-full font-semibold border text-xs transition ${
                  tableStatusFilter === "all"
                    ? "bg-primary text-primary-foreground border-primary"
                    : "bg-secondary/40 text-muted-foreground hover:bg-secondary border-border"
                }`}
              >
                All Tables ({tables.data?.length || 0})
              </button>
              <button
                type="button"
                onClick={() => setTableStatusFilter("available")}
                className={`px-3 py-1.5 rounded-full font-semibold border text-xs transition ${
                  tableStatusFilter === "available"
                    ? "bg-success text-success-foreground border-success"
                    : "bg-secondary/40 text-muted-foreground hover:bg-secondary border-border"
                }`}
              >
                Available ({tables.data?.filter((t) => t.status ? t.status === "Available" : !t.activeOrdersCount && !t.openBillTotal).length || 0})
              </button>
              <button
                type="button"
                onClick={() => setTableStatusFilter("active")}
                className={`px-3 py-1.5 rounded-full font-semibold border text-xs transition ${
                  tableStatusFilter === "active"
                    ? "bg-primary text-primary-foreground border-primary"
                    : "bg-secondary/40 text-muted-foreground hover:bg-secondary border-border"
                }`}
              >
                Active / Occupied ({tables.data?.filter((t) => t.status === "Occupied" || t.status === "Bill Ready" || !!t.activeOrdersCount).length || 0})
              </button>
              {mode !== "bills-only" && <button
                type="button"
                onClick={() => setTableStatusFilter("drafts")}
                className={`px-3 py-1.5 rounded-full font-semibold border text-xs transition ${
                  tableStatusFilter === "drafts"
                    ? "bg-amber-600 text-white border-amber-600"
                    : "bg-secondary/40 text-muted-foreground hover:bg-secondary border-border"
                }`}
              >
                With Drafts ({tables.data?.filter((t) => getTableDraftItemsCount(restaurantId, employeeId, t.id) > 0).length || 0})
              </button>}
            </div>
          </div>

          {/* Table Loading State */}
          {tables.isLoading && (
            <div className="p-12 text-center space-y-3">
              <Loader2 className="h-7 w-7 animate-spin text-primary mx-auto" />
              <p className="text-xs text-muted-foreground">Loading restaurant floor tables...</p>
            </div>
          )}

          {/* Table Error State with Distinct Retry Action */}
          {tables.error && (
            <div className="p-6 rounded-2xl border border-destructive/30 bg-destructive/5 text-center space-y-3">
              <AlertCircle className="h-8 w-8 text-destructive mx-auto" />
              <p role="alert" className="text-sm font-semibold text-destructive">
                {tables.error.message || "Failed to load floor tables."}
              </p>
              <p className="text-xs text-muted-foreground">
                Unable to connect to table status service. Please verify your connection or retry.
              </p>
              <Button size="sm" variant="outline" onClick={() => tables.refetch()} className="text-xs gap-1.5 mt-2">
                <RotateCw className="h-3.5 w-3.5" /> Retry Table Connection
              </Button>
            </div>
          )}

          {/* Empty State */}
          {!tables.isLoading && !tables.error && filteredTables.length === 0 && (
            <div className="p-12 text-center rounded-2xl border border-dashed border-border bg-card space-y-2">
              <p className="text-sm font-semibold text-muted-foreground">No matching tables found.</p>
              <p className="text-xs text-muted-foreground">
                Try clearing your search query or switching your filter category.
              </p>
            </div>
          )}

          {/* Touch-Friendly Table Grid */}
          {!tables.isLoading && !tables.error && filteredTables.length > 0 && (
            <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 gap-3.5 pt-2">
              {filteredTables.map((t) => {
                const draftCount = mode === "bills-only" ? 0 : getTableDraftItemsCount(restaurantId, employeeId, t.id);
                const isOccupied = t.status === "Occupied" || (t.activeOrdersCount && t.activeOrdersCount > 0);
                const isBillReady = t.status === "Bill Ready";
                const isBillReq = t.status === "Bill Requested";

                return (
                  <button
                    key={t.id}
                    type="button"
                    onClick={() => {
                      setTable(t.id);
                      setGroup(mode === "bills-only" && t.billReadyGroups?.length === 1
                        ? t.billReadyGroups[0].diningGroupId ?? ""
                        : "");
                    }}
                    className={`flex flex-col justify-between p-4 rounded-2xl border text-left transition-all hover:scale-[1.02] hover:shadow-soft active:scale-[0.99] cursor-pointer ${
                      isBillReady || isBillReq
                        ? "border-amber-400 bg-amber-500/5 hover:border-amber-500"
                        : isOccupied
                        ? "border-primary/40 bg-primary/5 hover:border-primary"
                        : "border-border bg-card hover:border-border/80"
                    }`}
                  >
                    <div>
                      <div className="flex items-center justify-between">
                        <span className="font-display text-2xl font-black text-foreground">
                          #{t.tableNumber}
                        </span>
                        <Badge
                          variant="outline"
                          className={`text-[10px] font-bold uppercase ${
                            isBillReady || isBillReq
                              ? "bg-amber-100 text-amber-800 border-amber-300"
                              : isOccupied
                              ? "bg-primary/15 text-primary border-primary/30"
                              : "bg-success/15 text-success border-success/30"
                          }`}
                        >
                          {isBillReady ? "Bill ready" : isBillReq ? "Bill requested" : isOccupied ? "Occupied" : "Available"}
                        </Badge>
                      </div>

                      <div className="mt-2 text-xs">
                        <span className="font-semibold text-foreground">Table {t.tableNumber}</span>
                        {!t.isActive && <span className="text-destructive ml-1">(Inactive)</span>}
                      </div>

                      {/* Active Order Details */}
                      <div className="mt-1 text-[11px] text-muted-foreground">
                        {isBillReady || isBillReq ? (
                          <span className="flex items-center gap-1 font-medium text-amber-700">
                            <Receipt className="h-3 w-3" />
                            {isBillReady ? "Ready to print" : "Guest requested the bill"}
                          </span>
                        ) : isOccupied ? (
                          <span className="flex items-center gap-1 font-medium text-foreground">
                            <Utensils className="h-3 w-3 text-primary" />
                            {t.activeOrdersCount ? `${t.activeOrdersCount} active order(s)` : "Open bill"}
                            {t.openBillTotal ? ` · ₹${t.openBillTotal}` : ""}
                          </span>
                        ) : (
                          <span>Ready for guests</span>
                        )}
                      </div>
                    </div>

                    {/* Unsent Draft Notification Badge */}
                    {draftCount > 0 && (
                      <div className="mt-3 pt-2 border-t border-border/50">
                        <span className="inline-flex items-center gap-1 text-[10px] font-bold text-amber-700 bg-amber-100 rounded-md px-1.5 py-0.5">
                          <ShoppingBag className="h-3 w-3" /> Draft: {draftCount} item(s)
                        </span>
                      </div>
                    )}
                  </button>
                );
              })}
            </div>
          )}
        </div>
      ) : (
        /* =========================================================================
            SECTION 2: SELECTED TABLE WORKSPACE & MULTI-DRAFT ORDER ENTRY
            ========================================================================= */
        <div className="mt-4 space-y-4">
          {/* Active Table Sticky Header Banner */}
          <div className="rounded-2xl border border-primary/30 bg-primary/5 p-4 flex flex-col md:flex-row md:items-center justify-between gap-3 shadow-xs">
            <div className="flex items-center gap-3">
              <Button
                variant="outline"
                size="sm"
                onClick={() => {
                  setTable("");
                  setGroup("");
                }}
                className="gap-1.5 text-xs font-semibold bg-background"
              >
                <ArrowLeft className="h-4 w-4" /> Switch Table
              </Button>

              <div>
                <div className="flex items-center gap-2">
                  <span className="text-lg font-black font-display text-foreground">
                    Table #{table?.tableNumber}
                  </span>
                  <Badge
                    variant="outline"
                    className={`text-[10px] font-bold uppercase ${
                      table?.status === "Bill Requested"
                        ? "bg-amber-100 text-amber-800 border-amber-300"
                        : table?.status === "Occupied"
                        ? "bg-primary/15 text-primary border-primary/30"
                        : "bg-success/15 text-success border-success/30"
                    }`}
                  >
                    {table?.status || "Active"}
                  </Badge>
                  {activeGroup && (
                    <Badge variant="outline" className="text-[10px] font-bold bg-primary/10 text-primary border-primary/20">
                      Dining Group {activeGroup.label}
                    </Badge>
                  )}
                </div>
                <p className="text-xs text-muted-foreground mt-0.5">
                  {mode === "bills-only" ? "Billing for:" : "Ordering for:"} <span className="font-semibold text-foreground">{activeGroup ? `Group ${activeGroup.label}` : groupId ? "Selected dining group" : "Whole Table (Default Bill)"}</span>
                </p>
              </div>
            </div>

            <div className="flex items-center gap-2">
              {mode !== "bills-only" && (
              <Button
                variant="outline"
                size="sm"
                onClick={() => setIsNewGroupModalOpen(true)}
                className="text-xs font-semibold gap-1.5 bg-background"
              >
                <Plus className="h-3.5 w-3.5 text-primary" /> New Dining Group
              </Button>
              )}
              <Button
                variant="outline"
                size="sm"
                onClick={() => setIsSplitBillModalOpen(true)}
                disabled={!review.data?.openBills?.length || !review.data.openBills.some((b) => b.totalAmount > 0)}
                className="text-xs font-semibold gap-1.5 bg-background"
              >
                <Split className="h-3.5 w-3.5 text-primary" /> Split Bill
              </Button>
            </div>
          </div>

          {/* Dining Groups Selector Tabs */}
          <div className="flex items-center gap-2 overflow-x-auto text-xs py-1">
            <span className="text-muted-foreground font-semibold flex items-center gap-1 mr-1 text-[11px] uppercase tracking-wider">
              <Users className="h-3.5 w-3.5" /> Group:
            </span>
            <button
              type="button"
              onClick={() => setGroup("")}
              className={`px-3 py-1.5 rounded-xl font-semibold border text-xs transition ${
                !groupId
                  ? "bg-primary text-primary-foreground border-primary shadow-xs"
                  : "bg-card text-muted-foreground hover:bg-secondary border-border"
              }`}
            >
              Whole Table
            </button>
            {groups.data?.map((g) => (
              <button
                key={g.id}
                type="button"
                onClick={() => setGroup(g.id)}
                className={`px-3 py-1.5 rounded-xl font-semibold border text-xs transition ${
                  groupId === g.id
                    ? "bg-primary text-primary-foreground border-primary shadow-xs"
                    : "bg-card text-muted-foreground hover:bg-secondary border-border"
                }`}
              >
                Group {g.label}
              </button>
            ))}
            {mode !== "bills-only" && (
            <button
              type="button"
              onClick={() => setIsNewGroupModalOpen(true)}
              className="px-2.5 py-1.5 rounded-xl border border-dashed border-primary/50 text-primary text-xs font-semibold hover:bg-primary/5 transition flex items-center gap-1"
            >
              <Plus className="h-3.5 w-3.5" /> Add
            </button>
            )}
          </div>

          {/* Layout: Order Entry Workspace (Menu + Current Cart) */}
          <div className={`mt-3 grid gap-6 ${mode === "all" ? "xl:grid-cols-2" : mode === "order-only" ? "max-w-4xl" : ""}`}>
            {mode !== "bills-only" && (
            <div>
              <div className="flex items-center justify-between">
                <h3 className="font-semibold text-sm">
                  Add Items · Table {table?.tableNumber}
                  {activeGroup ? ` (Group ${activeGroup.label})` : ""}
                </h3>
                {cart.length > 0 && (
                  <Badge variant="outline" className="text-[10px] bg-primary/10 text-primary border-primary/20">
                    Draft Autosaved ({cart.reduce((s, i) => s + i.quantity, 0)} items)
                  </Badge>
                )}
              </div>

              {groupRequired && (
                <div className="mt-2 rounded-xl bg-amber-500/10 border border-amber-500/30 p-2.5 text-xs text-amber-800">
                  This table requires a dining group. Please select an existing group above or tap <strong>Add Group</strong> to proceed.
                </div>
              )}
              {groupUnavailable && groups.data && (
                <p role="alert" className="mt-2 text-xs text-destructive">
                  This dining group is no longer available. Select an active group or switch tables.
                </p>
              )}
              {groups.isError && !groups.data && (
                <Button variant="outline" size="sm" onClick={() => groups.refetch()}>
                  Could not load dining groups. Retry
                </Button>
              )}

              <div className="relative my-3">
                <Search className="absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" />
                <Input
                  aria-label="Search menu"
                  className="pl-9 text-xs"
                  value={search}
                  placeholder="Search menu items or categories..."
                  onChange={(e) => setSearch(e.target.value)}
                />
              </div>

              {menu.isLoading && <p className="text-xs text-muted-foreground">Loading menu items…</p>}
              {menu.error && <p role="alert" className="text-xs text-destructive">{menu.error.message}</p>}

              {/* Menu Items List */}
              <div className="max-h-96 space-y-2 overflow-y-auto pr-1">
                {menu.data
                  ?.filter(
                    (i) =>
                      i.available &&
                      `${i.name} ${i.category}`.toLowerCase().includes(search.toLowerCase()),
                  )
                  .map((i) => {
                    const quantity = cart.filter((l) => l.menuItemId === i.id).reduce((s, l) => s + l.quantity, 0);
                    const hasModifiers = i.modifierGroups && i.modifierGroups.length > 0;
                    return (
                      <div
                        key={i.id}
                        className="flex items-center justify-between gap-3 rounded-xl border border-border bg-card p-3 shadow-xs hover:border-primary/40 transition"
                      >
                        <div>
                          <p className="font-semibold text-xs text-foreground">{i.name}</p>
                          <p className="text-[11px] text-muted-foreground">
                            {i.category} · {formatINR(i.price)}
                            {hasModifiers && <span className="ml-1 text-primary font-medium">(Customizable)</span>}
                          </p>
                        </div>
                        <div className="flex items-center gap-2">
                          {hasModifiers ? (
                            <>
                              {quantity > 0 && (
                                <button
                                  aria-label={`Remove ${i.name}`}
                                  className={button}
                                  disabled={busy || !!pending}
                                  onClick={() => change(i.id, -1)}
                                >
                                  −
                                </button>
                              )}
                              {quantity > 0 && <span className="text-xs font-bold text-primary">{quantity}</span>}
                              <button
                                type="button"
                                aria-label={`Customize ${i.name}`}
                                className="rounded-full border border-primary/50 bg-primary/10 px-3 py-1.5 text-xs font-semibold text-primary hover:bg-primary/20 transition"
                                disabled={!table?.isActive || orderEntryBlocked || busy || !!pending}
                                onClick={() => setCustomizingItem(i)}
                              >
                                {quantity > 0 ? "+ Add another" : "+ Customize"}
                              </button>
                            </>
                          ) : (
                            <>
                              <button
                                aria-label={`Remove ${i.name}`}
                                className={button}
                                disabled={!quantity || busy || !!pending}
                                onClick={() => change(i.id, -1)}
                              >
                                −
                              </button>
                              <span className="text-xs font-semibold min-w-4 text-center">{quantity}</span>
                              <button
                                aria-label={`Add ${i.name}`}
                                className={button}
                                disabled={!table?.isActive || orderEntryBlocked || busy || !!pending}
                                onClick={() => change(i.id, 1)}
                              >
                                +
                              </button>
                            </>
                          )}
                        </div>
                      </div>
                    );
                  })}
              </div>

              {/* Current Unsent Draft Cart */}
              {!!cart.length && (
                <div className="mt-4 rounded-2xl bg-secondary/40 border border-border p-4 space-y-2.5">
                  <div className="flex items-center justify-between border-b border-border/60 pb-2">
                    <p className="font-bold text-xs uppercase tracking-wider text-muted-foreground flex items-center gap-1.5">
                      <ShoppingBag className="h-3.5 w-3.5 text-primary" />
                      Current Draft ({cart.reduce((s, i) => s + i.quantity, 0)} items)
                    </p>
                    <button
                      type="button"
                      onClick={() => setCart([])}
                      className="text-[11px] text-destructive hover:underline"
                    >
                      Clear Draft
                    </button>
                  </div>

                  {cart.map((i) => (
                    <div key={i.lineId} className="flex flex-wrap items-center justify-between gap-2 border-b border-border/40 pb-2 text-xs">
                      <div className="min-w-0 flex-1">
                        <div className="flex items-center gap-1.5">
                          <span className="font-semibold text-foreground">{i.quantity} × {i.name}</span>
                          <span className="text-muted-foreground">({formatINR(i.price * i.quantity)})</span>
                        </div>
                        {i.selectedModifiers && i.selectedModifiers.length > 0 && (
                          <div className="mt-0.5 flex flex-wrap gap-1">
                            {i.selectedModifiers.map((m, idx) => (
                              <span key={idx} className="rounded bg-secondary/80 px-1.5 py-0.2 text-[10px] text-muted-foreground">
                                {m.optionName}{m.priceDelta > 0 ? ` (+${formatINR(m.priceDelta)})` : ""}
                              </span>
                            ))}
                          </div>
                        )}
                        {i.specialInstructions && (
                          <p className="mt-0.5 text-[10px] italic text-muted-foreground">“{i.specialInstructions}”</p>
                        )}
                      </div>
                      <div className="flex items-center gap-1.5">
                        <button
                          type="button"
                          aria-label={`Decrease ${i.name}`}
                          className="rounded border px-2 py-0.5 text-[11px] hover:bg-secondary"
                          disabled={busy || !!pending}
                          onClick={() => updateLineQty(i.lineId, -1)}
                        >
                          −
                        </button>
                        <button
                          type="button"
                          aria-label={`Increase ${i.name}`}
                          className="rounded border px-2 py-0.5 text-[11px] hover:bg-secondary"
                          disabled={busy || !!pending}
                          onClick={() => updateLineQty(i.lineId, 1)}
                        >
                          +
                        </button>
                        <select
                          aria-label={`Course for ${i.name}`}
                          className="rounded border bg-background px-2 py-1 text-[11px]"
                          value={i.courseName || ""}
                          disabled={busy || !!pending}
                          onChange={(e) => updateLineCourse(i.lineId, e.target.value)}
                        >
                          <option value="">No Course</option>
                          <option value="Starters">Starters</option>
                          <option value="Mains">Mains</option>
                          <option value="Desserts">Desserts</option>
                          <option value="Beverages">Beverages</option>
                        </select>
                        <button
                          type="button"
                          aria-label={`Toggle Hold/Fire for ${i.name}`}
                          className={`rounded-full px-2.5 py-1 text-[11px] font-bold ${
                            i.releaseState === "Held"
                              ? "bg-amber-100 text-amber-800 border border-amber-300"
                              : "bg-emerald-100 text-emerald-800 border border-emerald-300"
                          }`}
                          disabled={busy || !!pending}
                          onClick={() => toggleLineRelease(i.lineId)}
                        >
                          {i.releaseState === "Held" ? "Hold ⏸" : "Fire 🔥"}
                        </button>
                        <button
                          type="button"
                          aria-label={`Toggle Packing Intent for ${i.name}`}
                          className={`rounded-full px-2.5 py-1 text-[11px] font-bold ${
                            i.packingIntent === "Takeaway"
                              ? "bg-purple-100 text-purple-800 border border-purple-300"
                              : "bg-muted text-muted-foreground border"
                          }`}
                          disabled={busy || !!pending}
                          onClick={() => toggleLinePacking(i.lineId)}
                        >
                          {i.packingIntent === "Takeaway" ? "Takeaway 🛍️" : "Dine-In 🍽️"}
                        </button>
                      </div>
                    </div>
                  ))}

                  <div className="pt-1 flex items-center justify-between text-xs">
                    <span className="text-muted-foreground font-medium">Estimated Subtotal:</span>
                    <span className="font-bold text-foreground">
                      {formatINR(cart.reduce((sum, i) => sum + i.price * i.quantity, 0))}
                    </span>
                  </div>
                </div>
              )}

              {pending && (
                <p className="mt-3 text-xs text-amber-700 bg-amber-50 rounded-lg p-2 border border-amber-200">
                  Submission awaiting confirmation. Retry before editing.
                </p>
              )}

              <button
                className={`${button} mt-4 w-full bg-primary text-primary-foreground font-bold shadow-soft hover:bg-primary/95 transition`}
                disabled={busy || !cart.length || (!pending && (!table?.isActive || orderEntryBlocked))}
                onClick={() => void place()}
              >
                {busy ? "Sending Order…" : pending ? "Retry saved submission" : `Send Order to Kitchen (${cart.reduce((s, i) => s + i.quantity, 0)} items)`}
              </button>

              {error && (
                <p role="alert" className="mt-3 text-xs font-semibold text-destructive">
                  {error}
                </p>
              )}
            </div>

            )}
            {/* Existing Submitted Table Visits & Billing */}
            {mode !== "order-only" && (
              <FloorBills
                key={`${tableId}_${groupId}`}
                restaurantId={restaurantId}
                employeeId={employeeId}
                tableId={tableId}
                groupId={groupId}
                review={review.data}
                tables={tables.data || []}
                onTransferred={setTable}
                billingOnly={mode === "bills-only"}
              />
            )}
          </div>
        </div>
      )}

      {/* Staff Customization Modal */}
      {customizingItem && (
        <StaffCustomizationModal
          item={customizingItem}
          onClose={() => setCustomizingItem(null)}
          onAdd={addCustomLine}
        />
      )}

      {/* New Dining Group Modal */}
      <NewDiningGroupModal
        open={isNewGroupModalOpen}
        onOpenChange={setIsNewGroupModalOpen}
        tableNumber={table?.tableNumber || 1}
        existingLabels={(groups.data || []).map((g) => g.label)}
        onSubmit={async (label, operationId) => {
          try {
            const created = await createDiningGroup(restaurantId, tableId, label, operationId);
            toast.success(`Dining Group ${created.label} created!`);
            await cache.invalidateQueries({ queryKey: ["diningGroups", restaurantId, tableId] });
            setGroup(created.id);
            setIsNewGroupModalOpen(false);
          } catch (err: any) {
            toast.error(err.message || "Failed to create dining group");
          }
        }}
      />

      {/* Split Bill Modal */}
      <SplitBillModal
        open={isSplitBillModalOpen}
        onOpenChange={setIsSplitBillModalOpen}
        tableNumber={table?.tableNumber || 1}
        bill={review.data?.openBills?.[0] || review.data?.openBill || null}
        onSplitComplete={async () => {
          setIsSplitBillModalOpen(false);
          await Promise.all([
            cache.invalidateQueries({ queryKey: ["tableBill", restaurantId] }),
            cache.invalidateQueries({ queryKey: ["serviceTables", restaurantId] }),
          ]);
        }}
        restaurantId={restaurantId}
      />
    </section>
  );
}

/* ==========================================================================
   MODAL 1: NEW DINING GROUP MODAL
   ========================================================================== */
function NewDiningGroupModal({
  open,
  onOpenChange,
  tableNumber,
  existingLabels,
  onSubmit,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  tableNumber: number;
  existingLabels: string[];
  onSubmit: (label: string, operationId: string) => Promise<void>;
}) {
  const nextDefault = useMemo(() => {
    for (let i = 0; i < 26; i++) {
      const letter = String.fromCharCode(65 + i);
      if (!existingLabels.includes(letter)) return letter;
    }
    return "A";
  }, [existingLabels]);

  const [label, setLabel] = useState(nextDefault);
  const [busy, setBusy] = useState(false);
  const pendingCreation = useRef<{ label: string; operationId: string } | null>(null);
  const wasOpen = useRef(false);

  useEffect(() => {
    if (open && !wasOpen.current) {
      setLabel(nextDefault);
      pendingCreation.current = null;
    }
    wasOpen.current = open;
  }, [nextDefault, open]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const normalizedLabel = label.trim().toUpperCase();
    if (busy || !/^[A-Z][A-Z0-9]{0,7}$/.test(normalizedLabel)) return;
    if (pendingCreation.current?.label !== normalizedLabel) {
      pendingCreation.current = { label: normalizedLabel, operationId: crypto.randomUUID() };
    }
    setBusy(true);
    try {
      await onSubmit(normalizedLabel, pendingCreation.current.operationId);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader className="text-left">
          <DialogTitle className="text-base font-bold">
            Start New Dining Group · Table {tableNumber}
          </DialogTitle>
          <DialogDescription className="text-xs text-muted-foreground">
            Creates an independent dining session and separate bill for guests sharing Table {tableNumber}.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit} className="space-y-4 py-2 text-xs">
          <div>
            <label className="font-semibold text-foreground">Group Identifier / Label</label>
            <Input
              value={label}
              onChange={(e) => setLabel(e.target.value)}
              placeholder="e.g. A, B, FAMILY1"
              maxLength={8}
              pattern="[A-Za-z][A-Za-z0-9]{0,7}"
              disabled={busy}
              required
              className="mt-1 text-xs"
            />
            <p className="mt-1 text-[11px] text-muted-foreground">
              Use 1–8 letters or digits, starting with a letter.
            </p>
            <p className="mt-1 text-[11px] text-muted-foreground">
              Existing groups on this table: {existingLabels.length > 0 ? existingLabels.join(", ") : "None"}
            </p>
          </div>

          <DialogFooter className="pt-2">
            <Button variant="outline" type="button" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button disabled={busy || !label.trim()} type="submit">
              {busy ? "Creating…" : "Create Group"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

/* ==========================================================================
   MODAL 2: SPLIT BILL MODAL (Item Split & Amount Split with Exact Paise Balance)
   ========================================================================== */
function SplitBillModal({
  open,
  onOpenChange,
  tableNumber,
  bill,
  onSplitComplete,
  restaurantId,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  tableNumber: number;
  bill: TableBill | null;
  onSplitComplete: () => Promise<void>;
  restaurantId: string;
}) {
  const [splitTab, setSplitTab] = useState<"item" | "amount">("item");
  const [itemAllocations, setItemAllocations] = useState<Record<string, number>>({});
  const [amountWays, setAmountWays] = useState<number>(2);
  const [customAmounts, setCustomAmounts] = useState<number[]>([]);
  const [busy, setBusy] = useState(false);

  // Extract all distinct line items from bill's orders
  const allItems = useMemo(() => {
    if (!bill) return [];
    const list: { id: string; name: string; quantity: number; unitPrice: number }[] = [];
    for (const ord of bill.orders || []) {
      for (const it of ord.items || []) {
        list.push({
          id: it.id,
          name: it.name,
          quantity: it.quantity,
          unitPrice: it.unitPrice,
        });
      }
    }
    return list;
  }, [bill]);

  const totalBillAmount = bill?.totalAmount || 0;

  // Initialize allocations and equal amount shares
  useEffect(() => {
    if (!bill) return;
    setItemAllocations({});
    // Default 2-way split
    const share1 = Math.round((totalBillAmount / 2) * 100) / 100;
    const share2 = Math.round((totalBillAmount - share1) * 100) / 100;
    setCustomAmounts([share1, share2]);
  }, [bill, totalBillAmount]);

  const handleWaysChange = (ways: number) => {
    setAmountWays(ways);
    const baseShare = Math.floor((totalBillAmount / ways) * 100) / 100;
    const amounts: number[] = Array(ways).fill(baseShare);
    const sum = amounts.reduce((a, b) => a + b, 0);
    const remainder = Math.round((totalBillAmount - sum) * 100) / 100;
    // Allocate paise remainder to first share
    amounts[0] = Math.round((amounts[0] + remainder) * 100) / 100;
    setCustomAmounts(amounts);
  };

  // Calculations for Item Split
  const bill2Subtotal = useMemo(() => {
    return allItems.reduce((acc, it) => {
      const movedQty = itemAllocations[it.id] || 0;
      return acc + movedQty * it.unitPrice;
    }, 0);
  }, [allItems, itemAllocations]);

  const bill2Tax = Math.round(bill2Subtotal * 0.05);
  const bill2Total = bill2Subtotal + bill2Tax;
  const bill1Total = Math.max(0, totalBillAmount - bill2Total);

  // Calculations for Amount Split
  const totalAllocatedAmount = customAmounts.reduce((a, b) => a + (Number(b) || 0), 0);
  const unallocatedAmount = Math.round((totalBillAmount - totalAllocatedAmount) * 100) / 100;
  const isAmountBalanced = Math.abs(unallocatedAmount) < 0.01;

  const handleExecuteSplit = async () => {
    if (!bill) return;
    setBusy(true);
    try {
      if (splitTab === "item") {
        const itemsToMove = Object.entries(itemAllocations)
          .filter(([, qty]) => qty > 0)
          .map(([billItemId, quantity]) => ({ billItemId, quantity }));

        if (itemsToMove.length === 0) {
          toast.error("Please allocate at least one item to the new split bill.");
          return;
        }

        await splitTableBill(restaurantId, bill.id, {
          splitMode: "Item",
          items: itemsToMove,
        });
      } else {
        if (!isAmountBalanced) {
          toast.error(`Please balance all shares. Unallocated: ₹${unallocatedAmount.toFixed(2)}`);
          return;
        }

        await splitTableBill(restaurantId, bill.id, {
          splitMode: "Amount",
          splitAmounts: customAmounts,
        });
      }
      toast.success("Bill split executed successfully!");
      await onSplitComplete();
    } catch (err: any) {
      toast.error(err.message || "Failed to split bill");
    } finally {
      setBusy(false);
    }
  };

  if (!bill) return null;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-xl max-h-[90vh] overflow-hidden flex flex-col">
        <DialogHeader className="text-left">
          <DialogTitle className="text-base font-bold flex items-center justify-between">
            <span>Split Bill · Table #{tableNumber}</span>
            <Badge variant="outline" className="text-xs font-mono font-bold text-primary">
              Total: {formatINR(totalBillAmount)}
            </Badge>
          </DialogTitle>
          <DialogDescription className="text-xs text-muted-foreground">
            Divide charges accurately down to the paise by moving items or dividing the total amount.
          </DialogDescription>
        </DialogHeader>

        {/* Tab Buttons */}
        <div className="flex border-b border-border text-xs font-semibold">
          <button
            type="button"
            onClick={() => setSplitTab("item")}
            className={`py-2 px-4 border-b-2 transition ${
              splitTab === "item"
                ? "border-primary text-primary font-bold"
                : "border-transparent text-muted-foreground hover:text-foreground"
            }`}
          >
            Item / Quantity Split
          </button>
          <button
            type="button"
            onClick={() => setSplitTab("amount")}
            className={`py-2 px-4 border-b-2 transition ${
              splitTab === "amount"
                ? "border-primary text-primary font-bold"
                : "border-transparent text-muted-foreground hover:text-foreground"
            }`}
          >
            Equal / Amount Split
          </button>
        </div>

        <div className="flex-1 overflow-y-auto py-3 space-y-4 text-xs">
          {splitTab === "item" ? (
            <div className="space-y-3">
              <p className="text-[11px] text-muted-foreground">
                Move items to <strong>Bill 2 (New Split)</strong>. Any unallocated items stay on <strong>Bill 1 (Original)</strong>.
              </p>

              <div className="rounded-xl border border-border divide-y divide-border overflow-hidden">
                {allItems.map((it) => {
                  const moved = itemAllocations[it.id] || 0;
                  return (
                    <div key={it.id} className="p-2.5 flex items-center justify-between gap-3 bg-card">
                      <div>
                        <p className="font-semibold">{it.name}</p>
                        <p className="text-[10px] text-muted-foreground">
                          {formatINR(it.unitPrice)} each · Total ordered: {it.quantity}
                        </p>
                      </div>

                      <div className="flex items-center gap-2">
                        <span className="text-[11px] text-muted-foreground">Move to Bill 2:</span>
                        <div className="flex items-center border rounded-lg px-2 py-0.5 bg-background">
                          <button
                            type="button"
                            disabled={moved <= 0}
                            onClick={() =>
                              setItemAllocations((prev) => ({
                                ...prev,
                                [it.id]: Math.max(0, moved - 1),
                              }))
                            }
                            className="px-1 font-bold disabled:opacity-40"
                          >
                            −
                          </button>
                          <span className="font-bold px-2">{moved}</span>
                          <button
                            type="button"
                            disabled={moved >= it.quantity}
                            onClick={() =>
                              setItemAllocations((prev) => ({
                                ...prev,
                                [it.id]: Math.min(it.quantity, moved + 1),
                              }))
                            }
                            className="px-1 font-bold disabled:opacity-40"
                          >
                            +
                          </button>
                        </div>
                      </div>
                    </div>
                  );
                })}
              </div>

              {/* Live Preview Comparison Cards */}
              <div className="grid grid-cols-2 gap-3 pt-2">
                <div className="rounded-xl border border-border bg-secondary/30 p-3 space-y-1">
                  <span className="font-bold text-[11px] text-foreground">Bill 1 (Original Party)</span>
                  <div className="text-xs space-y-0.5 text-muted-foreground">
                    <p>Remaining: <strong className="text-foreground">{formatINR(bill1Total)}</strong></p>
                  </div>
                </div>

                <div className="rounded-xl border border-primary/30 bg-primary/5 p-3 space-y-1">
                  <span className="font-bold text-[11px] text-primary">Bill 2 (New Split Party)</span>
                  <div className="text-xs space-y-0.5 text-muted-foreground">
                    <p>Subtotal: {formatINR(bill2Subtotal)}</p>
                    <p>GST (5%): {formatINR(bill2Tax)}</p>
                    <p className="font-bold text-foreground">Total: {formatINR(bill2Total)}</p>
                  </div>
                </div>
              </div>
            </div>
          ) : (
            <div className="space-y-4">
              <div className="flex items-center justify-between">
                <span className="font-semibold text-xs">Equal Division Presets:</span>
                <div className="flex items-center gap-1.5">
                  {[2, 3, 4, 5].map((w) => (
                    <button
                      key={w}
                      type="button"
                      onClick={() => handleWaysChange(w)}
                      className={`px-2.5 py-1 rounded-lg border text-xs font-semibold ${
                        amountWays === w
                          ? "bg-primary text-primary-foreground border-primary"
                          : "bg-secondary/50 border-border text-foreground"
                      }`}
                    >
                      {w} Ways
                    </button>
                  ))}
                </div>
              </div>

              <div className="space-y-2">
                {customAmounts.map((amt, idx) => (
                  <div key={idx} className="flex items-center justify-between p-2.5 rounded-xl border bg-card gap-3">
                    <span className="font-semibold text-xs">Bill Share #{idx + 1}</span>
                    <div className="flex items-center gap-1">
                      <span className="text-muted-foreground text-xs font-semibold">₹</span>
                      <Input
                        type="number"
                        step="0.01"
                        value={amt}
                        onChange={(e) => {
                          const val = parseFloat(e.target.value) || 0;
                          setCustomAmounts((prev) => prev.map((v, i) => (i === idx ? val : v)));
                        }}
                        className="w-28 text-xs font-mono font-bold"
                      />
                    </div>
                  </div>
                ))}
              </div>

              {/* Exact Paise Unallocated Balance Tracker */}
              <div
                className={`p-3 rounded-xl border flex items-center justify-between text-xs font-semibold ${
                  isAmountBalanced
                    ? "bg-success/10 border-success/30 text-success"
                    : "bg-amber-500/10 border-amber-500/30 text-amber-800"
                }`}
              >
                <span className="flex items-center gap-1.5">
                  {isAmountBalanced ? (
                    <CheckCircle2 className="h-4 w-4 text-success" />
                  ) : (
                    <AlertCircle className="h-4 w-4 text-amber-600" />
                  )}
                  {isAmountBalanced ? "All amounts fully balanced to the paise" : "Unallocated Balance (Must equal ₹0.00)"}
                </span>
                <span className="font-mono font-bold text-sm">
                  {isAmountBalanced ? "₹0.00" : `₹${unallocatedAmount.toFixed(2)}`}
                </span>
              </div>
            </div>
          )}
        </div>

        <DialogFooter className="pt-2 border-t">
          <Button variant="outline" type="button" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button
            disabled={busy || (splitTab === "item" ? bill2Total <= 0 : !isAmountBalanced)}
            onClick={handleExecuteSplit}
          >
            {busy ? "Splitting…" : "Confirm Bill Split"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/* ==========================================================================
   MODAL 3: STAFF CUSTOMIZATION MODAL (Hold/Fire, Course, Modifiers)
   ========================================================================== */
function StaffCustomizationModal({
  item,
  onClose,
  onAdd,
}: {
  item: any;
  onClose: () => void;
  onAdd: (line: {
    menuItemId: string;
    name: string;
    quantity: number;
    price: number;
    releaseState: "Fired" | "Held";
    courseName?: string;
    specialInstructions?: string;
    selectedModifiers?: { modifierGroupId: string; modifierOptionId: string; optionName: string; priceDelta: number }[];
  }) => void;
}) {
  const [qty, setQty] = useState(1);
  const [course, setCourse] = useState("");
  const [releaseState, setReleaseState] = useState<"Fired" | "Held">("Fired");
  const [instructions, setInstructions] = useState("");
  const [selectedOptionIds, setSelectedOptionIds] = useState<Record<string, string[]>>({});

  useEffect(() => {
    const initial: Record<string, string[]> = {};
    for (const group of item.modifierGroups || []) {
      if (group.minSelections > 0 && group.options?.length > 0 && group.maxSelections === 1) {
        initial[group.id] = [group.options[0].id];
      }
    }
    setSelectedOptionIds(initial);
  }, [item]);

  const toggleOption = (groupId: string, optionId: string, maxSelections: number) => {
    setSelectedOptionIds((prev) => {
      const current = prev[groupId] || [];
      if (maxSelections === 1) {
        return { ...prev, [groupId]: [optionId] };
      }
      if (current.includes(optionId)) {
        return { ...prev, [groupId]: current.filter((id) => id !== optionId) };
      }
      if (current.length < maxSelections) {
        return { ...prev, [groupId]: [...current, optionId] };
      }
      return prev;
    });
  };

  const isValid = useMemo(() => {
    for (const group of item.modifierGroups || []) {
      const count = (selectedOptionIds[group.id] || []).length;
      if (count < (group.minSelections || 0)) return false;
      if (count > (group.maxSelections || 1)) return false;
    }
    return true;
  }, [item, selectedOptionIds]);

  const selectedModifiersList = useMemo(() => {
    const list: { modifierGroupId: string; modifierOptionId: string; optionName: string; priceDelta: number }[] = [];
    for (const group of item.modifierGroups || []) {
      const chosenIds = selectedOptionIds[group.id] || [];
      for (const optId of chosenIds) {
        const opt = group.options?.find((o: any) => o.id === optId);
        if (opt) {
          list.push({
            modifierGroupId: group.id,
            modifierOptionId: opt.id,
            optionName: opt.name,
            priceDelta: Number(opt.priceDelta || 0),
          });
        }
      }
    }
    return list;
  }, [item, selectedOptionIds]);

  const modDelta = selectedModifiersList.reduce((s, m) => s + m.priceDelta, 0);
  const unitPrice = item.price + modDelta;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm">
      <div className="relative w-full max-w-lg max-h-[90vh] overflow-hidden rounded-2xl bg-card border p-5 shadow-2xl flex flex-col">
        <div className="flex items-center justify-between pb-3 border-b">
          <div>
            <h3 className="font-bold text-base">{item.name}</h3>
            <p className="text-xs text-muted-foreground">{formatINR(item.price)} base · {item.category}</p>
          </div>
          <button onClick={() => onClose()} className="rounded-full p-1 hover:bg-secondary">
            ✕
          </button>
        </div>

        <div className="flex-1 overflow-y-auto py-3 space-y-4 text-xs">
          {item.modifierGroups?.map((group: any) => {
            const isSingle = group.maxSelections === 1;
            const chosen = selectedOptionIds[group.id] || [];
            const isMet = chosen.length >= group.minSelections;

            return (
              <div key={group.id} className="rounded-xl border p-3 bg-secondary/20">
                <div className="flex items-center justify-between mb-2">
                  <span className="font-semibold">{group.name}</span>
                  <span className={`text-[10px] px-2 py-0.5 rounded-full ${isMet ? "bg-emerald-100 text-emerald-800" : "bg-amber-100 text-amber-800 font-bold"}`}>
                    {group.minSelections > 0 ? (isMet ? "Satisfied" : `Required (${group.minSelections})`) : "Optional"}
                  </span>
                </div>
                <div className="space-y-1.5 mt-2">
                  {group.options?.map((opt: any) => {
                    const isSelected = chosen.includes(opt.id);
                    const delta = Number(opt.priceDelta || 0);

                    return (
                      <label
                        key={opt.id}
                        onClick={() => toggleOption(group.id, opt.id, group.maxSelections)}
                        className={`flex items-center justify-between p-2 rounded-lg border cursor-pointer ${
                          isSelected ? "border-primary bg-primary-soft/50 font-medium" : "border-border bg-card"
                        }`}
                      >
                        <div className="flex items-center gap-2">
                          <input type={isSingle ? "radio" : "checkbox"} checked={isSelected} onChange={() => {}} />
                          <span>{opt.name}</span>
                        </div>
                        <span className="text-muted-foreground">
                          {delta > 0 ? `+${formatINR(delta)}` : delta < 0 ? `-${formatINR(Math.abs(delta))}` : "Free"}
                        </span>
                      </label>
                    );
                  })}
                </div>
              </div>
            );
          })}

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-[11px] font-semibold mb-1">Course</label>
              <select className="w-full rounded border bg-background p-1.5 text-xs" value={course} onChange={(e) => setCourse(e.target.value)}>
                <option value="">No Course</option>
                <option value="Starters">Starters</option>
                <option value="Mains">Mains</option>
                <option value="Desserts">Desserts</option>
                <option value="Beverages">Beverages</option>
              </select>
            </div>
            <div>
              <label className="block text-[11px] font-semibold mb-1">Release Timing</label>
              <select className="w-full rounded border bg-background p-1.5 text-xs" value={releaseState} onChange={(e) => setReleaseState(e.target.value as any)}>
                <option value="Fired">Fire Now 🔥</option>
                <option value="Held">Hold ⏸</option>
              </select>
            </div>
          </div>

          <div>
            <label className="block text-[11px] font-semibold mb-1">Special Cooking Instructions</label>
            <input
              type="text"
              placeholder="e.g. Extra spicy, no onions"
              className="w-full rounded border bg-background p-1.5 text-xs"
              value={instructions}
              onChange={(e) => setInstructions(e.target.value)}
            />
          </div>
        </div>

        <div className="pt-3 border-t flex items-center justify-between gap-3">
          <div className="flex items-center gap-2 border rounded-full px-2 py-0.5">
            <button onClick={() => setQty(Math.max(1, qty - 1))} disabled={qty <= 1} className="px-1 font-bold disabled:opacity-40">−</button>
            <span className="font-bold">{qty}</span>
            <button onClick={() => setQty(qty + 1)} className="px-1 font-bold">+</button>
          </div>
          <button
            disabled={!isValid}
            onClick={() => {
              onAdd({
                menuItemId: item.id,
                name: item.name,
                quantity: qty,
                price: unitPrice,
                releaseState,
                courseName: course || undefined,
                specialInstructions: instructions.trim() || undefined,
                selectedModifiers: selectedModifiersList.length > 0 ? selectedModifiersList : undefined,
              });
              onClose();
            }}
            className="flex-1 rounded-full bg-primary px-4 py-2 text-xs font-semibold text-primary-foreground disabled:opacity-50"
          >
            Add to Order · {formatINR(unitPrice * qty)}
          </button>
        </div>
      </div>
    </div>
  );
}
