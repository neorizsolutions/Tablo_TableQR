import { createFileRoute, Link } from "@tanstack/react-router";
import { useState } from "react";
import { Loader2, LogIn, LogOut, Search, X, FileText, ChevronLeft, ChevronRight } from "lucide-react";
import { toast } from "sonner";
import { SiteHeader } from "@/components/site-header";
import { useAuth } from "@/hooks/use-auth";
import { useReports } from "@/hooks/use-reports";
import { useTables } from "@/hooks/use-tables";
import { ReportOrderDto } from "@/services/api";

export const Route = createFileRoute("/reports")({
  head: () => ({
    meta: [
      { title: "Order History — Tablo Reports" },
      { name: "description", content: "Historical order reporting." },
    ],
  }),
  component: ReportsPage,
});

function ReportsPage() {
  const { user, roles, loading: authLoading, signOut } = useAuth();
  
  if (authLoading) {
    return (
      <div className="grid min-h-screen place-items-center bg-secondary/40">
        <Loader2 className="h-6 w-6 animate-spin text-primary" />
      </div>
    );
  }

  if (!user) return <SignInGate />;
  
  const isAdmin = roles.includes("Admin") || roles.includes("Owner");
  if (!isAdmin) return <NoAccessGate roles={roles} onSignOut={signOut} />;

  // We need the restaurant ID. The hook fetches for current tenant automatically via the API backend anyway.
  // Wait, useTables requires restaurantId? Yes. useTables usually takes `user.restaurantId` or we get it from auth.
  // Let's check how admin gets it. `admin.tsx` gets it from `useRestaurant("saffron-sage")` currently.
  // But our backend infers tenant from JWT for tables now if we use a specific endpoint, or we just pass the ID.
  return <ReportsInner />;
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
          <h1 className="mt-4 font-display text-2xl font-bold">Admin sign in required</h1>
          <p className="mt-2 text-sm text-muted-foreground">
            The reports panel is private. Sign in with an admin account to continue.
          </p>
          <div className="mt-6 flex flex-col gap-3">
            <Link to="/auth" className="rounded-full bg-primary px-4 py-2.5 text-sm font-semibold text-primary-foreground shadow-glow hover:bg-primary/90">
              Sign in
            </Link>
          </div>
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
          <span className="mx-auto grid h-12 w-12 place-items-center rounded-2xl bg-red-500/10 text-red-500">
            <LogOut className="h-5 w-5" />
          </span>
          <h1 className="mt-4 font-display text-2xl font-bold">Access Denied</h1>
          <p className="mt-2 text-sm text-muted-foreground">
            Your account is signed in as <span className="font-semibold">{roles.join(", ") || "staff"}</span>. You must be an <span className="font-semibold">Admin</span> or <span className="font-semibold">Owner</span> to access this page.
          </p>
          <div className="mt-6 flex flex-col gap-3">
            <Link to="/dashboard" className="rounded-full bg-primary px-4 py-2.5 text-sm font-semibold text-primary-foreground shadow-glow hover:bg-primary/90">
              Go to Dashboard
            </Link>
            <button onClick={onSignOut} className="rounded-full border border-border bg-background px-4 py-2.5 text-sm font-semibold hover:bg-secondary">
              Sign out
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

function ReportsInner() {
  const [startDate, setStartDate] = useState("");
  const [endDate, setEndDate] = useState("");
  const [status, setStatus] = useState("all");
  const [tableId, setTableId] = useState("all");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const pageSize = 20;

  // Assume user belongs to the one mock restaurant for getting tables, or standard API logic
  // Let's just fetch all tables for the dropdown via the mock ID if necessary, or pass empty to trigger JWT-based resolution if the backend supports it.
  // Actually, useTables in api.ts takes restaurantId. Let's hardcode the seed ID for now as the rest of the frontend does.
  const seedId = "29543ada-19bd-439e-a4b9-248aa43e58b5";
  const { data: tables = [] } = useTables(seedId);

  const { data: reportsData, isLoading, isError } = useReports({
    startDate: startDate || undefined,
    endDate: endDate || undefined,
    status,
    tableId,
    search: search || undefined,
    page,
    pageSize,
  });

  const [selectedOrder, setSelectedOrder] = useState<ReportOrderDto | null>(null);

  const handleApply = () => {
    setPage(1); // Reset to first page on new filter
  };

  const handleClear = () => {
    setStartDate("");
    setEndDate("");
    setStatus("all");
    setTableId("all");
    setSearch("");
    setPage(1);
  };

  return (
    <div className="min-h-screen bg-secondary/40">
      <SiteHeader />
      <main className="mx-auto max-w-7xl px-4 py-8 sm:px-6">
        <div className="mb-6">
          <p className="text-xs font-semibold uppercase tracking-wider text-primary">Admin Analytics</p>
          <h1 className="mt-1 font-display text-3xl font-bold">Order History</h1>
          <p className="mt-1 text-sm text-muted-foreground">View past orders and historical data.</p>
        </div>

        {/* Filters */}
        <div className="mb-6 grid gap-4 md:grid-cols-2 lg:grid-cols-6 rounded-2xl border border-border bg-card p-4 shadow-soft">
          <div className="flex flex-col gap-1 lg:col-span-1">
            <label className="text-xs font-medium text-muted-foreground">Start Date</label>
            <input type="date" value={startDate} onChange={e => setStartDate(e.target.value)} className="w-full rounded-md border border-border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring" />
          </div>
          <div className="flex flex-col gap-1 lg:col-span-1">
            <label className="text-xs font-medium text-muted-foreground">End Date</label>
            <input type="date" value={endDate} onChange={e => setEndDate(e.target.value)} className="w-full rounded-md border border-border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring" />
          </div>
          <div className="flex flex-col gap-1 lg:col-span-1">
            <label className="text-xs font-medium text-muted-foreground">Status</label>
            <select value={status} onChange={e => setStatus(e.target.value)} className="w-full rounded-md border border-border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring">
              <option value="all">All Statuses</option>
              <option value="Placed">Placed</option>
              <option value="Preparing">Preparing</option>
              <option value="Ready">Ready</option>
              <option value="Served">Served</option>
            </select>
          </div>
          <div className="flex flex-col gap-1 lg:col-span-1">
            <label className="text-xs font-medium text-muted-foreground">Table</label>
            <select value={tableId} onChange={e => setTableId(e.target.value)} className="w-full rounded-md border border-border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring">
              <option value="all">All Tables</option>
              {tables.map((t: any) => (
                <option key={t.id} value={t.id}>{t.nameOrNumber}</option>
              ))}
            </select>
          </div>
          <div className="flex flex-col gap-1 lg:col-span-1">
            <label className="text-xs font-medium text-muted-foreground">Search Order</label>
            <div className="relative">
              <Search className="pointer-events-none absolute left-2 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
              <input value={search} onChange={e => setSearch(e.target.value)} placeholder="e.g. ORD-0020" className="w-full rounded-md border border-border bg-background py-2 pl-8 pr-3 text-sm focus:outline-none focus:ring-2 focus:ring-ring" />
            </div>
          </div>
          <div className="flex items-end gap-2 lg:col-span-1">
            <button onClick={handleApply} className="flex-1 rounded-md bg-primary px-3 py-2 text-sm font-semibold text-primary-foreground hover:bg-primary/90">
              Apply
            </button>
            <button onClick={handleClear} className="flex-1 rounded-md border border-border bg-secondary px-3 py-2 text-sm font-semibold hover:bg-secondary/80">
              Clear
            </button>
          </div>
        </div>

        {/* Data Table */}
        <div className="overflow-hidden rounded-3xl border border-border bg-card shadow-soft">
          <table className="w-full text-sm">
            <thead className="hidden border-b border-border bg-secondary/50 text-left text-xs uppercase tracking-wider text-muted-foreground md:table-header-group">
              <tr>
                <th className="p-4 font-semibold">Order</th>
                <th className="p-4 font-semibold">Table</th>
                <th className="p-4 font-semibold">Date & Time</th>
                <th className="p-4 font-semibold">Items</th>
                <th className="p-4 font-semibold">Total</th>
                <th className="p-4 font-semibold">Status</th>
                <th className="p-4 text-right font-semibold">Action</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {isLoading && (
                <tr>
                  <td colSpan={7} className="p-10 text-center">
                    <Loader2 className="mx-auto h-6 w-6 animate-spin text-primary" />
                    <p className="mt-2 text-muted-foreground">Loading historical data...</p>
                  </td>
                </tr>
              )}
              {isError && (
                <tr>
                  <td colSpan={7} className="p-10 text-center text-red-500">
                    Failed to load order history.
                  </td>
                </tr>
              )}
              {!isLoading && !isError && reportsData?.items.length === 0 && (
                <tr>
                  <td colSpan={7} className="p-10 text-center text-muted-foreground">
                    <FileText className="mx-auto h-8 w-8 mb-2 opacity-20" />
                    No orders match the selected filters.
                  </td>
                </tr>
              )}
              {!isLoading && reportsData && reportsData.items.map((order) => (
                <tr key={order.orderId} className="block p-4 md:table-row md:p-0 hover:bg-secondary/20 transition-colors">
                  <td className="block md:table-cell md:p-4 font-medium">
                    <span className="md:hidden font-semibold mr-2">Order:</span>
                    {order.displayOrderNumber}
                  </td>
                  <td className="block md:table-cell md:p-4 text-muted-foreground">
                    <span className="md:hidden font-semibold mr-2 text-foreground">Table:</span>
                    {order.tableName}
                  </td>
                  <td className="block md:table-cell md:p-4 text-muted-foreground">
                    <span className="md:hidden font-semibold mr-2 text-foreground">Date:</span>
                    {new Date(order.createdAt).toLocaleString()}
                  </td>
                  <td className="block md:table-cell md:p-4 text-muted-foreground">
                    <span className="md:hidden font-semibold mr-2 text-foreground">Items:</span>
                    {order.items.reduce((acc, i) => acc + i.quantity, 0)}
                  </td>
                  <td className="block md:table-cell md:p-4 font-semibold">
                    <span className="md:hidden mr-2">Total:</span>
                    ${order.totalAmount.toFixed(2)}
                  </td>
                  <td className="block md:table-cell md:p-4">
                    <span className="md:hidden font-semibold mr-2">Status:</span>
                    <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${
                      order.status === 'Served' ? 'bg-green-500/10 text-green-600' :
                      order.status === 'Ready' ? 'bg-amber-500/10 text-amber-600' :
                      'bg-blue-500/10 text-blue-600'
                    }`}>
                      {order.status}
                    </span>
                  </td>
                  <td className="block mt-4 md:mt-0 md:table-cell md:p-4 text-right">
                    <button onClick={() => setSelectedOrder(order)} className="inline-flex w-full md:w-auto items-center justify-center rounded-md border border-border bg-background px-3 py-1.5 text-sm font-semibold hover:bg-secondary">
                      Details
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          
          {/* Pagination */}
          {!isLoading && reportsData && reportsData.totalPages > 0 && (
            <div className="flex items-center justify-between border-t border-border px-4 py-3 bg-secondary/10">
              <div className="text-sm text-muted-foreground">
                Showing <span className="font-semibold">{(page - 1) * pageSize + 1}</span> to <span className="font-semibold">{Math.min(page * pageSize, reportsData.totalCount)}</span> of <span className="font-semibold">{reportsData.totalCount}</span> results
              </div>
              <div className="flex items-center gap-2">
                <button 
                  disabled={page <= 1}
                  onClick={() => setPage(p => p - 1)}
                  className="inline-flex h-8 w-8 items-center justify-center rounded-md border border-border bg-card disabled:opacity-50 hover:bg-secondary"
                >
                  <ChevronLeft className="h-4 w-4" />
                </button>
                <span className="text-sm font-medium">Page {page} of {reportsData.totalPages}</span>
                <button 
                  disabled={page >= reportsData.totalPages}
                  onClick={() => setPage(p => p + 1)}
                  className="inline-flex h-8 w-8 items-center justify-center rounded-md border border-border bg-card disabled:opacity-50 hover:bg-secondary"
                >
                  <ChevronRight className="h-4 w-4" />
                </button>
              </div>
            </div>
          )}
        </div>
      </main>

      {/* Details Modal */}
      {selectedOrder && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-background/80 p-4 backdrop-blur-sm">
          <div className="w-full max-w-lg overflow-hidden rounded-3xl border border-border bg-card shadow-card animate-in zoom-in-95">
            <div className="flex items-center justify-between border-b border-border bg-secondary/30 px-6 py-4">
              <div>
                <h2 className="font-display text-lg font-bold">Order {selectedOrder.displayOrderNumber}</h2>
                <p className="text-xs text-muted-foreground">Table {selectedOrder.tableName} &bull; {new Date(selectedOrder.createdAt).toLocaleString()}</p>
              </div>
              <button onClick={() => setSelectedOrder(null)} className="rounded-full p-2 hover:bg-secondary">
                <X className="h-5 w-5" />
              </button>
            </div>
            <div className="p-6 max-h-[60vh] overflow-y-auto">
              <div className="space-y-4">
                {selectedOrder.items.map((item, idx) => (
                  <div key={idx} className="flex justify-between items-start gap-4 pb-4 border-b border-border last:border-0 last:pb-0">
                    <div className="flex gap-3">
                      <div className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-secondary text-xs font-semibold">
                        {item.quantity}
                      </div>
                      <div>
                        <p className="font-semibold text-sm leading-tight">{item.itemNameSnapshot}</p>
                        <p className="text-xs text-muted-foreground mt-0.5">Historical Price: ${item.unitPrice.toFixed(2)}</p>
                      </div>
                    </div>
                    <p className="font-semibold whitespace-nowrap">${item.lineTotal.toFixed(2)}</p>
                  </div>
                ))}
              </div>
            </div>
            <div className="border-t border-border bg-secondary/20 p-6 flex justify-between items-center">
              <span className="font-semibold text-muted-foreground">Total Paid</span>
              <span className="font-display text-xl font-bold text-primary">${selectedOrder.totalAmount.toFixed(2)}</span>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
