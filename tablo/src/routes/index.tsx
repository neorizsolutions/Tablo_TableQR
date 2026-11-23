import { createFileRoute, Link, useRouter } from "@tanstack/react-router";
import { useEffect, useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  ArrowRight,
  Bell,
  CheckCircle2,
  ChefHat,
  Languages,
  QrCode,
  Smartphone,
  Sparkles,
  TimerReset,
  ToggleRight,
  Utensils,
  ShieldCheck,
  Check,
  Package,
  Layers,
  Receipt,
  Users,
  Flame,
  PackageCheck,
  AlertCircle,
  ShoppingCart,
  Clock,
  Plus,
  RotateCw,
  TrendingUp,
  UtensilsCrossed,
  ArrowUpRight,
} from "lucide-react";
import { SiteHeader } from "@/components/site-header";
import { AppLayout } from "@/components/app-layout";
import { useAuth } from "@/hooks/use-auth";
import { useEffectiveAccess } from "@/hooks/use-effective-access";
import { CAPABILITIES } from "@/config/navigation";
import { publicPricingPlans } from "@/config/public-pricing";
import { useOrders } from "@/hooks/use-orders";
import { useActiveServiceRequests } from "@/hooks/use-service-requests";
import { getServiceTables, type ServiceTable } from "@/services/service-desk";
import { getIngredients, type Ingredient } from "@/services/inventory";
import { formatINR, relativeTime } from "@/lib/mock-data";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import heroImg from "@/assets/hero.jpg";

export const Route = createFileRoute("/")({
  head: () => ({
    meta: [
      { title: "Tablo — Scan. Order. Serve. QR ordering for restaurants" },
      {
        name: "description",
        content:
          "Tablo lets diners scan, browse and order from their phone. Live order updates, menu control and faster service for modern restaurants.",
      },
      { property: "og:title", content: "Tablo — Scan. Order. Serve." },
      {
        property: "og:description",
        content: "QR ordering, live kitchen updates and effortless menu control for restaurants.",
      },
    ],
  }),
  component: RootIndexPage,
});

function RootIndexPage() {
  const { user, isStaff, loading } = useAuth();

  if (loading) return null;

  // If user is authenticated as restaurant staff/admin/owner, show Executive Operational Overview
  if (user && (isStaff || user.role === "Owner")) {
    return <OperationalOverview />;
  }

  // Otherwise, show public landing page
  return <LandingPage />;
}

/* ==========================================================================
   AUTHENTICATED RESTAURANT OPERATIONAL OVERVIEW
   Surfaces active orders, kitchen delays, bill requests, and low stock
   ========================================================================== */
function OperationalOverview() {
  const { user } = useAuth();
  const { hasCapability } = useEffectiveAccess();
  const showStockAlerts = (user?.role === "Admin" || user?.role === "Owner") &&
    hasCapability(CAPABILITIES.InventoryCore);
  const restaurantId = user?.restaurantId || "22222222-0000-0000-0000-000000000001";

  const { data: orders = [], refetch: refetchOrders } = useOrders(restaurantId);
  const { data: serviceRequests = [] } = useActiveServiceRequests(restaurantId);

  const tablesQuery = useQuery({
    queryKey: ["serviceTables", restaurantId],
    queryFn: () => getServiceTables(restaurantId),
    refetchInterval: 15000,
  });

  const ingredientsQuery = useQuery({
    queryKey: ["ingredients", restaurantId],
    queryFn: () => getIngredients(restaurantId),
    refetchInterval: 30000,
    enabled: showStockAlerts,
  });

  const tables = tablesQuery.data || [];
  const ingredients = ingredientsQuery.data || [];

  // Metrics Calculations
  const activeOrders = useMemo(
    () => orders.filter((o) => o.status !== "served" && o.status !== "cancelled" && o.status !== "rejected"),
    [orders]
  );

  const preparingOrders = useMemo(
    () => orders.filter((o) => o.status === "preparing"),
    [orders]
  );

  // Delayed orders (>15 minutes since placed)
  const delayedOrders = useMemo(() => {
    const now = Date.now();
    return preparingOrders.filter((o) => {
      const placedTime = new Date(o.placed_at).getTime();
      return now - placedTime > 15 * 60 * 1000;
    });
  }, [preparingOrders]);

  const billRequests = useMemo(
    () => serviceRequests.filter((r) => r.type === "RequestBill"),
    [serviceRequests]
  );

  const lowStockIngredients = useMemo(
    () => ingredients.filter((ing) => ing.isLowStock || (ing.lowStockThreshold != null && ing.onHandQuantity <= ing.lowStockThreshold)),
    [ingredients]
  );

  const totalFloorBalance = useMemo(
    () => tables.reduce((acc, t) => acc + (t.openBillTotal || 0), 0),
    [tables]
  );

  return (
    <AppLayout>
      <div className="min-h-screen bg-secondary/40">
        <main className="mx-auto max-w-7xl px-4 py-8 sm:px-6 space-y-6">
          {/* Header */}
          <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 pb-2 border-b border-border/60">
            <div>
              <div className="flex items-center gap-2">
                <span className="text-xs font-semibold uppercase tracking-wider text-primary">
                  Operations Overview
                </span>
                <span className="h-1.5 w-1.5 rounded-full bg-success" />
                <span className="text-[11px] font-semibold text-muted-foreground">Live Shift · IST</span>
              </div>
              <h1 className="mt-1 font-display text-2xl sm:text-3xl font-bold">
                Tablo Heritage Restaurant
              </h1>
              <p className="text-xs text-muted-foreground mt-0.5">
                Executive dashboard tracking active table orders, kitchen delays, guest requests, and ingredient safety stock.
              </p>
            </div>

            {/* Quick Action Toolbar */}
            <div className="flex flex-wrap items-center gap-2">
              <Link
                to="/dashboard"
                search={{ tab: "order" }}
                className="inline-flex items-center gap-1.5 rounded-full bg-primary px-4 py-2 text-xs font-semibold text-primary-foreground shadow-soft hover:bg-primary/95 transition"
              >
                <Plus className="h-3.5 w-3.5" /> Take Staff Order
              </Link>
              <Link
                to="/dashboard"
                search={{ tab: "kitchen" }}
                className="inline-flex items-center gap-1.5 rounded-full border border-border bg-card px-4 py-2 text-xs font-semibold shadow-soft hover:bg-secondary transition"
              >
                <ChefHat className="h-3.5 w-3.5 text-primary" /> Kitchen Queue
              </Link>
              <Link
                to="/inventory"
                search={{ tab: "ingredients" }}
                className="inline-flex items-center gap-1.5 rounded-full border border-border bg-card px-4 py-2 text-xs font-semibold shadow-soft hover:bg-secondary transition"
              >
                <Package className="h-3.5 w-3.5 text-primary" /> Stock & Recipes
              </Link>
              <Link
                to="/reports"
                className="inline-flex items-center gap-1.5 rounded-full border border-border bg-card px-4 py-2 text-xs font-semibold shadow-soft hover:bg-secondary transition"
              >
                <Receipt className="h-3.5 w-3.5 text-primary" /> Payment Reports
              </Link>
            </div>
          </div>

          {/* 4 Primary Operational Metric Cards with Direct Filtered Links */}
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {/* 1. Active Floor Orders */}
            <Link
              to="/dashboard"
              search={{ tab: "floor" }}
              className="group rounded-2xl border border-border bg-card p-5 shadow-xs hover:border-primary/50 hover:shadow-soft transition flex flex-col justify-between"
            >
              <div>
                <div className="flex items-center justify-between">
                  <span className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">
                    Active Floor Orders
                  </span>
                  <span className="p-2 rounded-xl bg-primary/10 text-primary group-hover:bg-primary group-hover:text-primary-foreground transition">
                    <UtensilsCrossed className="h-4 w-4" />
                  </span>
                </div>
                <div className="mt-3 text-3xl font-black font-display text-foreground">
                  {activeOrders.length}
                </div>
                <p className="mt-1 text-xs text-muted-foreground">
                  Open floor balance: <strong className="text-foreground">{formatINR(totalFloorBalance)}</strong>
                </p>
              </div>
              <div className="mt-4 pt-2 border-t border-border/50 flex items-center justify-between text-[11px] font-semibold text-primary">
                <span>View Floor & Billing</span>
                <ArrowUpRight className="h-3.5 w-3.5 transition group-hover:translate-x-0.5" />
              </div>
            </Link>

            {/* 2. Kitchen Delays & Queue */}
            <Link
              to="/dashboard"
              search={{ tab: "kitchen" }}
              className={`group rounded-2xl border p-5 shadow-xs transition flex flex-col justify-between ${
                delayedOrders.length > 0
                  ? "border-destructive/40 bg-destructive/5 hover:border-destructive"
                  : "border-border bg-card hover:border-primary/50 hover:shadow-soft"
              }`}
            >
              <div>
                <div className="flex items-center justify-between">
                  <span className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">
                    Kitchen Queue & Delays
                  </span>
                  <span
                    className={`p-2 rounded-xl transition ${
                      delayedOrders.length > 0
                        ? "bg-destructive text-destructive-foreground"
                        : "bg-amber-500/10 text-amber-700"
                    }`}
                  >
                    <Flame className="h-4 w-4" />
                  </span>
                </div>
                <div className="mt-3 text-3xl font-black font-display text-foreground">
                  {preparingOrders.length}
                </div>
                <div className="mt-1 text-xs">
                  {delayedOrders.length > 0 ? (
                    <span className="font-bold text-destructive flex items-center gap-1">
                      <AlertCircle className="h-3 w-3" /> {delayedOrders.length} ticket(s) &gt;15 min delay
                    </span>
                  ) : (
                    <span className="text-success font-medium">All tickets on schedule</span>
                  )}
                </div>
              </div>
              <div className="mt-4 pt-2 border-t border-border/50 flex items-center justify-between text-[11px] font-semibold text-primary">
                <span>Open Kitchen Queue</span>
                <ArrowUpRight className="h-3.5 w-3.5 transition group-hover:translate-x-0.5" />
              </div>
            </Link>

            {/* 3. Service Calls & Bill Requests */}
            <Link
              to="/dashboard"
              search={{ tab: "floor" }}
              className={`group rounded-2xl border p-5 shadow-xs transition flex flex-col justify-between ${
                serviceRequests.length > 0
                  ? "border-amber-400 bg-amber-500/5 hover:border-amber-500"
                  : "border-border bg-card hover:border-primary/50 hover:shadow-soft"
              }`}
            >
              <div>
                <div className="flex items-center justify-between">
                  <span className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">
                    Guest Calls & Bills
                  </span>
                  <span className="p-2 rounded-xl bg-amber-500/10 text-amber-700 group-hover:bg-amber-600 group-hover:text-white transition">
                    <Bell className="h-4 w-4" />
                  </span>
                </div>
                <div className="mt-3 text-3xl font-black font-display text-foreground">
                  {serviceRequests.length}
                </div>
                <div className="mt-1 text-xs text-muted-foreground">
                  {billRequests.length > 0 ? (
                    <span className="font-semibold text-amber-700">
                      {billRequests.length} guest(s) requested check
                    </span>
                  ) : (
                    <span>No unhandled table requests</span>
                  )}
                </div>
              </div>
              <div className="mt-4 pt-2 border-t border-border/50 flex items-center justify-between text-[11px] font-semibold text-primary">
                <span>Manage Table Requests</span>
                <ArrowUpRight className="h-3.5 w-3.5 transition group-hover:translate-x-0.5" />
              </div>
            </Link>

            {/* 4. Low Stock Ingredients Alert */}
            {showStockAlerts && <Link
              to="/inventory"
              search={{ tab: "ingredients" }}
              className={`group rounded-2xl border p-5 shadow-xs transition flex flex-col justify-between ${
                lowStockIngredients.length > 0
                  ? "border-warning/40 bg-warning/5 hover:border-warning"
                  : "border-border bg-card hover:border-primary/50 hover:shadow-soft"
              }`}
            >
              <div>
                <div className="flex items-center justify-between">
                  <span className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">
                    Low Stock Alerts
                  </span>
                  <span className="p-2 rounded-xl bg-destructive/10 text-destructive group-hover:bg-destructive group-hover:text-white transition">
                    <Package className="h-4 w-4" />
                  </span>
                </div>
                <div className="mt-3 text-3xl font-black font-display text-foreground">
                  {lowStockIngredients.length}
                </div>
                <div className="mt-1 text-xs text-muted-foreground">
                  {lowStockIngredients.length > 0 ? (
                    <span className="font-bold text-destructive">
                      Requires reorder / PO issue
                    </span>
                  ) : (
                    <span className="text-success font-medium">All ingredients in stock</span>
                  )}
                </div>
              </div>
              <div className="mt-4 pt-2 border-t border-border/50 flex items-center justify-between text-[11px] font-semibold text-primary">
                <span>Review Stock & POs</span>
                <ArrowUpRight className="h-3.5 w-3.5 transition group-hover:translate-x-0.5" />
              </div>
            </Link>}
          </div>

          {/* Quick Floor Status Glance (10 Tables) */}
          <div className="rounded-2xl border border-border bg-card p-5 shadow-xs space-y-4">
            <div className="flex items-center justify-between">
              <div>
                <h2 className="text-base font-bold text-foreground">Live Floor Status Glance</h2>
                <p className="text-xs text-muted-foreground">
                  Tap any table to start taking an order or manage dining groups.
                </p>
              </div>
              <Link
                to="/dashboard"
                search={{ tab: "order" }}
                className="text-xs font-semibold text-primary hover:underline flex items-center gap-1"
              >
                Go to Staff Order Taking <ArrowRight className="h-3 w-3" />
              </Link>
            </div>

            <div className="grid grid-cols-2 sm:grid-cols-5 md:grid-cols-5 lg:grid-cols-10 gap-2.5">
              {tables.map((t) => {
                const isOccupied = t.status === "Occupied" || (t.activeOrdersCount && t.activeOrdersCount > 0);
                const isBillReq = t.status === "Bill Requested";

                return (
                  <Link
                    key={t.id}
                    to="/dashboard"
                    search={{ tab: "order", table: t.id }}
                    className={`p-3 rounded-xl border text-center transition hover:scale-[1.03] shadow-2xs ${
                      isBillReq
                        ? "border-amber-400 bg-amber-500/10 text-amber-900"
                        : isOccupied
                        ? "border-primary/40 bg-primary/10 text-primary"
                        : "border-border bg-secondary/30 text-foreground hover:bg-secondary"
                    }`}
                  >
                    <div className="font-display font-black text-lg">#{t.tableNumber}</div>
                    <div className="text-[10px] font-bold uppercase mt-0.5">
                      {isBillReq ? "Bill" : isOccupied ? "Active" : "Open"}
                    </div>
                  </Link>
                );
              })}
            </div>
          </div>
        </main>
      </div>
    </AppLayout>
  );
}

const features = [
  {
    icon: QrCode,
    title: "Table QR Ordering",
    desc: "Branded table QR codes open your menu instantly — zero app download or signup required.",
  },
  {
    icon: Bell,
    title: "Live Order Updates",
    desc: "Tickets fly to kitchen screens the second guests order. Real-time status flow from New to Served.",
  },
  {
    icon: ToggleRight,
    title: "Menu Availability & 86ing",
    desc: "Mark items out of stock in one tap. Diners and floor staff see live availability instantly.",
  },
  {
    icon: Package,
    title: "Ingredient Inventory & Costing",
    desc: "Full Thalassery biryani recipe costing, automatic stock deductions on settlement, and stocktakes.",
  },
];

const steps = [
  { n: "01", title: "Guest scans the QR", desc: "A frictionless menu opens on their phone in under a second." },
  { n: "02", title: "Order goes to kitchen", desc: "Staff get a live ticket with table, items, notes and timing." },
  { n: "03", title: "Status updates flow back", desc: "Guests watch their order move from preparing to served." },
];

const benefits = [
  "Reduce wait times during peak hours",
  "Free up servers for hospitality, not manual order-taking",
  "Boost average ticket size with rich menu visuals",
  "Eliminate paper menus and reprinting costs",
  "Bilingual menus — English & Malayalam ready",
  "Real-time analytics on best-selling dishes and gross profits",
];

function LandingPage() {
  const { user, roles, loading } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (!loading && user) {
      if (!roles.includes("Admin") && !roles.includes("Owner")) {
        router.navigate({ to: "/dashboard", replace: true });
      }
    }
  }, [user, roles, loading, router]);

  if (loading) return null;

  return (
    <div className="min-h-screen bg-background text-foreground scroll-smooth">
      <SiteHeader />

      {/* OVERVIEW SECTION */}
      <section id="overview" className="relative overflow-hidden pt-6 md:pt-12">
        <div className="absolute inset-0 -z-10 bg-gradient-hero" />
        <div className="mx-auto grid max-w-7xl gap-12 px-4 pb-20 pt-8 sm:px-6 md:grid-cols-[1.05fr_1fr] md:items-center md:pt-16 md:pb-24">
          <div className="animate-float-up">
            <span className="inline-flex items-center gap-2 rounded-full border border-primary/20 bg-card px-3 py-1 text-xs font-medium text-primary shadow-soft">
              <Sparkles className="h-3.5 w-3.5" /> Built for modern restaurants
            </span>
            <h1 className="mt-5 font-display text-5xl font-extrabold leading-[1.05] tracking-tight text-foreground sm:text-6xl md:text-7xl">
              Scan. <span className="text-primary">Order.</span> Serve.
            </h1>
            <p className="mt-5 max-w-xl text-base text-muted-foreground sm:text-lg">
              Tablo turns every table into a self-serve ordering counter. Diners order from their phone,
              your kitchen sees it instantly, and your floor staff finally have time for hospitality.
            </p>
            <div className="mt-8 flex flex-wrap gap-3">
              <Link
                to="/dashboard"
                className="inline-flex items-center gap-2 rounded-full border border-border bg-card px-6 py-3 text-sm font-semibold text-foreground shadow-soft transition-colors hover:bg-secondary"
              >
                <ChefHat className="h-4 w-4" /> Waiter dashboard
              </Link>
              <Link
                to="/admin"
                className="inline-flex items-center gap-2 rounded-full border border-border bg-card px-6 py-3 text-sm font-semibold text-foreground shadow-soft transition-colors hover:bg-secondary"
              >
                <Utensils className="h-4 w-4" /> Admin panel
              </Link>
              <a
                href="#pricing"
                className="inline-flex items-center gap-2 rounded-full bg-primary px-6 py-3 text-sm font-semibold text-primary-foreground shadow-soft transition-colors hover:bg-primary/90"
              >
                View Plans <ArrowRight className="h-4 w-4" />
              </a>
            </div>
            <div className="mt-10 flex flex-wrap items-center gap-x-8 gap-y-3 text-sm text-muted-foreground">
              <div className="flex items-center gap-2">
                <CheckCircle2 className="h-4 w-4 text-primary" /> No app download
              </div>
              <div className="flex items-center gap-2">
                <CheckCircle2 className="h-4 w-4 text-primary" /> Live order tracking
              </div>
              <div className="flex items-center gap-2">
                <Languages className="h-4 w-4 text-primary" /> EN · മലയാളം
              </div>
            </div>
          </div>
          <div className="relative">
            <div className="absolute -inset-6 -z-10 rounded-[2.5rem] bg-gradient-primary opacity-20 blur-3xl" />
            <div className="overflow-hidden rounded-[2rem] border border-border bg-card shadow-card">
              <img
                src={heroImg}
                alt="Tablo QR ordering preview"
                width={1536}
                height={1024}
                className="h-full w-full object-cover"
              />
            </div>
            <div className="absolute -left-4 bottom-6 hidden items-center gap-3 rounded-2xl border border-border bg-card p-3 pr-5 shadow-card sm:flex">
              <span className="grid h-10 w-10 place-items-center rounded-xl bg-primary-soft text-primary">
                <Bell className="h-4 w-4" />
              </span>
              <div>
                <p className="text-xs text-muted-foreground">Table 12</p>
                <p className="text-sm font-semibold">New order — 3 items</p>
              </div>
              <span className="ml-2 inline-flex items-center gap-1.5 rounded-full bg-primary-soft px-2 py-0.5 text-xs font-medium text-primary">
                <span className="h-1.5 w-1.5 animate-pulse-dot rounded-full bg-primary" /> Live
              </span>
            </div>
          </div>
        </div>
      </section>

      {/* FEATURES SECTION */}
      <section id="features" className="mx-auto max-w-7xl px-4 py-20 sm:px-6 border-t border-border/40">
        <div className="mb-12 max-w-2xl">
          <p className="text-sm font-semibold uppercase tracking-wider text-primary">
            Comprehensive Capabilities
          </p>
          <h2 className="mt-2 font-display text-3xl font-bold sm:text-4xl">
            Everything your floor and kitchen need, unified.
          </h2>
          <p className="mt-3 text-muted-foreground">
            From contactless QR ordering to real-time kitchen tickets and Thalassery biryani inventory
            reconciliations.
          </p>
        </div>
        <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-4">
          {features.map((f) => (
            <div
              key={f.title}
              className="group rounded-3xl border border-border bg-card p-6 shadow-soft transition-all hover:-translate-y-1 hover:shadow-card"
            >
              <span className="grid h-12 w-12 place-items-center rounded-2xl bg-primary-soft text-primary transition-colors group-hover:bg-gradient-primary group-hover:text-primary-foreground">
                <f.icon className="h-5 w-5" />
              </span>
              <h3 className="mt-5 font-display text-lg font-semibold">{f.title}</h3>
              <p className="mt-2 text-sm text-muted-foreground">{f.desc}</p>
            </div>
          ))}
        </div>
      </section>

      {/* HOW IT WORKS */}
      <section className="bg-secondary/60 py-20">
        <div className="mx-auto max-w-7xl px-4 sm:px-6">
          <div className="mb-12 flex items-end justify-between gap-6">
            <div>
              <p className="text-sm font-semibold uppercase tracking-wider text-primary">How it works</p>
              <h2 className="mt-2 font-display text-3xl font-bold sm:text-4xl">Three steps from scan to served.</h2>
            </div>
            <Smartphone className="hidden h-10 w-10 text-primary md:block" />
          </div>
          <div className="grid gap-5 md:grid-cols-3">
            {steps.map((s) => (
              <div key={s.n} className="relative rounded-3xl border border-border bg-card p-7 shadow-soft">
                <span className="font-display text-5xl font-extrabold text-primary/15">{s.n}</span>
                <h3 className="mt-3 font-display text-xl font-semibold">{s.title}</h3>
                <p className="mt-2 text-sm text-muted-foreground">{s.desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* PRICING SECTION */}
      <section id="pricing" className="mx-auto max-w-7xl px-4 py-24 sm:px-6 border-t border-border/40">
        <div className="text-center max-w-3xl mx-auto mb-16">
          <p className="text-sm font-semibold uppercase tracking-wider text-primary">Simple, transparent pricing</p>
          <h2 className="mt-2 font-display text-3xl font-extrabold sm:text-5xl">
            Choose the right plan for your restaurant
          </h2>
          <p className="mt-4 text-base text-muted-foreground sm:text-lg">
            Transparent annual pricing for modern dining operations. First year includes comprehensive setup and onboarding.
          </p>
        </div>

        <div className="grid gap-6 sm:grid-cols-2 xl:grid-cols-4 items-stretch">
          {publicPricingPlans.map((plan) => (
            <div
              key={plan.id}
              className={`relative flex flex-col justify-between rounded-3xl border bg-card p-6 shadow-soft transition-all duration-300 hover:shadow-card ${
                plan.highlight
                  ? "border-primary ring-2 ring-primary/20 bg-gradient-to-b from-primary/5 to-card"
                  : "border-border"
              }`}
            >
              {plan.highlight && (
                <div className="absolute -top-3.5 left-1/2 -translate-x-1/2 rounded-full bg-primary px-3.5 py-1 text-xs font-bold text-primary-foreground shadow-sm">
                  {plan.badge}
                </div>
              )}

              <div>
                <div className="flex items-center justify-between gap-2">
                  <h3 className="font-display text-2xl font-bold text-foreground">{plan.name}</h3>
                  {!plan.highlight && (
                    <span className="rounded-full bg-secondary px-2.5 py-0.5 text-xs font-semibold text-muted-foreground">
                      {plan.badge}
                    </span>
                  )}
                </div>

                <p className="mt-2 text-xs text-muted-foreground min-h-[32px] leading-relaxed">
                  {plan.description}
                </p>

                {/* Pricing Block */}
                <div className="mt-4 rounded-2xl bg-secondary/40 p-4 border border-border/50">
                  <div className="flex items-baseline justify-between gap-2">
                    <span className="font-display text-3xl font-extrabold tracking-tight text-foreground">
                      ₹{plan.firstYearPriceInr.toLocaleString("en-IN")}
                    </span>
                    <span className="rounded-md bg-primary/10 px-2 py-0.5 text-[11px] font-bold uppercase tracking-wide text-primary">
                      First year
                    </span>
                  </div>

                  <p className="mt-2 text-xs font-medium text-emerald-600 dark:text-emerald-400">
                    Includes ₹{plan.setupFeeInr.toLocaleString("en-IN")} one-time setup & onboarding
                  </p>

                  <div className="mt-3 pt-3 border-t border-border/50 flex items-center justify-between text-xs">
                    <span className="text-muted-foreground font-medium">Annual renewal</span>
                    <span className="font-bold text-foreground">
                      ₹{plan.annualRenewalPriceInr.toLocaleString("en-IN")}/year
                    </span>
                  </div>
                </div>

                <div className="my-5 border-t border-border/60" />

                <div className="space-y-3">
                  <p className="text-xs font-bold uppercase tracking-wider text-muted-foreground">Included Features</p>
                  <ul className="space-y-2.5">
                    {plan.features.map((feat, idx) => (
                      <li key={idx} className="flex items-start gap-2.5 text-xs font-medium text-foreground">
                        <Check className="mt-0.5 h-4 w-4 shrink-0 text-primary" />
                        <span>{feat}</span>
                      </li>
                    ))}
                  </ul>
                </div>
              </div>

              <div className="mt-6 pt-4 border-t border-border/40">
                <Link
                  to="/auth"
                  className={`w-full inline-flex items-center justify-center rounded-xl py-3 text-sm font-semibold transition ${
                    plan.highlight
                      ? "bg-primary text-primary-foreground hover:bg-primary/90 shadow-soft"
                      : "bg-secondary text-secondary-foreground hover:bg-secondary/80 border border-border"
                  }`}
                >
                  Get Started with {plan.name}
                </Link>
                <div className="mt-2 text-center">
                  <a
                    href={`mailto:sales@tablo.com?subject=Inquiry%20regarding%20Tablo%20${encodeURIComponent(plan.name)}%20Plan`}
                    className="text-[11px] font-medium text-muted-foreground hover:text-primary transition-colors underline decoration-border hover:decoration-primary"
                  >
                    Contact sales for {plan.name}
                  </a>
                </div>
              </div>
            </div>
          ))}
        </div>

        <div className="mt-12 rounded-2xl border border-border/80 bg-card p-6 text-center sm:flex sm:items-center sm:justify-between sm:text-left shadow-soft">
          <div>
            <h4 className="font-display text-base font-bold text-foreground">Need a custom plan or have questions?</h4>
            <p className="mt-1 text-xs text-muted-foreground">
              We assist with hardware setup, POS integration, and multi-outlet menu migrations.
            </p>
          </div>
          <div className="mt-4 sm:mt-0 sm:shrink-0">
            <a
              href="mailto:sales@tablo.com?subject=Custom%20Enterprise%20Inquiry%20-%20Tablo"
              className="inline-flex items-center gap-2 rounded-xl bg-secondary px-5 py-2.5 text-xs font-semibold text-foreground transition-colors hover:bg-secondary/80 border border-border"
            >
              Talk to a Specialist <ArrowRight className="h-3.5 w-3.5" />
            </a>
          </div>
        </div>
      </section>

      {/* RESTAURANT RHYTHM */}
      <section className="mx-auto max-w-7xl px-4 py-20 sm:px-6">
        <div className="grid gap-10 rounded-[2rem] border border-border bg-gradient-hero p-8 shadow-card md:grid-cols-2 md:p-12">
          <div>
            <h2 className="font-display text-3xl font-bold sm:text-4xl">Built for the rhythm of a real restaurant.</h2>
            <p className="mt-4 text-muted-foreground">
              From quiet weekday lunches to a packed Saturday rush, Tablo keeps your floor calm, orders accurate, and
              your kitchen in flow.
            </p>
          </div>
          <ul className="grid gap-3">
            {benefits.map((b) => (
              <li key={b} className="flex items-start gap-3 rounded-2xl bg-card/70 p-4 backdrop-blur">
                <CheckCircle2 className="mt-0.5 h-5 w-5 shrink-0 text-primary" />
                <span className="text-sm font-medium text-foreground">{b}</span>
              </li>
            ))}
          </ul>
        </div>
      </section>

      {/* FOOTER */}
      <footer className="border-t border-border/60 py-10">
        <div className="mx-auto flex max-w-7xl flex-col items-center justify-between gap-3 px-4 text-sm text-muted-foreground sm:flex-row sm:px-6">
          <p>© {new Date().getFullYear()} Tablo. Hospitality, simplified.</p>
          <p className="flex items-center gap-2">
            <span className="h-1.5 w-1.5 animate-pulse-dot rounded-full bg-primary" /> Live demo environment
          </p>
        </div>
      </footer>
    </div>
  );
}
