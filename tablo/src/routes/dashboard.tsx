import { createFileRoute, Link } from "@tanstack/react-router";
import { useMemo, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { Bell, Check, ChefHat, CreditCard, Filter, Flame, Loader2, LogIn, LogOut, PackageCheck, Printer, Search, ShieldAlert, ShoppingCart, Timer, UtensilsCrossed } from "lucide-react";
import { toast } from "sonner";
import { SiteHeader } from "@/components/site-header";
import { AppLayout } from "@/components/app-layout";
import { ServiceDesk } from "@/components/service-desk";
import { KitchenPrintingSettingsModal } from "@/components/kitchen-printing-settings";
import { useAuth } from "@/hooks/use-auth";
import { formatINR, relativeTime } from "@/lib/mock-data";
import { Order, OrderStatus } from "@/types";
import { useOrders, useUpdateOrderStatus } from "@/hooks/use-orders";
import { useActiveServiceRequests, useManageServiceRequest } from "@/hooks/use-service-requests";
import { fireOrderItems, createRefill, cancelOrderItem } from "@/services/api";

export const Route = createFileRoute("/dashboard")({
  validateSearch: (search: Record<string, unknown>) => ({
    tab: typeof search.tab === "string" ? search.tab : undefined,
    table: typeof search.table === "string" ? search.table : undefined,
    group: typeof search.group === "string" ? search.group : undefined,
  }),
  head: () => ({
    meta: [
      { title: "Floor, Orders & Kitchen — Tablo" },
      { name: "description", content: "Floor management, staff order taking, and live kitchen display." },
    ],
  }),
  component: DashboardPage,
});

const flow: OrderStatus[] = ["placed", "accepted", "preparing", "ready", "served"];
const labels: Record<OrderStatus, string> = {
  placed: "New", accepted: "Accepted", preparing: "Preparing", ready: "Ready", served: "Served", cancelled: "Cancelled", rejected: "Rejected",
};
const tone: Record<OrderStatus, string> = {
  placed: "bg-warning/15 text-warning",
  accepted: "bg-primary-soft text-primary",
  preparing: "bg-primary-soft text-primary",
  ready: "bg-success/15 text-success",
  served: "bg-secondary text-muted-foreground",
  cancelled: "bg-destructive/10 text-destructive",
  rejected: "bg-destructive/10 text-destructive",
};

function DashboardPage() {
  const { user, loading: authLoading, isStaff, roles, signOut } = useAuth();

  if (authLoading) {
    return (
      <div className="grid min-h-screen place-items-center bg-secondary/40">
        <Loader2 className="h-6 w-6 animate-spin text-primary" />
      </div>
    );
  }

  if (!user) return <SignInGate />;
  if (!isStaff) return <NoAccessGate roles={roles} onSignOut={signOut} />;

  return <DashboardInner />;
}

function SignInGate() {
  return (
    <div className="min-h-screen bg-secondary/40">
      <SiteHeader />
      <div className="mx-auto max-w-md px-4 py-16 text-center">
        <div className="rounded-3xl border border-border bg-card p-10 shadow-card">
          <span className="mx-auto grid h-12 w-12 place-items-center rounded-2xl bg-primary-soft text-primary">
            <LogIn className="h-5 w-5" />
          </span>
          <h1 className="mt-4 font-display text-2xl font-bold">Staff sign in required</h1>
          <p className="mt-2 text-sm text-muted-foreground">
            The kitchen dashboard is private. Sign in with your staff account to continue.
          </p>
          <Link to="/auth" className="mt-6 inline-flex items-center justify-center gap-2 rounded-full bg-gradient-primary px-5 py-3 text-sm font-semibold text-primary-foreground shadow-glow">
            Go to sign in
          </Link>
        </div>
      </div>
    </div>
  );
}

function NoAccessGate({ roles, onSignOut }: { roles: string[]; onSignOut: () => void }) {
  return (
    <div className="min-h-screen bg-secondary/40">
      <SiteHeader />
      <div className="mx-auto max-w-md px-4 py-16 text-center">
        <div className="rounded-3xl border border-border bg-card p-10 shadow-card">
          <span className="mx-auto grid h-12 w-12 place-items-center rounded-2xl bg-warning/15 text-warning">
            <ShieldAlert className="h-5 w-5" />
          </span>
          <h1 className="mt-4 font-display text-2xl font-bold">No staff access yet</h1>
          <p className="mt-2 text-sm text-muted-foreground">
            Your account is signed in as <span className="font-semibold">{roles.join(", ") || "customer"}</span>. Ask a restaurant owner to grant you the <span className="font-semibold">staff</span> or <span className="font-semibold">owner</span> role.
          </p>
          <button onClick={onSignOut} className="mt-6 inline-flex items-center gap-2 rounded-full border border-border px-4 py-2 text-sm font-semibold">
            <LogOut className="h-4 w-4" /> Sign out
          </button>
        </div>
      </div>
    </div>
  );
}

function DashboardInner() {
  const { signOut, user } = useAuth();
  const search = Route.useSearch();
  const currentTab = search.tab === "order" ? "order" : search.tab === "kitchen" ? "kitchen" : search.tab === "billing" ? "billing" : "floor";
  const searchTable = search.table;
  const searchGroup = search.group;
  const [showServiceDesk, setShowServiceDesk] = useState(false);
  const canManageBills = user?.role === "Admin" || user?.role === "Waiter" || user?.role === "Owner";

  const { data: orders = [], isLoading: loadingOrders } = useOrders(user?.restaurantId || null);
  const { mutateAsync: updateStatus } = useUpdateOrderStatus();
  const queryClient = useQueryClient();

  const { data: serviceRequests = [] } = useActiveServiceRequests(user?.restaurantId || null);
  const { mutate: manageServiceReq } = useManageServiceRequest();

  const [filter, setFilter] = useState<OrderStatus | "all">("all");
  const [tableQ, setTableQ] = useState("");
  const [processingId, setProcessingId] = useState<string | null>(null);
  const [showPrintingSettings, setShowPrintingSettings] = useState(false);

  // Modal State for Exception
  const [exceptionModal, setExceptionModal] = useState<{ orderId: string, action: "rejected" | "cancelled" } | null>(null);
  const [exceptionReason, setExceptionReason] = useState("");

  // Modal State for Manager Item Cancellation
  const [cancelItemModal, setCancelItemModal] = useState<{
    orderId: string;
    orderItemId: string;
    itemName: string;
    maxQty: number;
    wasReleased: boolean;
  } | null>(null);
  const [cancelQty, setCancelQty] = useState(1);
  const [cancelOutcome, setCancelOutcome] = useState<"NotCooked" | "KitchenWaste">("NotCooked");
  const [cancelReason, setCancelReason] = useState("");
  const [managerPin, setManagerPin] = useState("");

  const handleFireItems = async (orderId: string, courseName?: string) => {
    try {
      setProcessingId(orderId);
      const res = await fireOrderItems(orderId, {
        operationId: crypto.randomUUID(),
        courseName,
      }, user?.restaurantId);
      toast.success(res.message);
      queryClient.invalidateQueries({ queryKey: ["orders"] });
    } catch (err: any) {
      toast.error(err.message || "Failed to fire items");
    } finally {
      setProcessingId(null);
    }
  };

  const handleRefillItem = async (orderId: string, orderItemId: string) => {
    try {
      setProcessingId(orderId);
      const res = await createRefill(orderId, orderItemId, {
        operationId: crypto.randomUUID(),
        quantity: 1,
      }, user?.restaurantId);
      toast.success(res.message);
      queryClient.invalidateQueries({ queryKey: ["orders"] });
    } catch (err: any) {
      toast.error(err.message || "Failed to create refill");
    } finally {
      setProcessingId(null);
    }
  };

  const handleItemCancelSubmit = async () => {
    if (!cancelItemModal || !cancelReason.trim()) return;
    try {
      setProcessingId(cancelItemModal.orderId);
      const res = await cancelOrderItem(cancelItemModal.orderId, cancelItemModal.orderItemId, {
        operationId: crypto.randomUUID(),
        quantity: cancelQty,
        outcome: cancelOutcome,
        reason: cancelReason,
        managerPin: cancelItemModal.wasReleased ? managerPin : undefined,
      }, user?.restaurantId);
      toast.success(res.message);
      queryClient.invalidateQueries({ queryKey: ["orders"] });
      setCancelItemModal(null);
      setCancelReason("");
      setManagerPin("");
    } catch (err: any) {
      toast.error(err.message || "Failed to cancel item");
    } finally {
      setProcessingId(null);
    }
  };

  const counts = useMemo(() => ({
    active: orders.filter((o) => o.status !== "served" && o.status !== "cancelled" && o.status !== "rejected").length,
    preparing: orders.filter((o) => o.status === "preparing").length,
    ready: orders.filter((o) => o.status === "ready").length,
    served: orders.filter((o) => o.status === "served").length,
  }), [orders]);

  const visible = useMemo(() => orders.filter((o) => {
    if (filter !== "all" && o.status !== filter) return false;
    if (tableQ && !String(o.table_number).includes(tableQ)) return false;
    return true;
  }), [orders, filter, tableQ]);

  const advance = async (id: string, next: OrderStatus) => {
    if (processingId) return; // Prevent duplicate clicks
    try {
      setProcessingId(id);
      await updateStatus({ orderId: id, status: next, restaurantId: user?.restaurantId || "" });
    } catch (err: any) {
      toast.error(err.message || "Failed to update status");
    } finally {
      setProcessingId(null);
    }
  };

  const handleExceptionSubmit = async () => {
    if (!exceptionModal || !exceptionReason.trim()) return;
    try {
      setProcessingId(exceptionModal.orderId);
      await updateStatus({ orderId: exceptionModal.orderId, status: exceptionModal.action, restaurantId: user?.restaurantId || "", exceptionReason });
      setExceptionModal(null);
      setExceptionReason("");
    } catch (err: any) {
      toast.error(err.message || "Failed to update status");
    } finally {
      setProcessingId(null);
    }
  };

  return (
    <AppLayout>
      <div className="min-h-screen bg-secondary/40">
        <main className="mx-auto max-w-7xl px-4 py-8 sm:px-6">
          <div className="flex flex-wrap items-end justify-between gap-3">
            <div>
              <p className="text-xs font-semibold uppercase tracking-wider text-primary">
                {currentTab === "order"
                  ? "Direct table ordering"
                  : currentTab === "kitchen"
                  ? "Live kitchen display"
                  : currentTab === "billing"
                  ? "Table billing & payment settlements"
                  : "Restaurant floor & billing"}
              </p>
              <h1 className="mt-1 font-display text-3xl font-bold">
                {currentTab === "order"
                  ? "Staff Order Taking"
                  : currentTab === "kitchen"
                  ? "Kitchen Production Queue"
                  : currentTab === "billing"
                  ? "Table Billing & Settlements"
                  : "Floor & Table Orders"}
              </h1>
              <p className="mt-1 text-xs text-muted-foreground">Signed in as {user?.email}</p>
            </div>
            <div className="flex items-center gap-2">
              <span className="inline-flex items-center gap-2 rounded-full bg-card px-3 py-1.5 text-xs font-medium shadow-soft">
                <span className="h-1.5 w-1.5 animate-pulse-dot rounded-full bg-primary" /> Real-time updates
              </span>
              <button onClick={() => void signOut()} className="inline-flex items-center gap-1.5 rounded-full border border-border bg-card px-3 py-1.5 text-xs font-semibold">
                <LogOut className="h-3.5 w-3.5" /> Sign out
              </button>
            </div>
          </div>

          {/* Operational View Tabs */}
          <div className="mt-6 flex flex-wrap gap-2 border-b border-border/60 pb-3">
            <Link
              to="/dashboard"
              search={{ tab: "floor" }}
              className={`inline-flex items-center gap-2 rounded-full px-4 py-2 text-sm font-semibold transition ${
                currentTab === "floor"
                  ? "bg-primary text-primary-foreground shadow-sm"
                  : "bg-card text-muted-foreground hover:bg-secondary hover:text-foreground"
              }`}
            >
              <UtensilsCrossed className="h-4 w-4" /> Floor & Table Orders
            </Link>
            {canManageBills && (
              <Link
                to="/dashboard"
                search={{ tab: "order" }}
                className={`inline-flex items-center gap-2 rounded-full px-4 py-2 text-sm font-semibold transition ${
                  currentTab === "order"
                    ? "bg-primary text-primary-foreground shadow-sm"
                    : "bg-card text-muted-foreground hover:bg-secondary hover:text-foreground"
                }`}
              >
                <ShoppingCart className="h-4 w-4" /> Staff Order Taking
              </Link>
            )}
            <Link
              to="/dashboard"
              search={{ tab: "kitchen" }}
              className={`inline-flex items-center gap-2 rounded-full px-4 py-2 text-sm font-semibold transition ${
                currentTab === "kitchen"
                  ? "bg-primary text-primary-foreground shadow-sm"
                  : "bg-card text-muted-foreground hover:bg-secondary hover:text-foreground"
              }`}
            >
              <ChefHat className="h-4 w-4" /> Kitchen Queue
            </Link>
            {canManageBills && (
              <Link
                to="/dashboard"
                search={{ tab: "billing" }}
                className={`inline-flex items-center gap-2 rounded-full px-4 py-2 text-sm font-semibold transition ${
                  currentTab === "billing"
                    ? "bg-primary text-primary-foreground shadow-sm"
                    : "bg-card text-muted-foreground hover:bg-secondary hover:text-foreground"
                }`}
              >
                <CreditCard className="h-4 w-4" /> Billing & Settlements
              </Link>
            )}
          </div>

          {/* 1. DEDICATED STAFF ORDER TAKING WORKSPACE */}
          {currentTab === "order" && canManageBills && user?.restaurantId && (
            <div className="mt-6">
              <ServiceDesk
                restaurantId={user.restaurantId}
                employeeId={user.id}
                initialTableId={searchTable}
                initialGroupId={searchGroup}
                mode="order-only"
              />
            </div>
          )}

          {/* DEDICATED TABLE BILLING & SETTLEMENTS */}
          {currentTab === "billing" && canManageBills && user?.restaurantId && (
            <div className="mt-6">
              <ServiceDesk
                restaurantId={user.restaurantId}
                employeeId={user.id}
                initialTableId={searchTable}
                initialGroupId={searchGroup}
                mode="bills-only"
              />
            </div>
          )}

              {/* 2. FLOOR & ORDERS WORKSPACE */}
              {currentTab === "floor" && (
                <>
                  <div className="mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
                    <Stat title="Active orders" value={counts.active} icon={Bell} accent />
                    <Stat title="Preparing" value={counts.preparing} icon={Flame} />
                    <Stat title="Ready" value={counts.ready} icon={PackageCheck} />
                    <Stat title="Served today" value={counts.served} icon={Check} />
                  </div>

                  {canManageBills && user?.restaurantId && (
                    <div className="mt-6">
                      <ServiceDesk
                        restaurantId={user.restaurantId}
                        employeeId={user.id}
                        initialTableId={searchTable}
                        initialGroupId={searchGroup}
                        mode="bills-only"
                      />
                    </div>
                  )}

                  {serviceRequests.length > 0 && (
                    <div className="mt-8">
                      <h2 className="mb-4 font-display text-xl font-bold">Active Service Requests</h2>
                      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
                        {serviceRequests.map((req) => (
                          <div key={req.id} className="flex items-center justify-between rounded-2xl border border-warning bg-warning/5 p-4 shadow-sm">
                            <div>
                              <span className="text-xs uppercase tracking-wider text-muted-foreground">{req.tableName}</span>
                              <p className="font-semibold text-warning">{req.type === 'CallWaiter' ? 'Waiter Requested' : 'Bill Requested'}</p>
                              <p className="text-xs text-muted-foreground mt-1">{relativeTime(req.createdAt)} • {req.status}</p>
                            </div>
                            <div className="flex gap-2">
                              {req.status === 'Pending' && (
                                <button
                                  onClick={() => manageServiceReq({ id: req.id, action: "acknowledge" })}
                                  className="rounded-full bg-secondary px-3 py-1.5 text-xs font-semibold"
                                >
                                  Acknowledge
                                </button>
                              )}
                              <button
                                onClick={() => manageServiceReq({ id: req.id, action: "complete" })}
                                className="rounded-full bg-warning px-3 py-1.5 text-xs font-semibold text-warning-foreground"
                              >
                                Done
                              </button>
                            </div>
                          </div>
                        ))}
                      </div>
                    </div>
                  )}

                  <div className="mt-8 flex flex-wrap items-center gap-3 rounded-2xl border border-border bg-card p-3 shadow-soft">
                    <div className="flex items-center gap-1.5 text-sm text-muted-foreground"><Filter className="h-4 w-4" /> Status</div>
                    <div className="flex flex-wrap gap-1.5">
                      {(["all", ...flow, "cancelled", "rejected"] as const).map((s) => (
                        <button
                          key={s}
                          onClick={() => setFilter(s as any)}
                          className={`rounded-full px-3 py-1.5 text-xs font-semibold capitalize transition ${filter === s ? "bg-foreground text-background" : "bg-secondary text-muted-foreground hover:text-foreground"}`}
                        >
                          {s === "all" ? "All" : labels[s as OrderStatus]}
                        </button>
                      ))}
                    </div>
                    <div className="ml-auto flex items-center gap-2">
                      <button
                        type="button"
                        onClick={() => setShowPrintingSettings(true)}
                        className="inline-flex items-center gap-1.5 rounded-full border border-border bg-card px-3 py-1.5 text-xs font-semibold hover:bg-secondary transition"
                      >
                        <Printer className="h-3.5 w-3.5 text-primary" /> Printers & Stations
                      </button>
                      <div className="relative">
                        <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                        <input
                          value={tableQ}
                          onChange={(e) => setTableQ(e.target.value)}
                          placeholder="Filter by table…"
                          className="w-40 rounded-full border border-border bg-background py-1.5 pl-9 pr-3 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
                        />
                      </div>
                    </div>
                  </div>

                  <div className="mt-6 grid gap-5 md:grid-cols-2 xl:grid-cols-3">
                    {loadingOrders ? (
                      <div className="col-span-full grid place-items-center py-16">
                        <Loader2 className="h-6 w-6 animate-spin text-primary" />
                      </div>
                    ) : visible.length === 0 ? (
                      <div className="col-span-full rounded-3xl border border-dashed border-border bg-card p-12 text-center">
                        <ChefHat className="mx-auto h-8 w-8 text-muted-foreground" />
                        <p className="mt-3 text-sm font-medium">No active floor orders.</p>
                        <p className="mt-1 text-xs text-muted-foreground">Take an order using Staff Order Taking or customer QR code.</p>
                      </div>
                    ) : (
                      visible.map((o) => (
                        <OrderCard
                          key={o.id}
                          order={o}
                          isProcessing={processingId === o.id}
                          onAdvance={advance}
                          onException={(id, action) => setExceptionModal({ orderId: id, action })}
                          onFireItems={handleFireItems}
                          onRefillItem={handleRefillItem}
                          onCancelItem={(orderId, orderItemId, itemName, maxQty, wasReleased) => {
                            setCancelItemModal({ orderId, orderItemId, itemName, maxQty, wasReleased });
                            setCancelQty(1);
                            setCancelOutcome("NotCooked");
                            setCancelReason("");
                            setManagerPin("");
                          }}
                        />
                      ))
                    )}
                  </div>
                </>
              )}

              {/* 3. KITCHEN QUEUE WORKSPACE */}
              {currentTab === "kitchen" && (
                <>
                  <div className="mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
                    <Stat title="Active in Kitchen" value={counts.active} icon={Bell} accent />
                    <Stat title="Preparing" value={counts.preparing} icon={Flame} />
                    <Stat title="Ready for Dispatch" value={counts.ready} icon={PackageCheck} />
                    <Stat title="Completed Today" value={counts.served} icon={Check} />
                  </div>

                  <div className="mt-8 flex flex-wrap items-center gap-3 rounded-2xl border border-border bg-card p-3 shadow-soft">
                    <div className="flex items-center gap-1.5 text-sm text-muted-foreground"><Filter className="h-4 w-4" /> Status</div>
                    <div className="flex flex-wrap gap-1.5">
                      {(["all", ...flow, "cancelled", "rejected"] as const).map((s) => (
                        <button
                          key={s}
                          onClick={() => setFilter(s as any)}
                          className={`rounded-full px-3 py-1.5 text-xs font-semibold capitalize transition ${filter === s ? "bg-foreground text-background" : "bg-secondary text-muted-foreground hover:text-foreground"}`}
                        >
                          {s === "all" ? "All" : labels[s as OrderStatus]}
                        </button>
                      ))}
                    </div>
                    <div className="ml-auto flex items-center gap-2">
                      <button
                        type="button"
                        onClick={() => setShowPrintingSettings(true)}
                        className="inline-flex items-center gap-1.5 rounded-full border border-border bg-card px-3 py-1.5 text-xs font-semibold hover:bg-secondary transition"
                      >
                        <Printer className="h-3.5 w-3.5 text-primary" /> Printers & Stations
                      </button>
                      <div className="relative">
                        <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                        <input
                          value={tableQ}
                          onChange={(e) => setTableQ(e.target.value)}
                          placeholder="Filter by table…"
                          className="w-40 rounded-full border border-border bg-background py-1.5 pl-9 pr-3 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
                        />
                      </div>
                    </div>
                  </div>

                  <div className="mt-6 grid gap-5 md:grid-cols-2 xl:grid-cols-3">
                    {loadingOrders ? (
                      <div className="col-span-full grid place-items-center py-16">
                        <Loader2 className="h-6 w-6 animate-spin text-primary" />
                      </div>
                    ) : visible.length === 0 ? (
                      <div className="col-span-full rounded-3xl border border-dashed border-border bg-card p-12 text-center">
                        <ChefHat className="mx-auto h-8 w-8 text-muted-foreground" />
                        <p className="mt-3 text-sm font-medium">No tickets in kitchen queue.</p>
                        <p className="mt-1 text-xs text-muted-foreground">Newly placed orders will appear here automatically.</p>
                      </div>
                    ) : (
                      visible.map((o) => (
                        <OrderCard
                          key={o.id}
                          order={o}
                          isProcessing={processingId === o.id}
                          onAdvance={advance}
                          onException={(id, action) => setExceptionModal({ orderId: id, action })}
                          onFireItems={handleFireItems}
                          onRefillItem={handleRefillItem}
                          onCancelItem={(orderId, orderItemId, itemName, maxQty, wasReleased) => {
                            setCancelItemModal({ orderId, orderItemId, itemName, maxQty, wasReleased });
                            setCancelQty(1);
                            setCancelOutcome("NotCooked");
                            setCancelReason("");
                            setManagerPin("");
                          }}
                        />
                      ))
                    )}
                  </div>
                </>
              )}

              {/* Manager-Approved Item Cancellation Modal */}
              {cancelItemModal && (
                <div className="fixed inset-0 z-50 flex items-center justify-center bg-background/80 p-4 backdrop-blur-sm">
                  <div className="w-full max-w-md rounded-3xl border border-border bg-card p-6 shadow-card animate-in zoom-in-95 space-y-4">
                    <div>
                      <h3 className="font-display text-lg font-bold text-destructive">
                        Cancel Item: {cancelItemModal.itemName}
                      </h3>
                      <p className="mt-1 text-xs text-muted-foreground">
                        {cancelItemModal.wasReleased
                          ? "Food was released to kitchen. Requires production outcome and Manager PIN authorization."
                          : "Held item not yet released to kitchen."}
                      </p>
                    </div>

                    <div className="space-y-3 text-xs">
                      <div>
                        <label className="font-semibold text-foreground">Quantity to Cancel (Max: {cancelItemModal.maxQty})</label>
                        <input
                          type="number"
                          min={1}
                          max={cancelItemModal.maxQty}
                          value={cancelQty}
                          onChange={(e) => setCancelQty(Math.min(cancelItemModal.maxQty, Math.max(1, parseInt(e.target.value) || 1)))}
                          className="w-full mt-1 rounded-md border border-border bg-background px-3 py-2 focus:outline-none focus:ring-2 focus:ring-ring"
                        />
                      </div>

                      <div>
                        <label className="font-semibold text-foreground">Production Outcome</label>
                        <select
                          value={cancelOutcome}
                          onChange={(e) => setCancelOutcome(e.target.value as any)}
                          className="w-full mt-1 rounded-md border border-border bg-background px-3 py-2 focus:outline-none focus:ring-2 focus:ring-ring"
                        >
                          <option value="NotCooked">Not Cooked (Release reservation, ₹0 waste loss)</option>
                          <option value="KitchenWaste">Kitchen Waste (Already cooked, record ingredient waste)</option>
                        </select>
                      </div>

                      <div>
                        <label className="font-semibold text-foreground">Cancellation Reason</label>
                        <input
                          placeholder="e.g. Customer changed mind, food burnt, wrong order"
                          value={cancelReason}
                          onChange={(e) => setCancelReason(e.target.value)}
                          required
                          className="w-full mt-1 rounded-md border border-border bg-background px-3 py-2 focus:outline-none focus:ring-2 focus:ring-ring"
                        />
                      </div>

                      {cancelItemModal.wasReleased && (
                        <div>
                          <label className="font-semibold text-foreground">Manager PIN (4-8 digits)</label>
                          <input
                            type="password"
                            placeholder="Enter Manager PIN"
                            value={managerPin}
                            onChange={(e) => setManagerPin(e.target.value)}
                            required
                            className="w-full mt-1 rounded-md border border-border bg-background px-3 py-2 focus:outline-none focus:ring-2 focus:ring-ring"
                          />
                          <p className="mt-1 text-[10px] text-muted-foreground">Rate-limited to 5 attempts before lockout.</p>
                        </div>
                      )}
                    </div>

                    <div className="flex justify-end gap-3 pt-2">
                      <button
                        type="button"
                        onClick={() => setCancelItemModal(null)}
                        className="rounded-full px-4 py-2 text-xs font-semibold hover:bg-secondary"
                      >
                        Close
                      </button>
                      <button
                        type="button"
                        disabled={!cancelReason.trim() || (cancelItemModal.wasReleased && !managerPin.trim()) || processingId !== null}
                        onClick={handleItemCancelSubmit}
                        className="rounded-full bg-destructive px-4 py-2 text-xs font-semibold text-destructive-foreground hover:bg-destructive/90 disabled:opacity-50"
                      >
                        {processingId ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : "Confirm Item Cancellation"}
                      </button>
                    </div>
                  </div>
                </div>
              )}

              {/* Exception Modal */}
              {exceptionModal && (
                <div className="fixed inset-0 z-50 flex items-center justify-center bg-background/80 p-4 backdrop-blur-sm">
                  <div className="w-full max-w-sm rounded-3xl border border-border bg-card p-6 shadow-card animate-in zoom-in-95">
                    <h3 className="font-display text-lg font-bold">
                      {exceptionModal.action === "rejected" ? "Reject Order" : "Cancel Order"}
                    </h3>
                    <p className="mt-2 text-sm text-muted-foreground">Please provide a reason for this action.</p>

                    <div className="mt-4 space-y-2">
                      <select
                        value={exceptionReason}
                        onChange={(e) => setExceptionReason(e.target.value)}
                        className="w-full rounded-md border border-border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
                      >
                        <option value="" disabled>Select a reason...</option>
                        <option value="Item unavailable">Item unavailable</option>
                        <option value="Kitchen unavailable">Kitchen unavailable</option>
                        <option value="Customer requested cancellation">Customer requested cancellation</option>
                        <option value="Duplicate order">Duplicate order</option>
                        <option value="Other">Other</option>
                      </select>
                      {exceptionReason === "Other" && (
                        <input
                          autoFocus
                          placeholder="Type reason..."
                          className="w-full rounded-md border border-border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
                          onChange={(e) => setExceptionReason(e.target.value)} // Note: in a real app you'd separate the custom reason state
                        />
                      )}
                    </div>

                    <div className="mt-6 flex justify-end gap-3">
                      <button onClick={() => { setExceptionModal(null); setExceptionReason(""); }} className="rounded-full px-4 py-2 text-sm font-semibold hover:bg-secondary">
                        Back
                      </button>
                      <button
                        disabled={!exceptionReason.trim() || processingId !== null}
                        onClick={handleExceptionSubmit}
                        className="rounded-full bg-destructive px-4 py-2 text-sm font-semibold text-destructive-foreground hover:bg-destructive/90 disabled:opacity-50"
                      >
                        {processingId ? <Loader2 className="h-4 w-4 animate-spin" /> : "Confirm"}
                      </button>
                    </div>
                  </div>
                </div>
              )}
              {showPrintingSettings && user?.restaurantId && (
                <KitchenPrintingSettingsModal
                  restaurantId={user.restaurantId}
                  onClose={() => setShowPrintingSettings(false)}
                />
              )}
            </main>
      </div>
    </AppLayout>
  );
}

function Stat({ title, value, icon: Icon, accent }: { title: string; value: number; icon: typeof Bell; accent?: boolean }) {
  return (
    <div className={`rounded-3xl border border-border p-5 shadow-soft ${accent ? "bg-gradient-primary text-primary-foreground" : "bg-card"}`}>
      <div className="flex items-center justify-between">
        <span className={`grid h-10 w-10 place-items-center rounded-2xl ${accent ? "bg-white/20" : "bg-primary-soft text-primary"}`}>
          <Icon className="h-4 w-4" />
        </span>
        <p className={`text-xs uppercase tracking-wider ${accent ? "opacity-80" : "text-muted-foreground"}`}>{title}</p>
      </div>
      <p className="mt-4 font-display text-4xl font-extrabold">{value}</p>
    </div>
  );
}

function OrderCard({
  order,
  isProcessing,
  onAdvance,
  onException,
  onFireItems,
  onRefillItem,
  onCancelItem,
}: {
  order: Order;
  isProcessing?: boolean;
  onAdvance: (id: string, s: OrderStatus) => void;
  onException: (id: string, action: "rejected" | "cancelled") => void;
  onFireItems: (orderId: string, courseName?: string) => void;
  onRefillItem: (orderId: string, orderItemId: string) => void;
  onCancelItem: (orderId: string, orderItemId: string, itemName: string, maxQty: number, wasReleased: boolean) => void;
}) {
  const idx = flow.indexOf(order.status);
  const next = flow[idx + 1];
  const total = order.total;
  const hasHeldItems = order.items.some((it) => it.release_state === "Held" && !it.is_cancelled);
  const elapsedMinutes = Math.floor((Date.now() - new Date(order.placed_at).getTime()) / 60000);
  const isDelayed = elapsedMinutes >= 15 && order.status !== "served" && order.status !== "cancelled" && order.status !== "rejected";

  return (
    <article className="flex flex-col rounded-3xl border border-border bg-card p-5 shadow-soft transition-shadow hover:shadow-card">
      <div className="flex items-start justify-between">
        <div>
          <div className="flex items-center gap-1.5 flex-wrap">
            <p className="text-xs uppercase tracking-wider text-muted-foreground">ORD-{String(order.order_number).padStart(4, "0")}</p>
            {order.delivery_provider && (
              <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-[10px] font-bold ${order.delivery_provider.toLowerCase() === "swiggy"
                ? "bg-orange-100 text-orange-800"
                : order.delivery_provider.toLowerCase() === "zomato"
                  ? "bg-red-100 text-red-800"
                  : "bg-blue-100 text-blue-800"
                }`}>
                🛵 {order.delivery_provider}
              </span>
            )}
            {order.external_order_reference && (
              <span className="text-[10px] font-mono font-bold text-muted-foreground">
                Ref: {order.external_order_reference}
              </span>
            )}
          </div>
          <div className="flex items-center gap-2">
            <h3 className="font-display text-xl font-bold">
              {order.delivery_provider || order.channel === "Delivery"
                ? `Delivery ${order.daily_token_number ? `#${String(order.daily_token_number).padStart(3, "0")}` : ""}`
                : `Table ${order.table_number}${order.group_label || ""}`}
            </h3>
            {(!order.delivery_provider && order.channel !== "Delivery") && (
              <Link
                to="/dashboard"
                search={{ tab: "order", table: String(order.table_id || order.table_number) }}
                className="inline-flex items-center gap-1 rounded-full bg-primary/10 px-2.5 py-0.5 text-[11px] font-semibold text-primary hover:bg-primary/20"
              >
                + New Order
              </Link>
            )}
          </div>
        </div>
        <div className="flex flex-col items-end gap-1">
          <span className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-[11px] font-semibold ${tone[order.status] || tone.placed}`}>
            {order.status === "preparing" && <span className="h-1.5 w-1.5 animate-pulse-dot rounded-full bg-current" />}
            {labels[order.status] || order.status}
          </span>
          {hasHeldItems && (
            <span className="rounded-full bg-amber-100 text-amber-800 px-2 py-0.5 text-[10px] font-bold">
              Has Held Items ⏸
            </span>
          )}
        </div>
      </div>
      <div className="mt-1 flex items-center gap-2">
        <p className="inline-flex items-center gap-1 text-xs text-muted-foreground"><Timer className="h-3 w-3" /> {relativeTime(order.placed_at)}</p>
        {isDelayed && (
          <span className="rounded-full bg-destructive/15 text-destructive px-2 py-0.5 text-[10px] font-black animate-pulse">
            ⚠️ {elapsedMinutes}m Delay
          </span>
        )}
      </div>

      {order.exception_reason && (
        <div className="mt-3 rounded-lg bg-destructive/10 p-2.5 text-xs text-destructive">
          <span className="font-semibold">Reason:</span> {order.exception_reason}
        </div>
      )}

      {hasHeldItems && order.status !== "cancelled" && order.status !== "rejected" && (
        <div className="mt-3 flex items-center justify-between rounded-xl bg-amber-500/10 p-2 text-xs">
          <span className="font-semibold text-amber-800">Held production courses</span>
          <button
            type="button"
            disabled={isProcessing}
            onClick={() => onFireItems(order.id)}
            className="rounded-full bg-amber-600 hover:bg-amber-700 px-2.5 py-1 text-[11px] font-bold text-white transition-colors"
          >
            🔥 Fire All Held
          </button>
        </div>
      )}

      <ul className="mt-4 flex-1 space-y-2 text-sm">
        {order.items.map((it) => {
          const isHeld = it.release_state === "Held";
          const isRefill = it.is_refill;
          const isCancelled = it.is_cancelled || (it.cancelled_quantity ?? 0) >= it.qty;
          const activeQty = it.qty - (it.cancelled_quantity ?? 0);

          return (
            <li key={it.id} className="flex flex-col gap-1 border-b border-border/40 pb-2">
              <div className="flex items-center justify-between">
                <span className="flex items-center gap-1.5 flex-wrap">
                  <span className="font-semibold text-primary">{activeQty}×</span>
                  <span className={isCancelled ? "line-through text-muted-foreground" : "font-medium"}>
                    {it.name}
                  </span>
                  {it.course_name && (
                    <span className="rounded bg-secondary px-1.5 py-0.5 text-[10px] font-semibold text-muted-foreground">
                      {it.course_name}
                    </span>
                  )}
                  {it.packing_intent === "Takeaway" && (
                    <span className="rounded bg-purple-100 text-purple-800 px-1.5 py-0.5 text-[10px] font-bold">
                      Parcel 🥡
                    </span>
                  )}
                  {isHeld && (
                    <span className="rounded bg-amber-100 text-amber-800 px-1.5 py-0.5 text-[10px] font-bold">
                      HELD ⏸
                    </span>
                  )}
                  {isRefill && (
                    <span className="rounded bg-blue-100 text-blue-800 px-1.5 py-0.5 text-[10px] font-bold">
                      REFILL #{it.refill_count ?? 1} 🔄
                    </span>
                  )}
                  {isCancelled && (
                    <span className="rounded bg-destructive/15 text-destructive px-1.5 py-0.5 text-[10px] font-bold">
                      CANCELLED
                    </span>
                  )}
                </span>
                <span className="text-muted-foreground">{formatINR(activeQty * it.price)}</span>
              </div>
              {it.notes && (
                <p className="text-[11px] text-amber-700 italic pl-5">Note: {it.notes}</p>
              )}

              {/* Item-level actions */}
              {!isCancelled && order.status !== "served" && order.status !== "cancelled" && (
                <div className="flex items-center gap-1.5 pt-0.5 text-[11px]">
                  {isHeld && (
                    <button
                      type="button"
                      disabled={isProcessing}
                      onClick={() => onFireItems(order.id, it.course_name)}
                      className="rounded-full bg-amber-500 hover:bg-amber-600 px-2 py-0.5 font-bold text-white transition-colors"
                    >
                      Fire Now 🔥
                    </button>
                  )}
                  <button
                    type="button"
                    disabled={isProcessing}
                    onClick={() => onRefillItem(order.id, it.id)}
                    className="rounded-full border border-blue-400 bg-blue-50 hover:bg-blue-100 px-2 py-0.5 font-semibold text-blue-700 transition-colors"
                  >
                    + Refill
                  </button>
                  <button
                    type="button"
                    disabled={isProcessing}
                    onClick={() => onCancelItem(order.id, it.id, it.name, activeQty, !isHeld && order.status !== "placed")}
                    className="rounded-full border border-destructive/40 bg-destructive/5 hover:bg-destructive/15 px-2 py-0.5 font-semibold text-destructive transition-colors"
                  >
                    Cancel
                  </button>
                </div>
              )}
            </li>
          );
        })}
      </ul>
      <div className="mt-4 flex items-center justify-between border-t border-border pt-3">
        <span className="font-display text-base font-bold">{formatINR(total)}</span>

        {order.status !== "cancelled" && order.status !== "rejected" && order.status !== "served" ? (
          <div className="flex gap-2 items-center">
            <button
              disabled={isProcessing}
              onClick={() => onException(order.id, order.status === "placed" ? "rejected" : "cancelled")}
              className="inline-flex items-center rounded-full border border-border bg-background px-3 py-2 text-xs font-semibold hover:bg-destructive hover:text-destructive-foreground hover:border-destructive transition-colors disabled:opacity-50"
            >
              {order.status === "placed" ? "Reject" : "Cancel"}
            </button>
            {next && (
              <button
                disabled={isProcessing}
                onClick={() => onAdvance(order.id, next)}
                className="inline-flex items-center gap-1.5 rounded-full bg-gradient-primary px-4 py-2 text-xs font-semibold text-primary-foreground shadow-soft disabled:opacity-50"
              >
                {isProcessing ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : null}
                Mark as {labels[next]}
              </button>
            )}
          </div>
        ) : (
          <span className="text-xs text-muted-foreground">Closed</span>
        )}
      </div>
    </article>
  );
}
