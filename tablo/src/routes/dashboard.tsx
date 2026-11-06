import { createFileRoute, Link } from "@tanstack/react-router";
import { useMemo, useState } from "react";
import { Bell, Check, ChefHat, Filter, Flame, Loader2, LogIn, LogOut, PackageCheck, Search, ShieldAlert, Timer } from "lucide-react";
import { toast } from "sonner";
import { SiteHeader } from "@/components/site-header";
import { ServiceDesk } from "@/components/service-desk";
import { useAuth } from "@/hooks/use-auth";
import { formatINR, relativeTime } from "@/lib/mock-data";
import { Order, OrderStatus } from "@/types";
import { useOrders, useUpdateOrderStatus } from "@/hooks/use-orders";
import { useActiveServiceRequests, useManageServiceRequest } from "@/hooks/use-service-requests";

export const Route = createFileRoute("/dashboard")({
  head: () => ({
    meta: [
      { title: "Waiter & kitchen dashboard — Tablo" },
      { name: "description", content: "Live order queue, status flow and table filters for restaurant staff." },
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
  const [showServiceDesk, setShowServiceDesk] = useState(false);
  const canManageBills = user?.role === "Admin" || user?.role === "Waiter";
  
  const { data: orders = [], isLoading: loadingOrders } = useOrders(user?.restaurantId || null);
  const { mutateAsync: updateStatus } = useUpdateOrderStatus();
  
  const { data: serviceRequests = [] } = useActiveServiceRequests(user?.restaurantId || null);
  const { mutate: manageServiceReq } = useManageServiceRequest();

  const [filter, setFilter] = useState<OrderStatus | "all">("all");
  const [tableQ, setTableQ] = useState("");
  const [processingId, setProcessingId] = useState<string | null>(null);

  // Modal State for Exception
  const [exceptionModal, setExceptionModal] = useState<{ orderId: string, action: "rejected" | "cancelled" } | null>(null);
  const [exceptionReason, setExceptionReason] = useState("");

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
    <div className="min-h-screen bg-secondary/40">
      <SiteHeader />
      <main className="mx-auto max-w-7xl px-4 py-8 sm:px-6">
        <div className="flex flex-wrap items-end justify-between gap-3">
          <div>
            <p className="text-xs font-semibold uppercase tracking-wider text-primary">Live kitchen view</p>
            <h1 className="mt-1 font-display text-3xl font-bold">Service in progress</h1>
            <p className="mt-1 text-xs text-muted-foreground">Signed in as {user?.email}</p>
          </div>
          <div className="flex items-center gap-2">
            {canManageBills && <button type="button" aria-expanded={showServiceDesk} onClick={() => setShowServiceDesk(value => !value)} className="rounded-full bg-primary px-4 py-2 text-xs font-semibold text-primary-foreground">{showServiceDesk ? "Hide orders & billing" : "Orders & billing"}</button>}
            <span className="inline-flex items-center gap-2 rounded-full bg-card px-3 py-1.5 text-xs font-medium shadow-soft">
              <span className="h-1.5 w-1.5 animate-pulse-dot rounded-full bg-primary" /> Real-time updates
            </span>
            <button onClick={() => void signOut()} className="inline-flex items-center gap-1.5 rounded-full border border-border bg-card px-3 py-1.5 text-xs font-semibold">
              <LogOut className="h-3.5 w-3.5" /> Sign out
            </button>
          </div>
        </div>

        {showServiceDesk && canManageBills && user?.restaurantId && <ServiceDesk restaurantId={user.restaurantId} employeeId={user.id} />}

        <div className="mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <Stat title="Active orders" value={counts.active} icon={Bell} accent />
          <Stat title="Preparing" value={counts.preparing} icon={Flame} />
          <Stat title="Ready" value={counts.ready} icon={PackageCheck} />
          <Stat title="Served today" value={counts.served} icon={Check} />
        </div>

        {serviceRequests.length > 0 && (
          <div className="mt-8">
            <h2 className="mb-4 font-display text-xl font-bold">Active Service Requests</h2>
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              {serviceRequests.map((req) => (
                <div key={req.id} className="flex items-center justify-between rounded-2xl border border-warning bg-warning/5 p-4 shadow-sm">
                  <div>
                    <span className="text-xs uppercase tracking-wider text-muted-foreground">Table {req.tableName}</span>
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
          <div className="ml-auto relative">
            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <input
              value={tableQ}
              onChange={(e) => setTableQ(e.target.value)}
              placeholder="Filter by table…"
              className="w-44 rounded-full border border-border bg-background py-2 pl-9 pr-3 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
            />
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
              <p className="mt-3 text-sm font-medium">No orders match your filters.</p>
              <p className="mt-1 text-xs text-muted-foreground">Place a test order from the customer page to see it appear here in real time.</p>
            </div>
          ) : (
            visible.map((o) => <OrderCard key={o.id} order={o} isProcessing={processingId === o.id} onAdvance={advance} onException={(id, action) => setExceptionModal({ orderId: id, action })} />)
          )}
        </div>

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
      </main>
    </div>
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

function OrderCard({ order, isProcessing, onAdvance, onException }: { order: Order; isProcessing?: boolean; onAdvance: (id: string, s: OrderStatus) => void; onException: (id: string, action: "rejected" | "cancelled") => void }) {
  const idx = flow.indexOf(order.status);
  const next = flow[idx + 1];
  const total = order.total;
  return (
    <article className="flex flex-col rounded-3xl border border-border bg-card p-5 shadow-soft transition-shadow hover:shadow-card">
      <div className="flex items-start justify-between">
        <div>
          <p className="text-xs uppercase tracking-wider text-muted-foreground">ORD-{String(order.order_number).padStart(4, "0")}</p>
          <h3 className="font-display text-xl font-bold">Table {order.table_number}</h3>
        </div>
        <span className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-[11px] font-semibold ${tone[order.status] || tone.placed}`}>
          {order.status === "preparing" && <span className="h-1.5 w-1.5 animate-pulse-dot rounded-full bg-current" />}
          {labels[order.status] || order.status}
        </span>
      </div>
      <p className="mt-1 inline-flex items-center gap-1 text-xs text-muted-foreground"><Timer className="h-3 w-3" /> {relativeTime(order.placed_at)}</p>
      
      {order.exception_reason && (
        <div className="mt-3 rounded-lg bg-destructive/10 p-2.5 text-xs text-destructive">
          <span className="font-semibold">Reason:</span> {order.exception_reason}
        </div>
      )}

      <ul className="mt-4 flex-1 space-y-1.5 text-sm">
        {order.items.map((it) => (
          <li key={it.id} className="flex justify-between">
            <span><span className="font-semibold text-primary">{it.qty}×</span> {it.name}</span>
            <span className="text-muted-foreground">{formatINR(it.qty * it.price)}</span>
          </li>
        ))}
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
