import { createFileRoute } from "@tanstack/react-router";
import { useState, useEffect, useMemo } from "react";
import { toast } from "sonner";
import { useAuth } from "@/hooks/use-auth";
import { apiClient } from "@/services/apiClient";
import {
  getSysAdminOverview,
  getSysAdminPlans,
  updatePlanFeatures,
  getSysAdminSubscriptions,
  changeSubscription,
  previewSysAdminPlanChange,
  getSysAdminAuditLogs,
  type SuperAdminOverview,
  type PlanItem,
  type SubscriptionItem,
  type SubscriptionAuditLogItem,
  type PlanChangePreview,
} from "@/services/subscription";
import {
  Loader2,
  QrCode,
  Building2,
  Sliders,
  ShieldCheck,
  History,
  LayoutDashboard,
  LogOut,
  Plus,
  Search,
  CheckCircle2,
  XCircle,
  AlertTriangle,
  Users,
  Calendar,
  Sparkles,
  ArrowRight,
  TrendingUp,
  AlertCircle,
  Clock,
  Layers,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";

export const Route = createFileRoute("/super-admin")({
  component: SuperAdminPage,
  head: () => ({
    meta: [{ title: "Super Admin Workspace — Tablo" }],
  }),
});

type TabType = "overview" | "tenants" | "plans" | "subscriptions" | "audit";

function SuperAdminPage() {
  const { user, signIn, signOut } = useAuth();

  if (user && user.role === "SuperAdmin") {
    return <SuperAdminWorkspace onLogout={() => signOut("/super-admin")} />;
  }

  if (user) {
    return (
      <div className="min-h-screen bg-secondary/40 px-4 py-10 flex items-center justify-center">
        <div className="text-center max-w-md bg-card p-8 rounded-2xl shadow border">
          <AlertCircle className="h-10 w-10 text-destructive mx-auto mb-3" />
          <h2 className="text-xl font-bold mb-2">Restricted Access</h2>
          <p className="text-sm text-muted-foreground mb-6">
            You are signed in as standard staff ({user.email}). Super Admin workspace requires platform operator
            credentials.
          </p>
          <Button onClick={() => signOut("/super-admin")}>Sign Out and Switch Account</Button>
        </div>
      </div>
    );
  }

  return <SuperAdminLogin onLogin={signIn} />;
}

/* ==========================================================================
   1. SUPER ADMIN LOGIN
   ========================================================================== */
function SuperAdminLogin({ onLogin }: { onLogin: (token: string) => void }) {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [busy, setBusy] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    try {
      const res = await apiClient<{ token: string }>("/sysadmin/login", {
        method: "POST",
        body: JSON.stringify({ username, password }),
      });
      onLogin(res.token);
      toast.success("Super Admin Authenticated");
    } catch (err: any) {
      toast.error(err.message || "Authentication failed");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-secondary/40 via-background to-primary-soft/30 px-4 py-10 flex items-center justify-center">
      <div className="w-full max-w-md">
        <div className="mb-6 flex items-center justify-center gap-2 font-display text-xl font-bold">
          <span className="grid h-10 w-10 place-items-center rounded-xl bg-gradient-primary text-primary-foreground shadow-soft">
            <QrCode className="h-5 w-5" />
          </span>
          Tablo <span className="text-primary font-normal text-sm ml-1">Super Admin Console</span>
        </div>
        <div className="rounded-2xl border border-border bg-card p-6 shadow-soft sm:p-8">
          <h2 className="font-display text-2xl font-bold">Operator Sign In</h2>
          <p className="mt-1 text-sm text-muted-foreground mb-6">Enter platform credentials to manage tenant entitlements.</p>
          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <label className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">Username</label>
              <Input
                type="text"
                value={username}
                onChange={(e) => setUsername(e.target.value)}
                placeholder="superadmin"
                required
                className="mt-1"
              />
            </div>
            <div>
              <label className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">Password</label>
              <Input
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
                className="mt-1"
              />
            </div>
            <Button disabled={busy} className="w-full mt-4" size="lg">
              {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {busy ? "Authenticating..." : "Sign In to Workspace"}
            </Button>
          </form>
        </div>
      </div>
    </div>
  );
}

/* ==========================================================================
   2. SUPER ADMIN WORKSPACE (Tabs: Overview, Tenants, Plans, Subscriptions, Audit)
   ========================================================================== */
function SuperAdminWorkspace({ onLogout }: { onLogout: () => void }) {
  const [activeTab, setActiveTab] = useState<TabType>("overview");

  return (
    <div className="min-h-screen flex flex-col bg-background text-foreground">
      {/* Top Header */}
      <header className="sticky top-0 z-30 flex h-16 w-full items-center justify-between border-b border-border bg-card/90 px-4 backdrop-blur sm:px-6">
        <div className="flex items-center gap-3">
          <span className="grid h-9 w-9 place-items-center rounded-xl bg-gradient-primary text-primary-foreground shadow-soft">
            <QrCode className="h-4 w-4" />
          </span>
          <div className="flex items-center gap-2">
            <span className="font-display text-lg font-bold">
              Tablo<span className="text-primary">.</span>
            </span>
            <span className="rounded-full bg-primary/10 px-2.5 py-0.5 text-xs font-bold text-primary">
              Platform Super Admin
            </span>
          </div>
        </div>

        <div className="flex items-center gap-3">
          <Button variant="outline" size="sm" onClick={onLogout} className="gap-1.5 text-xs">
            <LogOut className="h-3.5 w-3.5" /> Sign Out
          </Button>
        </div>
      </header>

      {/* Main Workspace Body: Tabs Bar + Content */}
      <div className="flex-1 flex flex-col">
        {/* Navigation Tabs */}
        <div className="border-b border-border/80 bg-secondary/30 px-4 sm:px-6">
          <div className="flex space-x-1 overflow-x-auto py-2">
            <TabButton
              active={activeTab === "overview"}
              onClick={() => setActiveTab("overview")}
              icon={LayoutDashboard}
              label="Overview"
            />
            <TabButton
              active={activeTab === "tenants"}
              onClick={() => setActiveTab("tenants")}
              icon={Building2}
              label="Restaurants"
            />
            <TabButton
              active={activeTab === "plans"}
              onClick={() => setActiveTab("plans")}
              icon={Sliders}
              label="Plans & Features"
            />
            <TabButton
              active={activeTab === "subscriptions"}
              onClick={() => setActiveTab("subscriptions")}
              icon={ShieldCheck}
              label="Subscriptions"
            />
            <TabButton
              active={activeTab === "audit"}
              onClick={() => setActiveTab("audit")}
              icon={History}
              label="Audit Log"
            />
          </div>
        </div>

        {/* Tab Content Panes */}
        <main className="flex-1 p-4 sm:p-6 max-w-7xl w-full mx-auto">
          {activeTab === "overview" && <OverviewPane onNavigateTab={(t) => setActiveTab(t)} />}
          {activeTab === "tenants" && <TenantsPane onManageSub={() => setActiveTab("subscriptions")} />}
          {activeTab === "plans" && <PlansPane />}
          {activeTab === "subscriptions" && <SubscriptionsPane />}
          {activeTab === "audit" && <AuditLogPane />}
        </main>
      </div>
    </div>
  );
}

function TabButton({
  active,
  onClick,
  icon: Icon,
  label,
}: {
  active: boolean;
  onClick: () => void;
  icon: any;
  label: string;
}) {
  return (
    <button
      onClick={onClick}
      className={`inline-flex items-center gap-2 rounded-lg px-3.5 py-2 text-xs font-semibold transition ${
        active
          ? "bg-card text-foreground shadow-xs border border-border"
          : "text-muted-foreground hover:bg-card/50 hover:text-foreground"
      }`}
    >
      <Icon className={`h-4 w-4 ${active ? "text-primary" : "text-muted-foreground"}`} />
      <span>{label}</span>
    </button>
  );
}

/* ==========================================================================
   TAB 1: OVERVIEW PANE
   ========================================================================== */
function OverviewPane({ onNavigateTab }: { onNavigateTab: (tab: TabType) => void }) {
  const [data, setData] = useState<SuperAdminOverview | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    getSysAdminOverview()
      .then(setData)
      .catch((err) => toast.error(err.message || "Failed to load overview"))
      .finally(() => setLoading(false));
  }, []);

  if (loading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <Loader2 className="h-6 w-6 animate-spin text-primary" />
      </div>
    );
  }

  if (!data) return <p className="text-muted-foreground">No metrics available.</p>;

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-bold tracking-tight">Platform Operational Overview</h2>
        <p className="text-xs text-muted-foreground">
          Real-time metrics on tenant workspaces, subscription statuses, and active entitlements.
        </p>
      </div>

      {/* Top 4 Stat Cards */}
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <div className="rounded-xl border border-border bg-card p-4 shadow-xs">
          <div className="flex items-center justify-between">
            <span className="text-xs font-semibold text-muted-foreground">Total Restaurants</span>
            <Building2 className="h-4 w-4 text-primary" />
          </div>
          <div className="mt-2 text-2xl font-black">{data.totalRestaurants}</div>
          <div className="mt-1 flex gap-2 text-xs text-muted-foreground">
            <span className="text-success font-medium">{data.activeRestaurants} active</span>
            <span>•</span>
            <span className="text-destructive font-medium">{data.disabledRestaurants} disabled</span>
          </div>
        </div>

        <div className="rounded-xl border border-border bg-card p-4 shadow-xs">
          <div className="flex items-center justify-between">
            <span className="text-xs font-semibold text-muted-foreground">Active Subscriptions</span>
            <ShieldCheck className="h-4 w-4 text-success" />
          </div>
          <div className="mt-2 text-2xl font-black">{data.activeSubscriptions}</div>
          <div className="mt-1 flex gap-2 text-xs text-muted-foreground">
            <span className="text-warning font-medium">{data.trialSubscriptions} in trial</span>
            <span>•</span>
            <span>{data.totalSubscriptions} total</span>
          </div>
        </div>

        <div className="rounded-xl border border-border bg-card p-4 shadow-xs">
          <div className="flex items-center justify-between">
            <span className="text-xs font-semibold text-muted-foreground">Expiring in 30 Days</span>
            <Clock className="h-4 w-4 text-warning" />
          </div>
          <div className="mt-2 text-2xl font-black">{data.expiringSoonCount}</div>
          <p className="mt-1 text-xs text-muted-foreground">Require operator renewal or review</p>
        </div>

        <div className="rounded-xl border border-border bg-card p-4 shadow-xs">
          <div className="flex items-center justify-between">
            <span className="text-xs font-semibold text-muted-foreground">Total Active Staff</span>
            <Users className="h-4 w-4 text-primary" />
          </div>
          <div className="mt-2 text-2xl font-black">{data.totalEmployees}</div>
          <p className="mt-1 text-xs text-muted-foreground">Admins, Waiters & Kitchen users</p>
        </div>
      </div>

      {/* Subscription Breakdown & Quick Actions */}
      <div className="grid gap-6 md:grid-cols-2">
        <div className="rounded-xl border border-border bg-card p-5 shadow-xs">
          <h3 className="text-sm font-bold uppercase tracking-wider text-muted-foreground mb-4">
            Subscriptions by Plan Tier
          </h3>
          <div className="space-y-3">
            {["basic", "plus", "premium"].map((code) => {
              const count = data.subscriptionsByPlan?.[code] || 0;
              const total = data.totalSubscriptions || 1;
              const pct = Math.round((count / total) * 100);
              const label = code.charAt(0).toUpperCase() + code.slice(1);
              return (
                <div key={code} className="space-y-1">
                  <div className="flex justify-between text-xs font-medium">
                    <span className="capitalize">{label} Tier</span>
                    <span className="text-muted-foreground">
                      {count} restaurant(s) ({pct}%)
                    </span>
                  </div>
                  <div className="h-2 w-full rounded-full bg-secondary overflow-hidden">
                    <div
                      className={`h-full ${
                        code === "premium"
                          ? "bg-primary"
                          : code === "plus"
                          ? "bg-primary/70"
                          : "bg-muted-foreground/50"
                      }`}
                      style={{ width: `${pct}%` }}
                    />
                  </div>
                </div>
              );
            })}
          </div>
        </div>

        <div className="rounded-xl border border-border bg-card p-5 shadow-xs flex flex-col justify-between">
          <div>
            <h3 className="text-sm font-bold uppercase tracking-wider text-muted-foreground mb-2">
              Operator Quick Links
            </h3>
            <p className="text-xs text-muted-foreground mb-4">
              Direct shortcuts for provisioning, editing plan feature sets, or reviewing logs.
            </p>
            <div className="grid grid-cols-2 gap-2 text-xs">
              <Button
                variant="outline"
                className="justify-start gap-2 h-auto py-2.5"
                onClick={() => onNavigateTab("tenants")}
              >
                <Plus className="h-4 w-4 text-primary" />
                <span>Onboard Tenant</span>
              </Button>
              <Button
                variant="outline"
                className="justify-start gap-2 h-auto py-2.5"
                onClick={() => onNavigateTab("subscriptions")}
              >
                <ShieldCheck className="h-4 w-4 text-primary" />
                <span>Change Plans</span>
              </Button>
              <Button
                variant="outline"
                className="justify-start gap-2 h-auto py-2.5"
                onClick={() => onNavigateTab("plans")}
              >
                <Sliders className="h-4 w-4 text-primary" />
                <span>Configure Features</span>
              </Button>
              <Button
                variant="outline"
                className="justify-start gap-2 h-auto py-2.5"
                onClick={() => onNavigateTab("audit")}
              >
                <History className="h-4 w-4 text-primary" />
                <span>Audit Logs</span>
              </Button>
            </div>
          </div>

          <div className="mt-4 rounded-lg bg-secondary/30 p-3 text-xs text-muted-foreground">
            <span className="font-semibold text-foreground">Operational Rule:</span> Backfilled existing restaurants
            are active on Premium. Any manual plan change records an immutable audit log.
          </div>
        </div>
      </div>
    </div>
  );
}

/* ==========================================================================
   TAB 2: RESTAURANTS / TENANTS PANE + ONBOARDING
   ========================================================================== */
function TenantsPane({ onManageSub }: { onManageSub: () => void }) {
  const [tenants, setTenants] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("all");
  const [onboardOpen, setOnboardOpen] = useState(false);

  const fetchTenants = async () => {
    try {
      const data = await apiClient<any[]>("/sysadmin/tenants");
      setTenants(data);
    } catch (err: any) {
      toast.error(err.message || "Failed to load tenants");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchTenants();
  }, []);

  const toggleStatus = async (id: string) => {
    try {
      const res = await apiClient<any>(`/sysadmin/tenants/${id}/toggle-status`, {
        method: "PUT",
      });
      toast.success(res.message);
      setTenants((prev) => prev.map((t) => (t.id === id ? { ...t, isActive: res.isActive } : t)));
    } catch (err: any) {
      toast.error(err.message || "Failed to toggle status");
    }
  };

  const filteredTenants = useMemo(() => {
    return tenants.filter((t) => {
      const matchesSearch =
        t.name?.toLowerCase().includes(search.toLowerCase()) ||
        t.contactEmail?.toLowerCase().includes(search.toLowerCase());
      if (!matchesSearch) return false;
      if (statusFilter === "active" && !t.isActive) return false;
      if (statusFilter === "disabled" && t.isActive) return false;
      return true;
    });
  }, [tenants, search, statusFilter]);

  return (
    <div className="space-y-4">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
        <div>
          <h2 className="text-xl font-bold tracking-tight">Restaurant Tenants</h2>
          <p className="text-xs text-muted-foreground">
            View provisioned restaurants, toggle access, and manage subscriptions.
          </p>
        </div>
        <Button onClick={() => setOnboardOpen(true)} className="gap-1.5 text-xs font-semibold">
          <Plus className="h-4 w-4" /> Onboard Restaurant
        </Button>
      </div>

      {/* Filter and Search Bar */}
      <div className="flex flex-col sm:flex-row gap-2">
        <div className="relative flex-1">
          <Search className="absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" />
          <Input
            placeholder="Search restaurants by name or email..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="pl-9 text-xs"
          />
        </div>
        <div className="flex gap-1">
          <Button
            variant={statusFilter === "all" ? "default" : "outline"}
            size="sm"
            onClick={() => setStatusFilter("all")}
            className="text-xs"
          >
            All ({tenants.length})
          </Button>
          <Button
            variant={statusFilter === "active" ? "default" : "outline"}
            size="sm"
            onClick={() => setStatusFilter("active")}
            className="text-xs"
          >
            Active
          </Button>
          <Button
            variant={statusFilter === "disabled" ? "default" : "outline"}
            size="sm"
            onClick={() => setStatusFilter("disabled")}
            className="text-xs"
          >
            Disabled
          </Button>
        </div>
      </div>

      {/* Tenants Table */}
      <div className="rounded-xl border border-border bg-card shadow-xs overflow-hidden">
        {loading ? (
          <div className="p-8 text-center">
            <Loader2 className="h-6 w-6 animate-spin text-primary mx-auto" />
          </div>
        ) : filteredTenants.length === 0 ? (
          <div className="p-8 text-center text-xs text-muted-foreground">No restaurants found.</div>
        ) : (
          <div className="divide-y divide-border">
            {filteredTenants.map((t) => (
              <div key={t.id} className="p-4 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                <div className="space-y-1">
                  <div className="flex items-center gap-2">
                    <span className="font-bold text-sm text-foreground">{t.name}</span>
                    <Badge
                      variant="outline"
                      className={`text-[10px] font-bold uppercase ${
                        t.planCode === "premium"
                          ? "bg-primary/10 text-primary border-primary/30"
                          : t.planCode === "plus"
                          ? "bg-primary/5 text-primary border-primary/20"
                          : "bg-secondary text-muted-foreground"
                      }`}
                    >
                      {t.planCode || "Basic"}
                    </Badge>
                    <span
                      className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                        t.isActive ? "bg-success/10 text-success" : "bg-destructive/10 text-destructive"
                      }`}
                    >
                      {t.isActive ? "Active" : "Disabled"}
                    </span>
                  </div>
                  <p className="text-xs text-muted-foreground">{t.contactEmail}</p>
                  <div className="flex gap-4 text-[11px] text-muted-foreground pt-1">
                    <span>Created: {new Date(t.createdAt).toLocaleDateString()}</span>
                    <span>Outlets: {t.outletsCount || 1}</span>
                    <span>Active Users: {t.activeUsers?.length || 0}</span>
                  </div>
                </div>

                <div className="flex items-center gap-2">
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={onManageSub}
                    className="text-xs gap-1"
                  >
                    <ShieldCheck className="h-3.5 w-3.5 text-primary" />
                    Subscription
                  </Button>
                  <Button
                    variant={t.isActive ? "destructive" : "secondary"}
                    size="sm"
                    onClick={() => toggleStatus(t.id)}
                    className="text-xs"
                  >
                    {t.isActive ? "Disable" : "Enable"}
                  </Button>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Onboarding Dialog */}
      <OnboardingModal
        open={onboardOpen}
        onOpenChange={setOnboardOpen}
        onSuccess={() => {
          setOnboardOpen(false);
          fetchTenants();
        }}
      />
    </div>
  );
}

function OnboardingModal({
  open,
  onOpenChange,
  onSuccess,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSuccess: () => void;
}) {
  const [restaurantName, setRestaurantName] = useState("");
  const [adminFirstName, setAdminFirstName] = useState("");
  const [adminLastName, setAdminLastName] = useState("");
  const [adminEmail, setAdminEmail] = useState("");
  const [adminPassword, setAdminPassword] = useState("");
  const [busy, setBusy] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    try {
      await apiClient<any>("/sysadmin/onboard", {
        method: "POST",
        body: JSON.stringify({
          restaurantName,
          adminFirstName,
          adminLastName,
          adminEmail,
          adminPassword,
        }),
      });
      toast.success(`Restaurant "${restaurantName}" onboarded with Premium tier!`);
      onSuccess();
    } catch (err: any) {
      toast.error(err.message || "Failed to onboard restaurant");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader className="text-left">
          <DialogTitle className="text-lg font-bold">Onboard New Restaurant Tenant</DialogTitle>
          <DialogDescription className="text-xs text-muted-foreground">
            Provisions an isolated restaurant tenant, default main outlet, and an initial Administrator.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit} className="space-y-3 py-2 text-xs">
          <div>
            <label className="font-semibold text-foreground">Restaurant / Brand Name</label>
            <Input
              value={restaurantName}
              onChange={(e) => setRestaurantName(e.target.value)}
              placeholder="e.g. Malabar Spice Kitchen"
              required
              className="mt-1 text-xs"
            />
          </div>
          <div className="grid grid-cols-2 gap-2">
            <div>
              <label className="font-semibold text-foreground">Admin First Name</label>
              <Input
                value={adminFirstName}
                onChange={(e) => setAdminFirstName(e.target.value)}
                placeholder="Rizal"
                required
                className="mt-1 text-xs"
              />
            </div>
            <div>
              <label className="font-semibold text-foreground">Admin Last Name</label>
              <Input
                value={adminLastName}
                onChange={(e) => setAdminLastName(e.target.value)}
                placeholder="Ahmed"
                required
                className="mt-1 text-xs"
              />
            </div>
          </div>
          <div>
            <label className="font-semibold text-foreground">Admin Email (Login ID)</label>
            <Input
              type="email"
              value={adminEmail}
              onChange={(e) => setAdminEmail(e.target.value)}
              placeholder="admin@malabarspice.com"
              required
              className="mt-1 text-xs"
            />
          </div>
          <div>
            <label className="font-semibold text-foreground">Temporary Password</label>
            <Input
              type="text"
              value={adminPassword}
              onChange={(e) => setAdminPassword(e.target.value)}
              placeholder="At least 6 characters"
              minLength={6}
              required
              className="mt-1 text-xs"
            />
          </div>

          <DialogFooter className="pt-3">
            <Button variant="outline" type="button" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button disabled={busy} type="submit">
              {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {busy ? "Provisioning..." : "Provision Tenant"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

/* ==========================================================================
   TAB 3: PLANS & FEATURES PANE (Dependency Check & Packaging)
   ========================================================================== */
function PlansPane() {
  const [plans, setPlans] = useState<PlanItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [editPlan, setEditPlan] = useState<PlanItem | null>(null);

  const fetchPlans = async () => {
    try {
      const data = await getSysAdminPlans();
      setPlans(data);
    } catch (err: any) {
      toast.error(err.message || "Failed to load plans");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchPlans();
  }, []);

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-bold tracking-tight">Plan Packaging & Feature Entitlements</h2>
        <p className="text-xs text-muted-foreground">
          Define capabilities enabled for each subscription tier. Feature dependencies (such as purchasing requiring
          inventory) are strictly enforced.
        </p>
      </div>

      {loading ? (
        <div className="p-8 text-center">
          <Loader2 className="h-6 w-6 animate-spin text-primary mx-auto" />
        </div>
      ) : (
        <div className="grid gap-6 md:grid-cols-3">
          {plans.map((p) => (
            <div key={p.id} className="rounded-xl border border-border bg-card p-5 shadow-xs flex flex-col justify-between">
              <div>
                <div className="flex items-center justify-between">
                  <h3 className="font-bold text-lg capitalize">{p.name}</h3>
                  <Badge variant="outline" className="uppercase text-[10px] font-bold">
                    {p.code}
                  </Badge>
                </div>
                <div className="mt-2 text-2xl font-black">
                  ₹{p.monthlyPriceInr.toLocaleString("en-IN")}
                  <span className="text-xs font-normal text-muted-foreground">/mo</span>
                </div>
                <p className="mt-2 text-xs text-muted-foreground">{p.description}</p>

                <div className="mt-4 grid grid-cols-2 gap-2 text-xs border-y border-border/60 py-2">
                  <div>
                    <span className="text-muted-foreground">Max Outlets:</span>{" "}
                    <span className="font-semibold">{p.maxOutlets}</span>
                  </div>
                  <div>
                    <span className="text-muted-foreground">Max Tables:</span>{" "}
                    <span className="font-semibold">{p.maxTables}</span>
                  </div>
                </div>

                <div className="mt-4 space-y-2">
                  <p className="text-[11px] font-bold uppercase tracking-wider text-muted-foreground">
                    Included Capabilities ({p.features.length})
                  </p>
                  <div className="max-h-56 overflow-y-auto space-y-1 rounded bg-secondary/30 p-2 text-xs">
                    {p.features.map((f) => (
                      <div key={f} className="flex items-center gap-1.5 font-medium">
                        <CheckCircle2 className="h-3 w-3 text-primary shrink-0" />
                        <span className="capitalize">{f.replace(/_/g, " ")}</span>
                      </div>
                    ))}
                  </div>
                </div>
              </div>

              <div className="mt-6 pt-2">
                <Button
                  variant="outline"
                  className="w-full text-xs font-semibold"
                  onClick={() => setEditPlan(p)}
                >
                  Configure Features
                </Button>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Edit Plan Features Modal */}
      {editPlan && (
        <EditPlanFeaturesModal
          plan={editPlan}
          open={!!editPlan}
          onOpenChange={(open) => !open && setEditPlan(null)}
          onSuccess={() => {
            setEditPlan(null);
            fetchPlans();
          }}
        />
      )}
    </div>
  );
}

function EditPlanFeaturesModal({
  plan,
  open,
  onOpenChange,
  onSuccess,
}: {
  plan: PlanItem;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSuccess: () => void;
}) {
  const [selectedFeatures, setSelectedFeatures] = useState<string[]>(plan.features);
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);

  const ALL_POSSIBLE_FEATURES = [
    { key: "qr_ordering", label: "QR Ordering", group: "Basic" },
    { key: "kitchen_display", label: "Kitchen Display Queue", group: "Basic" },
    { key: "standard_billing", label: "Standard Cash/Card Billing", group: "Basic" },
    { key: "basic_reports", label: "Basic Daily Reports", group: "Basic" },
    { key: "menu_management", label: "Menu & Categories", group: "Basic" },
    { key: "tables_qr", label: "Tables & QR Generation", group: "Basic" },
    { key: "staff_ordering", label: "Staff & Waiter Table Ordering", group: "Plus" },
    { key: "dining_groups", label: "Dining Groups & Merging", group: "Plus" },
    { key: "split_billing", label: "Split Billing & Mixed Payments", group: "Plus" },
    { key: "mixed_payments", label: "Mixed Payments (Cash+UPI)", group: "Plus" },
    { key: "table_transfers", label: "Table Transfers & Recovery", group: "Plus" },
    { key: "advanced_payment_reports", label: "Advanced Payment Reports", group: "Plus" },
    { key: "inventory_core", label: "Ingredient Stock & Ledger", group: "Premium" },
    { key: "recipes_costing", label: "Recipes & Biryani Yield Costing", group: "Premium" },
    { key: "stocktakes", label: "Daily Stocktakes & Variances", group: "Premium" },
    { key: "purchasing", label: "Purchase Orders & Receiving", group: "Premium" },
    { key: "lots_expiry", label: "Stock Lots & Expiry Tracking", group: "Premium" },
    { key: "preparations", label: "Semi-finished Prep Recipes", group: "Premium" },
    { key: "outlet_transfers", label: "Multi-outlet Stock Transfers", group: "Premium" },
    { key: "stock_profit_reports", label: "Gross Profit & Stock Valuation", group: "Premium" },
    { key: "auto_availability", label: "Auto 86 Dish on Depletion", group: "Premium" },
  ];

  const toggleFeature = (key: string) => {
    if (selectedFeatures.includes(key)) {
      setSelectedFeatures((prev) => prev.filter((f) => f !== key));
    } else {
      setSelectedFeatures((prev) => [...prev, key]);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!reason.trim()) {
      toast.error("A reason is required to modify plan features for audit compliance.");
      return;
    }

    setBusy(true);
    try {
      await updatePlanFeatures(plan.id, selectedFeatures, reason);
      toast.success(`Features updated for plan ${plan.name}`);
      onSuccess();
    } catch (err: any) {
      toast.error(err.message || "Failed to update plan features");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-xl">
        <DialogHeader className="text-left">
          <DialogTitle className="text-lg font-bold">
            Configure Features: <span className="capitalize">{plan.name}</span> Tier
          </DialogTitle>
          <DialogDescription className="text-xs text-muted-foreground">
            Select or remove capabilities. Enforces architectural prerequisite validation on submit.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit} className="space-y-4 py-2">
          <div className="max-h-72 overflow-y-auto space-y-2 rounded border border-border p-3 text-xs">
            {ALL_POSSIBLE_FEATURES.map((item) => {
              const checked = selectedFeatures.includes(item.key);
              return (
                <label
                  key={item.key}
                  className="flex items-center justify-between rounded p-1.5 hover:bg-secondary/40 cursor-pointer"
                >
                  <div className="flex items-center gap-2">
                    <input
                      type="checkbox"
                      checked={checked}
                      onChange={() => toggleFeature(item.key)}
                      className="rounded border-border text-primary"
                    />
                    <span className="font-medium text-foreground">{item.label}</span>
                  </div>
                  <Badge variant="outline" className="text-[10px]">
                    {item.group}
                  </Badge>
                </label>
              );
            })}
          </div>

          <div>
            <label className="text-xs font-semibold text-foreground">Change Reason (Audit Log)</label>
            <Input
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="e.g. Added multi-outlet transfers entitlement to tier"
              required
              className="mt-1 text-xs"
            />
          </div>

          <DialogFooter className="pt-2">
            <Button variant="outline" type="button" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button disabled={busy} type="submit">
              {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {busy ? "Saving..." : "Save Entitlements"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

/* ==========================================================================
   TAB 4: SUBSCRIPTIONS PANE (Change Plan with Live Preview Gained/Lost)
   ========================================================================== */
function SubscriptionsPane() {
  const [subscriptions, setSubscriptions] = useState<SubscriptionItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [selectedSub, setSelectedSub] = useState<SubscriptionItem | null>(null);

  const fetchSubscriptions = async () => {
    try {
      const data = await getSysAdminSubscriptions();
      setSubscriptions(data);
    } catch (err: any) {
      toast.error(err.message || "Failed to load subscriptions");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchSubscriptions();
  }, []);

  return (
    <div className="space-y-4">
      <div className="flex justify-between items-center">
        <div>
          <h2 className="text-xl font-bold tracking-tight">Active Restaurant Subscriptions</h2>
          <p className="text-xs text-muted-foreground">
            Manage plans, upgrade or downgrade tiers with preview of gained/lost capabilities, and track validity.
          </p>
        </div>
      </div>

      <div className="rounded-xl border border-border bg-card shadow-xs overflow-hidden">
        {loading ? (
          <div className="p-8 text-center">
            <Loader2 className="h-6 w-6 animate-spin text-primary mx-auto" />
          </div>
        ) : subscriptions.length === 0 ? (
          <div className="p-8 text-center text-xs text-muted-foreground">No subscriptions recorded.</div>
        ) : (
          <div className="divide-y divide-border">
            {subscriptions.map((sub) => (
              <div key={sub.id} className="p-4 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                <div className="space-y-1">
                  <div className="flex items-center gap-2">
                    <span className="font-bold text-sm text-foreground">{sub.restaurantName}</span>
                    <Badge
                      variant="outline"
                      className={`text-[10px] font-bold uppercase ${
                        sub.planCode === "premium"
                          ? "bg-primary/10 text-primary border-primary/30"
                          : sub.planCode === "plus"
                          ? "bg-primary/5 text-primary border-primary/20"
                          : "bg-secondary text-muted-foreground"
                      }`}
                    >
                      {sub.planName}
                    </Badge>
                    <Badge
                      variant="outline"
                      className={`text-[10px] font-semibold capitalize ${
                        sub.status === "Active"
                          ? "bg-success/15 text-success border-success/30"
                          : sub.status === "Trial"
                          ? "bg-warning/15 text-warning border-warning/30"
                          : "bg-destructive/15 text-destructive border-destructive/30"
                      }`}
                    >
                      {sub.status}
                    </Badge>
                  </div>
                  <div className="flex flex-wrap gap-4 text-xs text-muted-foreground pt-1">
                    <span>Started: {new Date(sub.startDateUtc).toLocaleDateString()}</span>
                    <span>
                      End Date:{" "}
                      {sub.endDateUtc ? new Date(sub.endDateUtc).toLocaleDateString() : "Continuous / Auto-renew"}
                    </span>
                    <span>Auto-renew: {sub.autoRenew ? "Yes" : "No"}</span>
                  </div>
                </div>

                <div className="flex items-center gap-2">
                  <Button
                    variant="outline"
                    size="sm"
                    className="text-xs font-semibold gap-1.5"
                    onClick={() => setSelectedSub(sub)}
                  >
                    <Sliders className="h-3.5 w-3.5 text-primary" />
                    Change Plan / Status
                  </Button>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Change Plan Modal with Live Preview */}
      {selectedSub && (
        <ChangeSubscriptionModal
          subscription={selectedSub}
          open={!!selectedSub}
          onOpenChange={(open) => !open && setSelectedSub(null)}
          onSuccess={() => {
            setSelectedSub(null);
            fetchSubscriptions();
          }}
        />
      )}
    </div>
  );
}

function ChangeSubscriptionModal({
  subscription,
  open,
  onOpenChange,
  onSuccess,
}: {
  subscription: SubscriptionItem;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSuccess: () => void;
}) {
  const [targetPlanCode, setTargetPlanCode] = useState(subscription.planCode);
  const [newStatus, setNewStatus] = useState(subscription.status);
  const [endDate, setEndDate] = useState(
    subscription.endDateUtc ? subscription.endDateUtc.split("T")[0] : ""
  );
  const [reason, setReason] = useState("");
  const [preview, setPreview] = useState<PlanChangePreview | null>(null);
  const [previewLoading, setPreviewLoading] = useState(false);
  const [busy, setBusy] = useState(false);

  // Live preview when targetPlanCode changes
  useEffect(() => {
    if (targetPlanCode && targetPlanCode !== subscription.planCode) {
      setPreviewLoading(true);
      previewSysAdminPlanChange(subscription.restaurantId, targetPlanCode)
        .then(setPreview)
        .catch(() => setPreview(null))
        .finally(() => setPreviewLoading(false));
    } else {
      setPreview(null);
    }
  }, [targetPlanCode, subscription.restaurantId, subscription.planCode]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!reason.trim()) {
      toast.error("Please provide a reason for this subscription change.");
      return;
    }

    setBusy(true);
    try {
      await changeSubscription(subscription.restaurantId, {
        targetPlanCode: targetPlanCode !== subscription.planCode ? targetPlanCode : undefined,
        newStatus: newStatus !== subscription.status ? newStatus : undefined,
        endDateUtc: endDate ? new Date(endDate).toISOString() : undefined,
        reason,
      });
      toast.success("Subscription updated successfully!");
      onSuccess();
    } catch (err: any) {
      toast.error(err.message || "Failed to update subscription");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader className="text-left">
          <DialogTitle className="text-lg font-bold">
            Modify Subscription: {subscription.restaurantName}
          </DialogTitle>
          <DialogDescription className="text-xs text-muted-foreground">
            Current Tier: <span className="font-semibold uppercase text-foreground">{subscription.planCode}</span> ({subscription.status})
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit} className="space-y-4 py-2 text-xs">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="font-semibold text-foreground">Target Plan</label>
              <select
                value={targetPlanCode}
                onChange={(e) => setTargetPlanCode(e.target.value)}
                className="w-full mt-1 rounded-md border border-input bg-background px-3 py-2 text-xs"
              >
                <option value="basic">Basic (QR Menu & Kitchen)</option>
                <option value="plus">Plus (Waiter Orders & Split Bills)</option>
                <option value="premium">Premium (Inventory & Recipes)</option>
              </select>
            </div>
            <div>
              <label className="font-semibold text-foreground">Subscription Status</label>
              <select
                value={newStatus}
                onChange={(e) => setNewStatus(e.target.value)}
                className="w-full mt-1 rounded-md border border-input bg-background px-3 py-2 text-xs"
              >
                <option value="Active">Active</option>
                <option value="Trial">Trial</option>
                <option value="Suspended">Suspended</option>
                <option value="Expired">Expired</option>
              </select>
            </div>
          </div>

          <div>
            <label className="font-semibold text-foreground">Expiration Date (Optional)</label>
            <Input
              type="date"
              value={endDate}
              onChange={(e) => setEndDate(e.target.value)}
              className="mt-1 text-xs"
            />
          </div>

          {/* CAPABILITIES GAINED / LOST PREVIEW */}
          {previewLoading && (
            <div className="flex items-center gap-2 p-3 text-muted-foreground bg-secondary/30 rounded-lg">
              <Loader2 className="h-4 w-4 animate-spin text-primary" /> Calculating capability delta...
            </div>
          )}

          {preview && !previewLoading && (
            <div className="space-y-2 rounded-lg border border-border bg-secondary/20 p-3">
              <p className="text-[11px] font-bold uppercase tracking-wider text-muted-foreground">
                Capability Impact Preview
              </p>

              {preview.capabilitiesGained?.length > 0 && (
                <div>
                  <span className="text-[11px] font-semibold text-success flex items-center gap-1">
                    <CheckCircle2 className="h-3 w-3" /> Capabilities Gained ({preview.capabilitiesGained.length}):
                  </span>
                  <div className="mt-1 flex flex-wrap gap-1">
                    {preview.capabilitiesGained.map((cap) => (
                      <span key={cap} className="rounded bg-success/10 px-1.5 py-0.5 text-[10px] font-medium text-success">
                        +{cap}
                      </span>
                    ))}
                  </div>
                </div>
              )}

              {preview.capabilitiesLost?.length > 0 && (
                <div className="mt-2">
                  <span className="text-[11px] font-semibold text-destructive flex items-center gap-1">
                    <XCircle className="h-3 w-3" /> Capabilities Lost ({preview.capabilitiesLost.length}):
                  </span>
                  <div className="mt-1 flex flex-wrap gap-1">
                    {preview.capabilitiesLost.map((cap) => (
                      <span key={cap} className="rounded bg-destructive/10 px-1.5 py-0.5 text-[10px] font-medium text-destructive">
                        -{cap}
                      </span>
                    ))}
                  </div>
                </div>
              )}

              {preview.capabilitiesGained?.length === 0 && preview.capabilitiesLost?.length === 0 && (
                <p className="text-[11px] text-muted-foreground">No capability changes between these configurations.</p>
              )}
            </div>
          )}

          <div>
            <label className="font-semibold text-foreground">Reason for Change (Audit Requirement)</label>
            <Input
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="e.g. Upgraded to Premium following client trial approval"
              required
              className="mt-1 text-xs"
            />
          </div>

          <DialogFooter className="pt-2">
            <Button variant="outline" type="button" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button disabled={busy} type="submit">
              {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {busy ? "Applying..." : "Confirm & Record Audit"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

/* ==========================================================================
   TAB 5: AUDIT LOG PANE
   ========================================================================== */
function AuditLogPane() {
  const [logs, setLogs] = useState<SubscriptionAuditLogItem[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    getSysAdminAuditLogs(undefined, 100)
      .then(setLogs)
      .catch((err) => toast.error(err.message || "Failed to load audit logs"))
      .finally(() => setLoading(false));
  }, []);

  return (
    <div className="space-y-4">
      <div>
        <h2 className="text-xl font-bold tracking-tight">Subscription Audit Trail</h2>
        <p className="text-xs text-muted-foreground">
          Immutable ledger of every plan upgrade, downgrade, status adjustment, and feature set modification.
        </p>
      </div>

      <div className="rounded-xl border border-border bg-card shadow-xs overflow-hidden">
        {loading ? (
          <div className="p-8 text-center">
            <Loader2 className="h-6 w-6 animate-spin text-primary mx-auto" />
          </div>
        ) : logs.length === 0 ? (
          <div className="p-8 text-center text-xs text-muted-foreground">No audit entries recorded yet.</div>
        ) : (
          <div className="divide-y divide-border text-xs">
            {logs.map((log) => (
              <div key={log.id} className="p-4 flex flex-col sm:flex-row sm:items-center justify-between gap-2">
                <div className="space-y-1">
                  <div className="flex items-center gap-2">
                    <span className="font-bold text-foreground">{log.restaurantName}</span>
                    <Badge variant="outline" className="text-[10px] font-mono">
                      {log.action}
                    </Badge>
                    {log.fromPlanCode && log.toPlanCode && (
                      <span className="text-[11px] text-muted-foreground flex items-center gap-1">
                        <span className="font-semibold">{log.fromPlanCode}</span>
                        <ArrowRight className="h-3 w-3" />
                        <span className="font-semibold text-primary">{log.toPlanCode}</span>
                      </span>
                    )}
                  </div>
                  <p className="text-foreground">
                    <span className="text-muted-foreground font-semibold">Reason:</span> {log.reason}
                  </p>
                  <p className="text-[11px] text-muted-foreground">
                    Actor: <span className="font-mono">{log.actorEmail}</span> • Time:{" "}
                    {new Date(log.timestampUtc).toLocaleString("en-IN", { timeZone: "Asia/Kolkata" })} IST
                  </p>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
