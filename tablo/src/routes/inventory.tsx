import { createFileRoute, Link } from "@tanstack/react-router";
import React, { useEffect, useMemo, useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  Package,
  ChefHat,
  History,
  ClipboardCheck,
  Plus,
  AlertTriangle,
  Search,
  Loader2,
  TrendingDown,
  TrendingUp,
  CheckCircle2,
  X,
  ArrowRight,
  RefreshCw,
  SlidersHorizontal,
  FileSpreadsheet,
  BarChart3,
  DollarSign,
  Download,
  ChevronDown,
  ChevronRight,
  Info,
  Truck,
  ShoppingCart,
  FlaskConical,
  Boxes,
  ArrowLeftRight,
  Settings
} from "lucide-react";
import { toast } from "sonner";
import { SiteHeader } from "@/components/site-header";
import { AppLayout } from "@/components/app-layout";
import { CAPABILITIES, NAVIGATION_REGISTRY } from "@/config/navigation";
import { useEffectiveAccess } from "@/hooks/use-effective-access";
import { useAuth } from "@/hooks/use-auth";
import {
  SuppliersTab,
  PurchasingTab,
  PreparationsTab,
  LotsAndExpiryTab,
  TransfersTab,
  InventorySettingsModal,
} from "@/components/inventory-advanced";
import { CentralKitchenManager } from "@/components/central-kitchen-manager";
import {
  getIngredients,
  createIngredient,
  updateIngredient,
  recordStockMovement,
  getMovements,
  getRecipes,
  setRecipe,
  getStocktakes,
  startStocktake,
  postStocktake,
  getStockReport,
  exportStockReport,
  getSalesGrossProfitReport,
  exportSalesGrossProfitReport,
  type Ingredient,
  type MenuItemRecipe,
  type StockMovement,
  type Stocktake,
  type StockReport,
  type SalesGrossProfitReport
} from "@/services/inventory";
import { formatINR } from "@/lib/mock-data";

export const Route = createFileRoute("/inventory")({
  validateSearch: (search: Record<string, unknown>) => ({
    tab: typeof search.tab === "string" ? search.tab : undefined,
  }),
  head: () => ({
    meta: [
      { title: "Inventory & Costing — Tablo Admin" },
      { name: "description", content: "Manage ingredients, recipes, costing, automatic deduction, and financial reports." },
    ],
  }),
  component: InventoryPage,
});

function InventoryPage() {
  const { user, roles, loading: authLoading, signOut } = useAuth();
  const isAdmin = roles.includes("Admin") || roles.includes("Owner");

  if (authLoading) {
    return (
      <div className="grid min-h-screen place-items-center bg-secondary/40">
        <Loader2 className="h-6 w-6 animate-spin text-primary" />
      </div>
    );
  }

  if (!user || !isAdmin) {
    return (
      <div className="min-h-screen bg-secondary/40">
        <SiteHeader />
        <div className="mx-auto max-w-md px-4 py-16 text-center">
          <div className="rounded-3xl border border-border bg-card p-10 shadow-card">
            <h1 className="font-display text-2xl font-bold">Admin access required</h1>
            <p className="mt-2 text-sm text-muted-foreground">Sign in with an admin account to manage restaurant inventory and costing.</p>
            <Link to="/auth" className="mt-6 inline-flex items-center justify-center rounded-full bg-primary px-5 py-2.5 text-sm font-semibold text-primary-foreground">
              Sign in
            </Link>
          </div>
        </div>
      </div>
    );
  }

  return <InventoryInner restaurantId={user.restaurantId!} />;
}

function InventoryInner({ restaurantId }: { restaurantId: string }) {
  const { plan, hasCapability, isLoading: accessLoading } = useEffectiveAccess();
  const search = Route.useSearch();
  const navigate = Route.useNavigate();
  const tab = (search.tab as
    | "ingredients"
    | "recipes"
    | "preparations"
    | "suppliers"
    | "purchasing"
    | "lots"
    | "transfers"
    | "central-kitchen"
    | "movements"
    | "stocktakes"
    | "stock-report"
    | "gross-profit") || "ingredients";
  const setTab = (newTab: string) => {
    navigate({ search: { tab: newTab } });
  };
  const [settingsOpen, setSettingsOpen] = useState(false);
  const cache = useQueryClient();

  const isInventoryAllowed = hasCapability(CAPABILITIES.InventoryCore);
  const requiredCapabilities = NAVIGATION_REGISTRY.find(
    (item) => item.to === "/inventory" && item.tabParam === tab,
  )?.requiredCapabilities;
  const canOpenTab = isInventoryAllowed &&
    (!requiredCapabilities?.length || requiredCapabilities.some(hasCapability));

  const ingredientsQuery = useQuery({
    queryKey: ["inventory_ingredients", restaurantId],
    queryFn: () => getIngredients(restaurantId),
    enabled: isInventoryAllowed,
  });

  const recipesQuery = useQuery({
    queryKey: ["inventory_recipes", restaurantId],
    queryFn: () => getRecipes(restaurantId),
    enabled: isInventoryAllowed,
  });

  const lowStockCount = ingredientsQuery.data?.filter(i => i.isLowStock).length || 0;
  const unconfiguredCount = recipesQuery.data?.filter(r => !r.isConfigured).length || 0;

  if (accessLoading) return null;

  if (!canOpenTab) {
    return (
      <AppLayout>
        <div className="mx-auto max-w-xl px-4 py-16 text-center">
          <div className="rounded-3xl border border-border bg-card p-10 shadow-card">
            <div className="mx-auto mb-4 flex h-16 w-16 items-center justify-center rounded-2xl bg-primary/10 text-primary">
              <Package className="h-8 w-8" />
            </div>
            <h1 className="font-display text-2xl font-bold">Inventory & Recipe Costing</h1>
            <div className="mt-2 inline-flex items-center gap-1.5 rounded-full bg-primary/10 px-3 py-1 text-xs font-semibold text-primary">
              Inventory feature unavailable
            </div>
            <p className="mt-4 text-sm text-muted-foreground">
              Your restaurant’s <span className="font-semibold capitalize text-foreground">{plan?.name || "Basic"}</span> plan does not grant access to this inventory screen.
            </p>
            <div className="mt-6 flex flex-col sm:flex-row justify-center gap-3">
              <Link
                to="/admin"
                className="rounded-full border border-border bg-card px-5 py-2.5 text-xs font-semibold hover:bg-secondary"
              >
                Back to Menu & Tables
              </Link>
              <a
                href="/#pricing"
                className="rounded-full bg-primary px-5 py-2.5 text-xs font-semibold text-primary-foreground shadow-soft hover:bg-primary/90"
              >
                View Premium Features
              </a>
            </div>
          </div>
        </div>
      </AppLayout>
    );
  }

  return (
    <AppLayout>
      <div className="min-h-screen bg-secondary/40 pb-16">
        <main className="mx-auto max-w-7xl px-4 py-8 sm:px-6">
          <div className="flex flex-wrap items-end justify-between gap-4">
            <div>
              <p className="text-xs font-semibold uppercase tracking-wider text-primary">Manager workspace</p>
              <h1 className="mt-1 font-display text-3xl font-bold">Inventory, Purchasing & Profits</h1>
              <p className="mt-1 text-sm text-muted-foreground">Manage suppliers, purchase orders, lots, intermediate preps, transfers, recipe yields, and margins.</p>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <button
                onClick={() => setSettingsOpen(true)}
                className="inline-flex items-center gap-1.5 rounded-full border border-border bg-card px-4 py-2 text-xs font-semibold shadow-soft hover:bg-secondary"
              >
                <Settings className="h-3.5 w-3.5 text-muted-foreground" />
                Costing & Policy
              </button>
            </div>
          </div>

          {/* Tab Navigation */}
          <div className="mt-6 flex flex-wrap gap-2 border-b border-border/60 pb-3">
            <button
              onClick={() => setTab("ingredients")}
              className={`inline-flex items-center gap-2 rounded-full px-4 py-2 text-sm font-semibold transition ${tab === "ingredients" ? "bg-primary text-primary-foreground shadow-sm" : "bg-card text-muted-foreground hover:bg-secondary"
                }`}
            >
              <Package className="h-4 w-4" /> Ingredients
              {lowStockCount > 0 && (
                <span className="ml-1 rounded-full bg-destructive px-2 py-0.2 text-[11px] font-bold text-destructive-foreground">
                  {lowStockCount} low
                </span>
              )}
            </button>

            <button
              onClick={() => setTab("recipes")}
              className={`inline-flex items-center gap-2 rounded-full px-4 py-2 text-sm font-semibold transition ${tab === "recipes" ? "bg-primary text-primary-foreground shadow-sm" : "bg-card text-muted-foreground hover:bg-secondary"
                }`}
            >
              <ChefHat className="h-4 w-4" /> Recipes & Costing
              {unconfiguredCount > 0 && (
                <span className="ml-1 rounded-full bg-amber-500/20 px-2 py-0.2 text-[11px] font-bold text-amber-700 dark:text-amber-300">
                  {unconfiguredCount} unconfigured
                </span>
              )}
            </button>

            <button
              onClick={() => setTab("preparations")}
              className={`inline-flex items-center gap-2 rounded-full px-4 py-2 text-sm font-semibold transition ${tab === "preparations" ? "bg-primary text-primary-foreground shadow-sm" : "bg-card text-muted-foreground hover:bg-secondary"
                }`}
            >
              <FlaskConical className="h-4 w-4" /> Semi-Finished Prep
            </button>

            <button
              onClick={() => setTab("suppliers")}
              className={`inline-flex items-center gap-2 rounded-full px-4 py-2 text-sm font-semibold transition ${tab === "suppliers" ? "bg-primary text-primary-foreground shadow-sm" : "bg-card text-muted-foreground hover:bg-secondary"
                }`}
            >
              <Truck className="h-4 w-4" /> Suppliers
            </button>

            <button
              onClick={() => setTab("purchasing")}
              className={`inline-flex items-center gap-2 rounded-full px-4 py-2 text-sm font-semibold transition ${tab === "purchasing" ? "bg-primary text-primary-foreground shadow-sm" : "bg-card text-muted-foreground hover:bg-secondary"
                }`}
            >
              <ShoppingCart className="h-4 w-4" /> Purchasing & Returns
            </button>

            <button
              onClick={() => setTab("lots")}
              className={`inline-flex items-center gap-2 rounded-full px-4 py-2 text-sm font-semibold transition ${tab === "lots" ? "bg-primary text-primary-foreground shadow-sm" : "bg-card text-muted-foreground hover:bg-secondary"
                }`}
            >
              <Boxes className="h-4 w-4" /> Lots & Expiry
            </button>

            <button
              onClick={() => setTab("transfers")}
              className={`inline-flex items-center gap-2 rounded-full px-4 py-2 text-sm font-semibold transition ${tab === "transfers" ? "bg-primary text-primary-foreground shadow-sm" : "bg-card text-muted-foreground hover:bg-secondary"
                }`}
            >
              <ArrowLeftRight className="h-4 w-4" /> Transfers
            </button>

            <button
              onClick={() => setTab("movements")}
              className={`inline-flex items-center gap-2 rounded-full px-4 py-2 text-sm font-semibold transition ${tab === "movements" ? "bg-primary text-primary-foreground shadow-sm" : "bg-card text-muted-foreground hover:bg-secondary"
                }`}
            >
              <History className="h-4 w-4" /> Movement Ledger
            </button>

            <button
              onClick={() => setTab("stocktakes")}
              className={`inline-flex items-center gap-2 rounded-full px-4 py-2 text-sm font-semibold transition ${tab === "stocktakes" ? "bg-primary text-primary-foreground shadow-sm" : "bg-card text-muted-foreground hover:bg-secondary"
                }`}
            >
              <ClipboardCheck className="h-4 w-4" /> End-of-Day Stocktake
            </button>

            <button
              onClick={() => setTab("stock-report")}
              className={`inline-flex items-center gap-2 rounded-full px-4 py-2 text-sm font-semibold transition ${tab === "stock-report" ? "bg-primary text-primary-foreground shadow-sm" : "bg-card text-muted-foreground hover:bg-secondary"
                }`}
            >
              <FileSpreadsheet className="h-4 w-4" /> Stock Report
            </button>

            <button
              onClick={() => setTab("gross-profit")}
              className={`inline-flex items-center gap-2 rounded-full px-4 py-2 text-sm font-semibold transition ${tab === "gross-profit" ? "bg-primary text-primary-foreground shadow-sm" : "bg-card text-muted-foreground hover:bg-secondary"
                }`}
            >
              <BarChart3 className="h-4 w-4" /> Sales & Gross Profit
            </button>
          </div>

          {/* Tab Views */}
          <div className="mt-6">
            {tab === "ingredients" && <IngredientsTab restaurantId={restaurantId} ingredients={ingredientsQuery.data || []} loading={ingredientsQuery.isLoading} />}
            {tab === "recipes" && <RecipesTab restaurantId={restaurantId} recipes={recipesQuery.data || []} ingredients={ingredientsQuery.data || []} loading={recipesQuery.isLoading} />}
            {tab === "preparations" && <PreparationsTab restaurantId={restaurantId} ingredients={ingredientsQuery.data || []} />}
            {tab === "suppliers" && <SuppliersTab restaurantId={restaurantId} ingredients={ingredientsQuery.data || []} />}
            {tab === "purchasing" && <PurchasingTab restaurantId={restaurantId} ingredients={ingredientsQuery.data || []} />}
            {tab === "lots" && <LotsAndExpiryTab restaurantId={restaurantId} />}
            {tab === "transfers" && <TransfersTab restaurantId={restaurantId} ingredients={ingredientsQuery.data || []} />}
            {tab === "central-kitchen" && <CentralKitchenManager restaurantId={restaurantId} />}
            {tab === "movements" && <MovementsTab restaurantId={restaurantId} ingredients={ingredientsQuery.data || []} />}
            {tab === "stocktakes" && <StocktakesTab restaurantId={restaurantId} ingredients={ingredientsQuery.data || []} />}
            {tab === "stock-report" && <StockReportTab restaurantId={restaurantId} ingredients={ingredientsQuery.data || []} />}
            {tab === "gross-profit" && <GrossProfitReportTab restaurantId={restaurantId} recipes={recipesQuery.data || []} />}
          </div>

          {/* Inventory Costing & Policy Settings Modal */}
          {settingsOpen && (
            <InventorySettingsModal restaurantId={restaurantId} onClose={() => setSettingsOpen(false)} />
          )}
        </main>
      </div>
    </AppLayout>
  );
}

// --------------------------------------------------------------------------
// 1. INGREDIENTS TAB
// --------------------------------------------------------------------------
function IngredientsTab({ restaurantId, ingredients, loading }: { restaurantId: string; ingredients: Ingredient[]; loading: boolean }) {
  const cache = useQueryClient();
  const [query, setQuery] = useState("");
  const [filter, setFilter] = useState<"all" | "low" | "active">("all");
  const [createModal, setCreateModal] = useState(false);
  const [movementModal, setMovementModal] = useState<Ingredient | null>(null);

  const filtered = useMemo(() => {
    return ingredients.filter(i => {
      const matchQuery = i.name.toLowerCase().includes(query.toLowerCase());
      if (!matchQuery) return false;
      if (filter === "low") return i.isLowStock;
      if (filter === "active") return i.isActive;
      return true;
    });
  }, [ingredients, query, filter]);

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-border bg-card p-3 shadow-soft">
        <div className="relative min-w-[220px] flex-1">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <input
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Search ingredients…"
            className="w-full rounded-full border border-border bg-background py-2 pl-9 pr-3 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
          />
        </div>
        <div className="flex items-center gap-2">
          <button
            onClick={() => setFilter("all")}
            className={`rounded-full px-3 py-1.5 text-xs font-semibold ${filter === "all" ? "bg-foreground text-background" : "bg-secondary text-muted-foreground"}`}
          >
            All ({ingredients.length})
          </button>
          <button
            onClick={() => setFilter("low")}
            className={`rounded-full px-3 py-1.5 text-xs font-semibold ${filter === "low" ? "bg-destructive text-destructive-foreground" : "bg-secondary text-muted-foreground"}`}
          >
            Low Stock ({ingredients.filter(i => i.isLowStock).length})
          </button>
          <button
            onClick={() => setCreateModal(true)}
            className="inline-flex items-center gap-1.5 rounded-full bg-gradient-primary px-4 py-2 text-xs font-semibold text-primary-foreground shadow-glow"
          >
            <Plus className="h-3.5 w-3.5" /> Add ingredient
          </button>
        </div>
      </div>

      <div className="overflow-hidden rounded-3xl border border-border bg-card shadow-soft">
        <table className="w-full text-sm">
          <thead className="border-b border-border bg-secondary/50 text-left text-xs uppercase tracking-wider text-muted-foreground">
            <tr>
              <th className="p-4">Ingredient</th>
              <th className="p-4">Base Unit</th>
              <th className="p-4">Avg Unit Cost</th>
              <th className="p-4">On-Hand Stock</th>
              <th className="p-4">Threshold</th>
              <th className="p-4">Status</th>
              <th className="p-4 text-right">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {loading && (
              <tr>
                <td colSpan={7} className="p-10 text-center">
                  <Loader2 className="mx-auto h-5 w-5 animate-spin text-primary" />
                </td>
              </tr>
            )}
            {!loading && filtered.length === 0 && (
              <tr>
                <td colSpan={7} className="p-10 text-center text-sm text-muted-foreground">
                  No ingredients found matching your search.
                </td>
              </tr>
            )}
            {!loading && filtered.map((ing) => (
              <tr key={ing.id} className="hover:bg-secondary/20">
                <td className="p-4 font-semibold">{ing.name}</td>
                <td className="p-4 font-mono text-xs text-muted-foreground">{ing.baseUnit}</td>
                <td className="p-4 font-mono text-xs">
                  {ing.currentAverageCost > 0 ? (
                    <span className="font-semibold text-foreground">
                      ₹{ing.currentAverageCost.toFixed(4)} / {ing.baseUnit}
                    </span>
                  ) : (
                    <span className="text-muted-foreground italic">No cost recorded</span>
                  )}
                </td>
                <td className="p-4">
                  <span className={`font-mono font-bold ${ing.onHandQuantity < 0 ? "text-destructive" : ing.isLowStock ? "text-amber-600" : "text-foreground"}`}>
                    {ing.onHandQuantity.toLocaleString()} {ing.baseUnit}
                  </span>
                </td>
                <td className="p-4 font-mono text-xs text-muted-foreground">
                  {ing.lowStockThreshold ? `${ing.lowStockThreshold.toLocaleString()} ${ing.baseUnit}` : "—"}
                </td>
                <td className="p-4">
                  {ing.isLowStock ? (
                    <span className="inline-flex items-center gap-1 rounded-full bg-destructive/15 px-2.5 py-0.5 text-xs font-bold text-destructive">
                      <AlertTriangle className="h-3 w-3" /> Low stock
                    </span>
                  ) : (
                    <span className="inline-flex items-center gap-1 rounded-full bg-emerald-500/15 px-2.5 py-0.5 text-xs font-bold text-emerald-600">
                      <CheckCircle2 className="h-3 w-3" /> Normal
                    </span>
                  )}
                </td>
                <td className="p-4 text-right">
                  <button
                    onClick={() => setMovementModal(ing)}
                    className="rounded-full border border-border bg-background px-3 py-1.5 text-xs font-semibold hover:bg-secondary"
                  >
                    + Restock / Movement
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {createModal && (
        <CreateIngredientModal
          restaurantId={restaurantId}
          onClose={() => setCreateModal(false)}
          onSuccess={() => {
            setCreateModal(false);
            void cache.invalidateQueries({ queryKey: ["inventory_ingredients", restaurantId] });
            toast.success("Ingredient created");
          }}
        />
      )}

      {movementModal && (
        <RecordMovementModal
          restaurantId={restaurantId}
          ingredient={movementModal}
          onClose={() => setMovementModal(null)}
          onSuccess={() => {
            setMovementModal(null);
            void cache.invalidateQueries({ queryKey: ["inventory_ingredients", restaurantId] });
            void cache.invalidateQueries({ queryKey: ["inventory_movements", restaurantId] });
            toast.success("Stock movement recorded");
          }}
        />
      )}
    </div>
  );
}

function CreateIngredientModal({ restaurantId, onClose, onSuccess }: { restaurantId: string; onClose: () => void; onSuccess: () => void }) {
  const [name, setName] = useState("");
  const [baseUnit, setBaseUnit] = useState("g");
  const [threshold, setThreshold] = useState("");
  const [initialStock, setInitialStock] = useState("");
  const [initialUnit, setInitialUnit] = useState("g");
  const [initialCost, setInitialCost] = useState("");
  const [busy, setBusy] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) return;
    setBusy(true);
    try {
      await createIngredient(restaurantId, {
        name: name.trim(),
        baseUnit,
        lowStockThreshold: threshold ? parseFloat(threshold) : null,
        initialStock: initialStock ? parseFloat(initialStock) : null,
        initialStockUnit: initialUnit,
        initialCost: initialCost ? parseFloat(initialCost) : null,
        operationId: crypto.randomUUID(),
      });
      onSuccess();
    } catch (err: any) {
      toast.error(err.message || "Failed to create ingredient");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-background/80 backdrop-blur-sm p-4">
      <div className="w-full max-w-md rounded-3xl border border-border bg-card p-6 shadow-card">
        <div className="flex items-center justify-between pb-4 border-b border-border">
          <h2 className="font-display text-lg font-bold">Add New Ingredient</h2>
          <button onClick={onClose} className="rounded-full p-1 hover:bg-secondary"><X className="h-5 w-5" /></button>
        </div>
        <form onSubmit={handleSubmit} className="mt-4 space-y-4 text-sm">
          <div>
            <label className="block font-semibold">Ingredient Name</label>
            <input
              type="text"
              value={name}
              onChange={e => setName(e.target.value)}
              placeholder="e.g. Bone-in Chicken, Kaima Rice, Ghee"
              required
              className="mt-1 w-full rounded-xl border border-border bg-background px-3 py-2"
            />
          </div>

          <div>
            <label className="block font-semibold">Base Stock Unit</label>
            <select
              value={baseUnit}
              onChange={e => {
                setBaseUnit(e.target.value);
                setInitialUnit(e.target.value);
              }}
              className="mt-1 w-full rounded-xl border border-border bg-background px-3 py-2"
            >
              <option value="g">Grams (g) — Mass / Weight</option>
              <option value="ml">Milliliters (ml) — Volume / Liquid</option>
              <option value="pieces">Pieces (pcs) — Count / Items</option>
            </select>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block font-semibold">Low-Stock Alert Level</label>
              <input
                type="number"
                step="any"
                value={threshold}
                onChange={e => setThreshold(e.target.value)}
                placeholder={`in ${baseUnit}`}
                className="mt-1 w-full rounded-xl border border-border bg-background px-3 py-2"
              />
            </div>
            <div>
              <label className="block font-semibold">Opening Stock</label>
              <div className="flex gap-1.5 mt-1">
                <input
                  type="number"
                  step="any"
                  value={initialStock}
                  onChange={e => setInitialStock(e.target.value)}
                  placeholder="0"
                  className="w-full rounded-xl border border-border bg-background px-3 py-2"
                />
                <select
                  value={initialUnit}
                  onChange={e => setInitialUnit(e.target.value)}
                  className="rounded-xl border border-border bg-background px-2 py-2 text-xs"
                >
                  {baseUnit === "g" && (
                    <>
                      <option value="g">g</option>
                      <option value="kg">kg</option>
                    </>
                  )}
                  {baseUnit === "ml" && (
                    <>
                      <option value="ml">ml</option>
                      <option value="L">L</option>
                    </>
                  )}
                  {baseUnit === "pieces" && (
                    <option value="pieces">pcs</option>
                  )}
                </select>
              </div>
            </div>
          </div>

          <div>
            <label className="block font-semibold">Opening Stock Total Cost (INR)</label>
            <input
              type="number"
              step="any"
              value={initialCost}
              onChange={e => setInitialCost(e.target.value)}
              placeholder="e.g. 500 (total purchase cost of opening stock)"
              className="mt-1 w-full rounded-xl border border-border bg-background px-3 py-2"
            />
            {initialStock && initialCost && parseFloat(initialStock) > 0 && parseFloat(initialCost) > 0 && (
              <p className="mt-1 text-xs text-muted-foreground">
                Initial cost basis: ₹{(parseFloat(initialCost) / (initialUnit === "kg" || initialUnit === "L" ? parseFloat(initialStock) * 1000 : parseFloat(initialStock))).toFixed(4)} per {baseUnit}
              </p>
            )}
          </div>

          <div className="mt-6 flex justify-end gap-2 pt-4 border-t border-border">
            <button type="button" onClick={onClose} className="rounded-full px-4 py-2 font-semibold hover:bg-secondary">Cancel</button>
            <button type="submit" disabled={busy} className="rounded-full bg-primary px-5 py-2 font-semibold text-primary-foreground disabled:opacity-50">
              {busy ? "Saving…" : "Save Ingredient"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

function RecordMovementModal({ restaurantId, ingredient, onClose, onSuccess }: { restaurantId: string; ingredient: Ingredient; onClose: () => void; onSuccess: () => void }) {
  const [reason, setReason] = useState<"Restock" | "Waste" | "ManualCorrection">("Restock");
  const [quantity, setQuantity] = useState("");
  const [unit, setUnit] = useState(ingredient.baseUnit);
  const [totalCost, setTotalCost] = useState("");
  const [unitCost, setUnitCost] = useState("");
  const [notes, setNotes] = useState("");
  const [busy, setBusy] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const qty = parseFloat(quantity);
    if (isNaN(qty) || qty === 0) return;
    setBusy(true);
    try {
      await recordStockMovement(restaurantId, ingredient.id, {
        operationId: crypto.randomUUID(),
        quantity: qty,
        unit,
        reason,
        totalCost: totalCost ? parseFloat(totalCost) : null,
        unitCost: unitCost ? parseFloat(unitCost) : null,
        notes: notes.trim() || undefined,
      });
      onSuccess();
    } catch (err: any) {
      toast.error(err.message || "Failed to record movement");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-background/80 backdrop-blur-sm p-4">
      <div className="w-full max-w-md rounded-3xl border border-border bg-card p-6 shadow-card">
        <div className="flex items-center justify-between pb-4 border-b border-border">
          <div>
            <h2 className="font-display text-lg font-bold">Record Stock Movement</h2>
            <p className="text-xs text-muted-foreground">{ingredient.name} · Current on hand: {ingredient.onHandQuantity} {ingredient.baseUnit} (Avg cost: ₹{ingredient.currentAverageCost.toFixed(4)}/{ingredient.baseUnit})</p>
          </div>
          <button onClick={onClose} className="rounded-full p-1 hover:bg-secondary"><X className="h-5 w-5" /></button>
        </div>
        <form onSubmit={handleSubmit} className="mt-4 space-y-4 text-sm">
          <div>
            <label className="block font-semibold">Movement Type</label>
            <select
              value={reason}
              onChange={e => setReason(e.target.value as any)}
              className="mt-1 w-full rounded-xl border border-border bg-background px-3 py-2"
            >
              <option value="Restock">Restock / Purchase (+)</option>
              <option value="Waste">Kitchen Waste / Spoilage (-)</option>
              <option value="ManualCorrection">Manual Count Correction (+/-)</option>
            </select>
          </div>

          <div>
            <label className="block font-semibold">Quantity</label>
            <div className="flex gap-2 mt-1">
              <input
                type="number"
                step="any"
                value={quantity}
                onChange={e => {
                  setQuantity(e.target.value);
                  if (unitCost && e.target.value) {
                    const q = parseFloat(e.target.value);
                    const u = parseFloat(unitCost);
                    if (q > 0 && u > 0) setTotalCost((q * u).toFixed(2));
                  }
                }}
                placeholder="0"
                required
                className="w-full rounded-xl border border-border bg-background px-3 py-2"
              />
              <select
                value={unit}
                onChange={e => setUnit(e.target.value)}
                className="rounded-xl border border-border bg-background px-3 py-2 text-xs font-semibold"
              >
                {ingredient.baseUnit === "g" && (
                  <>
                    <option value="g">g</option>
                    <option value="kg">kg (1,000g)</option>
                  </>
                )}
                {ingredient.baseUnit === "ml" && (
                  <>
                    <option value="ml">ml</option>
                    <option value="L">L (1,000ml)</option>
                  </>
                )}
                {ingredient.baseUnit === "pieces" && (
                  <option value="pieces">pieces</option>
                )}
              </select>
            </div>
            {reason === "Waste" && <p className="mt-1 text-xs text-muted-foreground">Will be deducted from on-hand stock.</p>}
          </div>

          {reason === "Restock" && (
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="block font-semibold">Total Cost (INR)</label>
                <input
                  type="number"
                  step="any"
                  value={totalCost}
                  onChange={e => {
                    setTotalCost(e.target.value);
                    if (quantity && e.target.value) {
                      const q = parseFloat(quantity);
                      const t = parseFloat(e.target.value);
                      if (q > 0) setUnitCost((t / q).toFixed(2));
                    }
                  }}
                  placeholder="e.g. 500"
                  className="mt-1 w-full rounded-xl border border-border bg-background px-3 py-2"
                />
              </div>
              <div>
                <label className="block font-semibold">Unit Cost / {unit} (INR)</label>
                <input
                  type="number"
                  step="any"
                  value={unitCost}
                  onChange={e => {
                    setUnitCost(e.target.value);
                    if (quantity && e.target.value) {
                      const q = parseFloat(quantity);
                      const u = parseFloat(e.target.value);
                      if (q > 0) setTotalCost((q * u).toFixed(2));
                    }
                  }}
                  placeholder="e.g. 250"
                  className="mt-1 w-full rounded-xl border border-border bg-background px-3 py-2"
                />
              </div>
            </div>
          )}

          <div>
            <label className="block font-semibold">Notes / Reference</label>
            <input
              type="text"
              value={notes}
              onChange={e => setNotes(e.target.value)}
              placeholder="e.g. Vendor Invoice #9281 or Malabar Spices Co."
              className="mt-1 w-full rounded-xl border border-border bg-background px-3 py-2"
            />
          </div>

          <div className="mt-6 flex justify-end gap-2 pt-4 border-t border-border">
            <button type="button" onClick={onClose} className="rounded-full px-4 py-2 font-semibold hover:bg-secondary">Cancel</button>
            <button type="submit" disabled={busy} className="rounded-full bg-primary px-5 py-2 font-semibold text-primary-foreground disabled:opacity-50">
              {busy ? "Recording…" : "Post Movement"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

// --------------------------------------------------------------------------
// 2. RECIPES & COSTING TAB
// --------------------------------------------------------------------------
function RecipesTab({ restaurantId, recipes, ingredients, loading }: { restaurantId: string; recipes: MenuItemRecipe[]; ingredients: Ingredient[]; loading: boolean }) {
  const cache = useQueryClient();
  const [query, setQuery] = useState("");
  const [filter, setFilter] = useState<"all" | "configured" | "unconfigured">("all");
  const [editingRecipe, setEditingRecipe] = useState<MenuItemRecipe | null>(null);

  const filtered = useMemo(() => {
    return recipes.filter(r => {
      const matchQuery = r.menuItemName.toLowerCase().includes(query.toLowerCase()) || r.categoryName.toLowerCase().includes(query.toLowerCase());
      if (!matchQuery) return false;
      if (filter === "configured") return r.isConfigured;
      if (filter === "unconfigured") return !r.isConfigured;
      return true;
    });
  }, [recipes, query, filter]);

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-border bg-card p-3 shadow-soft">
        <div className="relative min-w-[220px] flex-1">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <input
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Search dishes or categories…"
            className="w-full rounded-full border border-border bg-background py-2 pl-9 pr-3 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
          />
        </div>
        <div className="flex items-center gap-2">
          <button
            onClick={() => setFilter("all")}
            className={`rounded-full px-3 py-1.5 text-xs font-semibold ${filter === "all" ? "bg-foreground text-background" : "bg-secondary text-muted-foreground"}`}
          >
            All Dishes ({recipes.length})
          </button>
          <button
            onClick={() => setFilter("configured")}
            className={`rounded-full px-3 py-1.5 text-xs font-semibold ${filter === "configured" ? "bg-foreground text-background" : "bg-secondary text-muted-foreground"}`}
          >
            Configured ({recipes.filter(r => r.isConfigured).length})
          </button>
          <button
            onClick={() => setFilter("unconfigured")}
            className={`rounded-full px-3 py-1.5 text-xs font-semibold ${filter === "unconfigured" ? "bg-amber-600 text-white" : "bg-secondary text-muted-foreground"}`}
          >
            Unconfigured ({recipes.filter(r => !r.isConfigured).length})
          </button>
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        {loading && <p className="col-span-2 text-center py-10"><Loader2 className="mx-auto h-5 w-5 animate-spin text-primary" /></p>}
        {!loading && filtered.map((recipe) => (
          <div key={recipe.menuItemId} className="rounded-3xl border border-border bg-card p-5 shadow-soft flex flex-col justify-between">
            <div>
              <div className="flex items-start justify-between gap-2">
                <div>
                  <span className="rounded-full bg-secondary px-2 py-0.5 text-[10px] uppercase font-bold text-muted-foreground">
                    {recipe.categoryName}
                  </span>
                  <h3 className="font-display text-lg font-bold mt-1">{recipe.menuItemName}</h3>
                  <div className="flex items-center gap-3 mt-1">
                    <span className="text-sm font-bold text-primary">{formatINR(recipe.price)}</span>
                    {recipe.isConfigured && (
                      <span className="text-xs font-medium text-muted-foreground">
                        Cost: <strong className="text-foreground">₹{recipe.totalCostPerServing.toFixed(2)}</strong> (Margin: <strong className={recipe.grossMarginPercent >= 60 ? "text-emerald-600" : "text-amber-600"}>{recipe.grossMarginPercent.toFixed(1)}%</strong>)
                      </span>
                    )}
                  </div>
                </div>
                <div>
                  {recipe.isConfigured ? (
                    <span className="inline-flex items-center gap-1 rounded-full bg-emerald-500/15 px-2.5 py-0.5 text-xs font-bold text-emerald-600">
                      <CheckCircle2 className="h-3 w-3" /> Configured
                    </span>
                  ) : (
                    <span className="inline-flex items-center gap-1 rounded-full bg-amber-500/15 px-2.5 py-0.5 text-xs font-bold text-amber-600">
                      <AlertTriangle className="h-3 w-3" /> Unconfigured
                    </span>
                  )}
                </div>
              </div>

              {recipe.hasIncompleteCost && recipe.isConfigured && (
                <div className="mt-2.5 flex items-center gap-1.5 rounded-xl bg-amber-500/10 px-3 py-1 text-xs text-amber-700 dark:text-amber-400">
                  <Info className="h-3.5 w-3.5 shrink-0" />
                  <span>Some recipe ingredients have zero or unrecorded cost basis.</span>
                </div>
              )}

              <div className="mt-4 border-t border-border pt-3">
                <p className="text-xs font-bold text-muted-foreground uppercase tracking-wider mb-2">Recipe Ingredients & Yield</p>
                {recipe.items.length === 0 ? (
                  <p className="text-xs text-muted-foreground italic">No ingredients configured. Sales of this dish will not deduct stock.</p>
                ) : (
                  <div className="space-y-1.5 max-h-48 overflow-y-auto pr-1">
                    {recipe.items.map(item => (
                      <div key={item.id} className="flex items-center justify-between text-xs rounded-xl bg-secondary/40 px-3 py-1.5">
                        <span className="font-semibold">{item.ingredientName}</span>
                        <div className="text-right font-mono">
                          <span>{item.usableQuantity} {item.ingredientBaseUnit} net</span>
                          <span className="text-muted-foreground mx-1">@ {item.yieldPercent}%</span>
                          <span className="font-bold text-primary">(= {item.rawQuantityPerServing.toFixed(1)} {item.ingredientBaseUnit} raw)</span>
                          {item.costPerServing > 0 && (
                            <span className="ml-1.5 text-muted-foreground">· ₹{item.costPerServing.toFixed(2)}</span>
                          )}
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>

            <div className="mt-4 pt-3 border-t border-border flex items-center justify-between">
              <span className="text-xs font-semibold text-muted-foreground">
                {recipe.items.length} ingredient{recipe.items.length === 1 ? "" : "s"} tracked
              </span>
              <button
                onClick={() => setEditingRecipe(recipe)}
                className="rounded-full bg-secondary px-4 py-1.5 text-xs font-bold hover:bg-foreground hover:text-background transition"
              >
                {recipe.isConfigured ? "Edit recipe" : "Configure recipe"}
              </button>
            </div>
          </div>
        ))}
      </div>

      {editingRecipe && (
        <EditRecipeModal
          restaurantId={restaurantId}
          recipe={editingRecipe}
          ingredients={ingredients}
          onClose={() => setEditingRecipe(null)}
          onSuccess={() => {
            setEditingRecipe(null);
            void cache.invalidateQueries({ queryKey: ["inventory_recipes", restaurantId] });
            toast.success("Recipe updated successfully");
          }}
        />
      )}
    </div>
  );
}

function EditRecipeModal({ restaurantId, recipe, ingredients, onClose, onSuccess }: {
  restaurantId: string;
  recipe: MenuItemRecipe;
  ingredients: Ingredient[];
  onClose: () => void;
  onSuccess: () => void;
}) {
  const [lines, setLines] = useState<{ ingredientId: string; usableQuantity: string; yieldPercent: string }[]>(
    recipe.items.map(i => ({
      ingredientId: i.ingredientId,
      usableQuantity: i.usableQuantity.toString(),
      yieldPercent: i.yieldPercent.toString(),
    }))
  );
  const [busy, setBusy] = useState(false);

  const addLine = () => {
    if (ingredients.length === 0) return;
    setLines([...lines, { ingredientId: ingredients[0].id, usableQuantity: "100", yieldPercent: "100" }]);
  };

  const removeLine = (idx: number) => {
    setLines(lines.filter((_, i) => i !== idx));
  };

  const updateLine = (idx: number, field: string, val: string) => {
    const updated = [...lines];
    updated[idx] = { ...updated[idx], [field]: val };
    setLines(updated);
  };

  const totalRawCost = useMemo(() => {
    return lines.reduce((sum, line) => {
      const ing = ingredients.find(i => i.id === line.ingredientId);
      if (!ing) return sum;
      const usable = parseFloat(line.usableQuantity) || 0;
      const yieldP = parseFloat(line.yieldPercent) || 100;
      const raw = yieldP > 0 ? usable / (yieldP / 100) : usable;
      return sum + (raw * ing.currentAverageCost);
    }, 0);
  }, [lines, ingredients]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    try {
      const payload = lines.map(l => ({
        ingredientId: l.ingredientId,
        usableQuantity: parseFloat(l.usableQuantity) || 0,
        yieldPercent: parseFloat(l.yieldPercent) || 100,
      }));
      await setRecipe(restaurantId, recipe.menuItemId, { items: payload });
      onSuccess();
    } catch (err: any) {
      toast.error(err.message || "Failed to update recipe");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-background/80 backdrop-blur-sm p-4">
      <div className="w-full max-w-xl rounded-3xl border border-border bg-card p-6 shadow-card max-h-[90vh] flex flex-col">
        <div className="flex items-center justify-between pb-4 border-b border-border">
          <div>
            <h2 className="font-display text-lg font-bold">Configure Recipe: {recipe.menuItemName}</h2>
            <p className="text-xs text-muted-foreground">{recipe.categoryName} · Selling Price: {formatINR(recipe.price)}</p>
          </div>
          <button onClick={onClose} className="rounded-full p-1 hover:bg-secondary"><X className="h-5 w-5" /></button>
        </div>

        <form onSubmit={handleSubmit} className="mt-4 flex-1 flex flex-col overflow-hidden text-sm">
          <div className="flex-1 overflow-y-auto space-y-3 pr-1">
            {lines.length === 0 ? (
              <p className="text-center py-6 text-xs text-muted-foreground">No ingredients in this recipe yet.</p>
            ) : (
              lines.map((line, idx) => {
                const ing = ingredients.find(i => i.id === line.ingredientId);
                const usable = parseFloat(line.usableQuantity) || 0;
                const yieldP = parseFloat(line.yieldPercent) || 100;
                const raw = yieldP > 0 ? (usable / (yieldP / 100)).toFixed(1) : usable.toFixed(1);
                const lineCost = ing && ing.currentAverageCost > 0 ? ((parseFloat(raw) || 0) * ing.currentAverageCost).toFixed(2) : "0.00";

                return (
                  <div key={idx} className="flex flex-wrap items-center gap-2 rounded-2xl border border-border bg-secondary/30 p-3">
                    <div className="flex-1 min-w-[160px]">
                      <select
                        value={line.ingredientId}
                        onChange={e => updateLine(idx, "ingredientId", e.target.value)}
                        className="w-full rounded-xl border border-border bg-background px-3 py-1.5 text-xs font-semibold"
                      >
                        {ingredients.map(i => (
                          <option key={i.id} value={i.id}>{i.name} ({i.baseUnit})</option>
                        ))}
                      </select>
                    </div>

                    <div className="w-24">
                      <input
                        type="number"
                        step="any"
                        value={line.usableQuantity}
                        onChange={e => updateLine(idx, "usableQuantity", e.target.value)}
                        placeholder="Usable qty"
                        required
                        className="w-full rounded-xl border border-border bg-background px-2.5 py-1.5 text-xs"
                      />
                    </div>

                    <div className="w-20">
                      <input
                        type="number"
                        step="any"
                        value={line.yieldPercent}
                        onChange={e => updateLine(idx, "yieldPercent", e.target.value)}
                        placeholder="Yield %"
                        required
                        className="w-full rounded-xl border border-border bg-background px-2.5 py-1.5 text-xs"
                      />
                    </div>

                    <div className="min-w-[100px] text-xs font-mono text-right">
                      <div className="font-bold text-primary">{raw} {ing?.baseUnit || "g"}</div>
                      <div className="text-[11px] text-muted-foreground">₹{lineCost}</div>
                    </div>

                    <button
                      type="button"
                      onClick={() => removeLine(idx)}
                      className="rounded-full p-1 text-muted-foreground hover:bg-destructive/15 hover:text-destructive"
                    >
                      <X className="h-4 w-4" />
                    </button>
                  </div>
                );
              })
            )}

            <button
              type="button"
              onClick={addLine}
              className="w-full rounded-2xl border border-dashed border-border py-2 text-xs font-semibold hover:border-primary hover:text-primary transition"
            >
              + Add ingredient to recipe
            </button>
          </div>

          <div className="mt-4 pt-4 border-t border-border flex items-center justify-between">
            <div>
              <p className="text-xs font-semibold text-muted-foreground">Calculated Recipe Cost / Serving</p>
              <p className="font-display text-lg font-bold">
                ₹{totalRawCost.toFixed(2)}{" "}
                <span className="text-xs font-normal text-muted-foreground">
                  (Margin: {recipe.price > 0 ? (((recipe.price - totalRawCost) / recipe.price) * 100).toFixed(1) : 0}%)
                </span>
              </p>
            </div>
            <div className="flex gap-2">
              <button type="button" onClick={onClose} className="rounded-full px-4 py-2 text-xs font-semibold hover:bg-secondary">Cancel</button>
              <button type="submit" disabled={busy} className="rounded-full bg-primary px-5 py-2 text-xs font-semibold text-primary-foreground disabled:opacity-50">
                {busy ? "Saving…" : "Save Recipe"}
              </button>
            </div>
          </div>
        </form>
      </div>
    </div>
  );
}

// --------------------------------------------------------------------------
// 3. MOVEMENT LEDGER TAB
// --------------------------------------------------------------------------
function MovementsTab({ restaurantId, ingredients }: { restaurantId: string; ingredients: Ingredient[] }) {
  const [selectedIngredient, setSelectedIngredient] = useState<string>("");
  const { data: movements = [], isLoading } = useQuery({
    queryKey: ["inventory_movements", restaurantId, selectedIngredient],
    queryFn: () => getMovements(restaurantId, selectedIngredient || undefined),
  });

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-border bg-card p-3 shadow-soft">
        <div className="flex items-center gap-2">
          <label className="text-xs font-bold text-muted-foreground uppercase">Filter Ingredient:</label>
          <select
            value={selectedIngredient}
            onChange={e => setSelectedIngredient(e.target.value)}
            className="rounded-full border border-border bg-background px-3 py-1.5 text-xs font-semibold focus:outline-none"
          >
            <option value="">All Ingredients</option>
            {ingredients.map(i => (
              <option key={i.id} value={i.id}>{i.name}</option>
            ))}
          </select>
        </div>
        <p className="text-xs text-muted-foreground">Showing last {movements.length} append-only ledger entries</p>
      </div>

      <div className="overflow-hidden rounded-3xl border border-border bg-card shadow-soft">
        <table className="w-full text-sm">
          <thead className="border-b border-border bg-secondary/50 text-left text-xs uppercase tracking-wider text-muted-foreground">
            <tr>
              <th className="p-4">Timestamp (IST)</th>
              <th className="p-4">Ingredient</th>
              <th className="p-4">Reason</th>
              <th className="p-4">Quantity Change</th>
              <th className="p-4">Unit Cost</th>
              <th className="p-4">Total Cost</th>
              <th className="p-4">Reference / Notes</th>
              <th className="p-4">Recorded By</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {isLoading && (
              <tr><td colSpan={8} className="p-10 text-center"><Loader2 className="mx-auto h-5 w-5 animate-spin text-primary" /></td></tr>
            )}
            {!isLoading && movements.length === 0 && (
              <tr><td colSpan={8} className="p-10 text-center text-sm text-muted-foreground">No stock movements recorded yet.</td></tr>
            )}
            {!isLoading && movements.map(m => (
              <tr key={m.id} className="hover:bg-secondary/20">
                <td className="p-4 font-mono text-xs text-muted-foreground">
                  {new Date(m.timestampUtc).toLocaleString("en-IN", { timeZone: "Asia/Kolkata" })}
                </td>
                <td className="p-4 font-semibold">{m.ingredientName}</td>
                <td className="p-4">
                  <span className={`inline-block rounded-full px-2.5 py-0.5 text-xs font-bold ${m.reason === "OrderDeduction" ? "bg-blue-500/15 text-blue-600" :
                      m.reason === "Restock" || m.reason === "OpeningStock" ? "bg-emerald-500/15 text-emerald-600" :
                        m.reason === "Waste" ? "bg-rose-500/15 text-rose-600" : "bg-purple-500/15 text-purple-600"
                    }`}>
                    {m.reason}
                  </span>
                </td>
                <td className="p-4 font-mono font-bold">
                  <span className={m.quantityChange > 0 ? "text-emerald-600" : "text-foreground"}>
                    {m.quantityChange > 0 ? `+${m.quantityChange.toLocaleString()}` : m.quantityChange.toLocaleString()} {m.unitSnapshot}
                  </span>
                </td>
                <td className="p-4 font-mono text-xs">
                  {m.unitCostSnapshot ? `₹${m.unitCostSnapshot.toFixed(4)}` : "—"}
                </td>
                <td className="p-4 font-mono text-xs font-semibold">
                  {m.totalCost ? `₹${m.totalCost.toFixed(2)}` : "—"}
                </td>
                <td className="p-4 text-xs text-muted-foreground">
                  {m.referenceType ? `${m.referenceType} · ` : ""}{m.notes || "—"}
                </td>
                <td className="p-4 text-xs text-muted-foreground">{m.actorName || "System"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

// --------------------------------------------------------------------------
// 4. END-OF-DAY STOCKTAKE TAB
// --------------------------------------------------------------------------
function StocktakesTab({ restaurantId, ingredients }: { restaurantId: string; ingredients: Ingredient[] }) {
  const cache = useQueryClient();
  const { data: stocktakes = [], isLoading } = useQuery({
    queryKey: ["inventory_stocktakes", restaurantId],
    queryFn: () => getStocktakes(restaurantId),
  });

  const [activeStocktake, setActiveStocktake] = useState<Stocktake | null>(null);
  const [counts, setCounts] = useState<{ [ingId: string]: string }>({});
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    const draft = stocktakes.find(s => s.status === "Draft");
    if (draft) {
      setActiveStocktake(draft);
      const initial: { [ingId: string]: string } = {};
      draft.items.forEach(i => { initial[i.ingredientId] = i.physicalCount.toString(); });
      setCounts(initial);
    }
  }, [stocktakes]);

  const handleStart = async () => {
    setBusy(true);
    try {
      const res = await startStocktake(restaurantId, crypto.randomUUID());
      setActiveStocktake(res);
      const initial: { [ingId: string]: string } = {};
      res.items.forEach(i => { initial[i.ingredientId] = i.physicalCount.toString(); });
      setCounts(initial);
      void cache.invalidateQueries({ queryKey: ["inventory_stocktakes", restaurantId] });
      toast.success("New stocktake started with current stock snapshot");
    } catch (err: any) {
      -
      toast.error(err.message || "Failed to start stocktake");
    } finally {
      setBusy(false);
    }
  };

  const handlePost = async (confirmReconciliation: boolean) => {
    if (!activeStocktake) return;
    setBusy(true);
    try {
      const itemsPayload = activeStocktake.items.map(item => ({
        ingredientId: item.ingredientId,
        physicalCount: parseFloat(counts[item.ingredientId]) || 0,
      }));

      await postStocktake(restaurantId, activeStocktake.id, {
        operationId: crypto.randomUUID(),
        confirmReconciliation,
        counts: itemsPayload,
      });

      setActiveStocktake(null);
      void cache.invalidateQueries({ queryKey: ["inventory_stocktakes", restaurantId] });
      void cache.invalidateQueries({ queryKey: ["inventory_ingredients", restaurantId] });
      void cache.invalidateQueries({ queryKey: ["inventory_movements", restaurantId] });
      toast.success("Stocktake adjustments posted successfully");
    } catch (err: any) {
      toast.error(err.message || "Failed to post stocktake");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="space-y-6">
      {!activeStocktake ? (
        <div className="rounded-3xl border border-border bg-card p-8 shadow-soft text-center max-w-xl mx-auto">
          <ClipboardCheck className="mx-auto h-12 w-12 text-primary mb-3" />
          <h2 className="font-display text-xl font-bold">End-of-Day Physical Stocktake</h2>
          <p className="mt-2 text-sm text-muted-foreground">
            Freeze a point-in-time snapshot of on-hand inventory balances, record actual counted physical quantities, and review variances before posting adjustments.
          </p>
          <button
            onClick={handleStart}
            disabled={busy}
            className="mt-6 inline-flex items-center gap-2 rounded-full bg-gradient-primary px-6 py-2.5 text-sm font-semibold text-primary-foreground shadow-glow disabled:opacity-50"
          >
            {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <Plus className="h-4 w-4" />} Start New Stocktake
          </button>
        </div>
      ) : (
        <div className="rounded-3xl border border-border bg-card p-6 shadow-soft space-y-4">
          <div className="flex flex-wrap items-center justify-between gap-3 pb-4 border-b border-border">
            <div>
              <span className="rounded-full bg-amber-500/15 px-2.5 py-0.5 text-xs font-bold text-amber-600">
                Draft Stocktake · IST Date: {activeStocktake.businessDateIst}
              </span>
              <h2 className="font-display text-xl font-bold mt-1">Enter Physical Counts</h2>
            </div>
            <div className="flex items-center gap-2">
              <button
                onClick={() => handlePost(true)}
                disabled={busy}
                className="rounded-full bg-primary px-5 py-2 text-xs font-bold text-primary-foreground shadow-glow disabled:opacity-50"
              >
                {busy ? "Posting…" : "Post Stocktake Adjustments"}
              </button>
            </div>
          </div>

          <div className="overflow-hidden rounded-2xl border border-border">
            <table className="w-full text-sm">
              <thead className="border-b border-border bg-secondary/50 text-left text-xs uppercase text-muted-foreground">
                <tr>
                  <th className="p-3">Ingredient</th>
                  <th className="p-3">Base Unit</th>
                  <th className="p-3">Snapshot Stock</th>
                  <th className="p-3">Physical Count</th>
                  <th className="p-3">Variance</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {activeStocktake.items.map(item => {
                  const countVal = counts[item.ingredientId] ?? item.physicalCount.toString();
                  const numCount = parseFloat(countVal) || 0;
                  const variance = numCount - item.recordedStockSnapshot;

                  return (
                    <tr key={item.id} className="hover:bg-secondary/20">
                      <td className="p-3 font-semibold">{item.ingredientName}</td>
                      <td className="p-3 font-mono text-xs text-muted-foreground">{item.baseUnit}</td>
                      <td className="p-3 font-mono">{item.recordedStockSnapshot}</td>
                      <td className="p-3">
                        <input
                          type="number"
                          step="any"
                          value={countVal}
                          onChange={e => setCounts({ ...counts, [item.ingredientId]: e.target.value })}
                          className="w-28 rounded-xl border border-border bg-background px-3 py-1 text-sm font-mono"
                        />
                      </td>
                      <td className="p-3 font-mono font-bold">
                        <span className={variance < 0 ? "text-destructive" : variance > 0 ? "text-emerald-600" : "text-muted-foreground"}>
                          {variance > 0 ? `+${variance}` : variance} {item.baseUnit}
                        </span>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Historical Stocktakes */}
      <div className="space-y-3">
        <h3 className="font-display text-base font-bold">Historical Stocktakes</h3>
        <div className="overflow-hidden rounded-3xl border border-border bg-card shadow-soft">
          <table className="w-full text-sm">
            <thead className="border-b border-border bg-secondary/50 text-left text-xs uppercase text-muted-foreground">
              <tr>
                <th className="p-4">IST Date</th>
                <th className="p-4">Status</th>
                <th className="p-4">Counted At</th>
                <th className="p-4">Posted At</th>
                <th className="p-4">Conducted By</th>
                <th className="p-4">Items Counted</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {stocktakes.filter(s => s.status === "Completed").length === 0 && (
                <tr><td colSpan={6} className="p-8 text-center text-xs text-muted-foreground">No posted stocktakes yet.</td></tr>
              )}
              {stocktakes.filter(s => s.status === "Completed").map(s => (
                <tr key={s.id} className="hover:bg-secondary/20">
                  <td className="p-4 font-semibold">{s.businessDateIst}</td>
                  <td className="p-4">
                    <span className="rounded-full bg-emerald-500/15 px-2.5 py-0.5 text-xs font-bold text-emerald-600">
                      Completed
                    </span>
                  </td>
                  <td className="p-4 font-mono text-xs text-muted-foreground">
                    {new Date(s.countedAtUtc).toLocaleString("en-IN", { timeZone: "Asia/Kolkata" })}
                  </td>
                  <td className="p-4 font-mono text-xs text-muted-foreground">
                    {s.postedAtUtc ? new Date(s.postedAtUtc).toLocaleString("en-IN", { timeZone: "Asia/Kolkata" }) : "—"}
                  </td>
                  <td className="p-4 text-xs">{s.actorName || "Admin"}</td>
                  <td className="p-4 font-mono text-xs">{s.items.length} ingredients</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}

// --------------------------------------------------------------------------
// 5. STOCK VALUATION REPORT TAB
// --------------------------------------------------------------------------
function StockReportTab({ restaurantId, ingredients }: { restaurantId: string; ingredients: Ingredient[] }) {
  const [startDate, setStartDate] = useState("");
  const [endDate, setEndDate] = useState("");
  const [ingredientId, setIngredientId] = useState("");
  const [lowStockOnly, setLowStockOnly] = useState(false);
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [exporting, setExporting] = useState(false);

  const { data: report, isLoading, refetch } = useQuery({
    queryKey: ["stock_report", restaurantId, startDate, endDate, ingredientId, lowStockOnly],
    queryFn: () => getStockReport(restaurantId, {
      startDate: startDate || undefined,
      endDate: endDate || undefined,
      ingredientId: ingredientId || undefined,
      lowStockOnly: lowStockOnly || undefined,
    }),
  });

  const handleExport = async () => {
    try {
      setExporting(true);
      await exportStockReport(restaurantId, {
        startDate: startDate || undefined,
        endDate: endDate || undefined,
        ingredientId: ingredientId || undefined,
        lowStockOnly: lowStockOnly || undefined,
      });
      toast.success("Stock report Excel downloaded");
    } catch (err: any) {
      toast.error(err.message || "Failed to export stock report");
    } finally {
      setExporting(false);
    }
  };

  return (
    <div className="space-y-6">
      {/* Filters Bar */}
      <div className="flex flex-wrap items-center justify-between gap-4 rounded-3xl border border-border bg-card p-4 shadow-soft">
        <div className="flex flex-wrap items-center gap-3">
          <div>
            <label className="block text-[11px] font-bold uppercase text-muted-foreground">Start Date (IST)</label>
            <input
              type="date"
              value={startDate}
              onChange={e => setStartDate(e.target.value)}
              className="mt-1 rounded-xl border border-border bg-background px-3 py-1.5 text-xs font-semibold focus:outline-none"
            />
          </div>
          <div>
            <label className="block text-[11px] font-bold uppercase text-muted-foreground">End Date (IST)</label>
            <input
              type="date"
              value={endDate}
              onChange={e => setEndDate(e.target.value)}
              className="mt-1 rounded-xl border border-border bg-background px-3 py-1.5 text-xs font-semibold focus:outline-none"
            />
          </div>
          <div>
            <label className="block text-[11px] font-bold uppercase text-muted-foreground">Filter Ingredient</label>
            <select
              value={ingredientId}
              onChange={e => setIngredientId(e.target.value)}
              className="mt-1 rounded-xl border border-border bg-background px-3 py-1.5 text-xs font-semibold focus:outline-none"
            >
              <option value="">All Ingredients</option>
              {ingredients.map(i => (
                <option key={i.id} value={i.id}>{i.name}</option>
              ))}
            </select>
          </div>
          <div className="flex items-center gap-2 pt-4">
            <label className="flex items-center gap-1.5 text-xs font-semibold cursor-pointer">
              <input
                type="checkbox"
                checked={lowStockOnly}
                onChange={e => setLowStockOnly(e.target.checked)}
                className="rounded border-border"
              />
              Low Stock Only
            </label>
          </div>
        </div>

        <button
          onClick={handleExport}
          disabled={exporting || isLoading}
          className="inline-flex items-center gap-2 rounded-full bg-emerald-600 hover:bg-emerald-700 px-5 py-2 text-xs font-bold text-white shadow-glow disabled:opacity-50"
        >
          {exporting ? <Loader2 className="h-4 w-4 animate-spin" /> : <Download className="h-4 w-4" />} Export Excel
        </button>
      </div>

      {/* KPI Cards */}
      {report && (
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
          <div className="rounded-3xl border border-border bg-card p-5 shadow-soft">
            <p className="text-xs font-bold text-muted-foreground uppercase tracking-wider">Total Closing Stock Value</p>
            <h3 className="font-display text-2xl font-bold mt-1 text-emerald-600">₹{report.totalClosingStockValue.toLocaleString("en-IN", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</h3>
            <p className="mt-1 text-xs text-muted-foreground">Valuated at moving weighted-average costs</p>
          </div>
          <div className="rounded-3xl border border-border bg-card p-5 shadow-soft">
            <p className="text-xs font-bold text-muted-foreground uppercase tracking-wider">Tracked Ingredients</p>
            <h3 className="font-display text-2xl font-bold mt-1">{report.totalIngredientsCount}</h3>
            <p className="mt-1 text-xs text-muted-foreground">Active ingredients in report</p>
          </div>
          <div className="rounded-3xl border border-border bg-card p-5 shadow-soft">
            <p className="text-xs font-bold text-muted-foreground uppercase tracking-wider">Low Stock Alerts</p>
            <h3 className={`font-display text-2xl font-bold mt-1 ${report.lowStockCount > 0 ? "text-destructive" : "text-emerald-600"}`}>
              {report.lowStockCount}
            </h3>
            <p className="mt-1 text-xs text-muted-foreground">Ingredients at or below threshold</p>
          </div>
        </div>
      )}

      {/* Report Table */}
      <div className="overflow-hidden rounded-3xl border border-border bg-card shadow-soft">
        <table className="w-full text-sm">
          <thead className="border-b border-border bg-secondary/50 text-left text-xs uppercase tracking-wider text-muted-foreground">
            <tr>
              <th className="p-4">Ingredient</th>
              <th className="p-4">Base Unit</th>
              <th className="p-4 text-right">Opening</th>
              <th className="p-4 text-right">Receipts</th>
              <th className="p-4 text-right">Sales</th>
              <th className="p-4 text-right">Waste</th>
              <th className="p-4 text-right">Adjustments</th>
              <th className="p-4 text-right">Closing Stock</th>
              <th className="p-4 text-right">Unit Cost</th>
              <th className="p-4 text-right">Closing Value</th>
              <th className="p-4 text-center">Status</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {isLoading && (
              <tr><td colSpan={11} className="p-10 text-center"><Loader2 className="mx-auto h-5 w-5 animate-spin text-primary" /></td></tr>
            )}
            {!isLoading && (!report || report.rows.length === 0) && (
              <tr><td colSpan={11} className="p-10 text-center text-sm text-muted-foreground">No data for selected period.</td></tr>
            )}
            {!isLoading && report?.rows.map(row => {
              const isExpanded = expandedId === row.ingredientId;

              return (
                <React.Fragment key={row.ingredientId}>
                  <tr
                    onClick={() => setExpandedId(isExpanded ? null : row.ingredientId)}
                    className="hover:bg-secondary/20 cursor-pointer transition"
                  >
                    <td className="p-4 font-semibold flex items-center gap-1.5">
                      {isExpanded ? <ChevronDown className="h-4 w-4 text-muted-foreground" /> : <ChevronRight className="h-4 w-4 text-muted-foreground" />}
                      {row.ingredientName}
                    </td>
                    <td className="p-4 font-mono text-xs text-muted-foreground">{row.baseUnit}</td>
                    <td className="p-4 font-mono text-xs text-right">{row.openingQuantity.toLocaleString()}</td>
                    <td className="p-4 font-mono text-xs text-right text-emerald-600 font-semibold">
                      {row.receiptsQuantity > 0 ? `+${row.receiptsQuantity.toLocaleString()}` : "—"}
                    </td>
                    <td className="p-4 font-mono text-xs text-right text-blue-600 font-semibold">
                      {row.salesQuantity < 0 ? row.salesQuantity.toLocaleString() : "—"}
                    </td>
                    <td className="p-4 font-mono text-xs text-right text-rose-600 font-semibold">
                      {row.wasteQuantity < 0 ? row.wasteQuantity.toLocaleString() : "—"}
                    </td>
                    <td className="p-4 font-mono text-xs text-right font-semibold">
                      {row.adjustmentsQuantity !== 0 ? (row.adjustmentsQuantity > 0 ? `+${row.adjustmentsQuantity}` : row.adjustmentsQuantity) : "—"}
                    </td>
                    <td className="p-4 font-mono font-bold text-right">
                      <span className={row.closingQuantity < 0 ? "text-destructive" : row.isLowStock ? "text-amber-600" : "text-foreground"}>
                        {row.closingQuantity.toLocaleString()} {row.baseUnit}
                      </span>
                    </td>
                    <td className="p-4 font-mono text-xs text-right">₹{row.costPerBaseUnit.toFixed(4)}</td>
                    <td className="p-4 font-mono text-xs font-bold text-right text-emerald-600">
                      ₹{row.closingStockValue.toFixed(2)}
                    </td>
                    <td className="p-4 text-center">
                      {row.isLowStock ? (
                        <span className="rounded-full bg-destructive/15 px-2 py-0.5 text-[10px] font-bold text-destructive">LOW</span>
                      ) : (
                        <span className="rounded-full bg-emerald-500/15 px-2 py-0.5 text-[10px] font-bold text-emerald-600">OK</span>
                      )}
                    </td>
                  </tr>

                  {isExpanded && (
                    <tr className="bg-secondary/30">
                      <td colSpan={11} className="p-4">
                        <div className="rounded-2xl border border-border bg-card p-3 space-y-2">
                          <p className="text-xs font-bold uppercase tracking-wider text-muted-foreground">
                            Movement Audit Breakdown ({row.movements.length} events in selected window)
                          </p>
                          {row.movements.length === 0 ? (
                            <p className="text-xs text-muted-foreground italic">No movements during this date window.</p>
                          ) : (
                            <div className="overflow-x-auto">
                              <table className="w-full text-xs">
                                <thead>
                                  <tr className="border-b border-border text-muted-foreground text-left">
                                    <th className="p-2">Timestamp (IST)</th>
                                    <th className="p-2">Type</th>
                                    <th className="p-2 text-right">Qty Change</th>
                                    <th className="p-2 text-right">Cost Basis</th>
                                    <th className="p-2 text-right">Valuation</th>
                                    <th className="p-2">Reference</th>
                                    <th className="p-2">Actor</th>
                                  </tr>
                                </thead>
                                <tbody className="divide-y divide-border/50">
                                  {row.movements.map(m => (
                                    <tr key={m.id}>
                                      <td className="p-2 font-mono">{m.timestampIst}</td>
                                      <td className="p-2 font-semibold">{m.reason}</td>
                                      <td className="p-2 font-mono text-right font-bold">
                                        {m.quantityChange > 0 ? `+${m.quantityChange}` : m.quantityChange} {m.unitSnapshot}
                                      </td>
                                      <td className="p-2 font-mono text-right">{m.unitCostSnapshot ? `₹${m.unitCostSnapshot.toFixed(4)}` : "—"}</td>
                                      <td className="p-2 font-mono text-right">{m.totalCost ? `₹${m.totalCost.toFixed(2)}` : "—"}</td>
                                      <td className="p-2 text-muted-foreground">{m.referenceType ? `${m.referenceType} · ` : ""}{m.notes || "—"}</td>
                                      <td className="p-2 text-muted-foreground">{m.actorName || "System"}</td>
                                    </tr>
                                  ))}
                                </tbody>
                              </table>
                            </div>
                          )}
                        </div>
                      </td>
                    </tr>
                  )}
                </React.Fragment>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}

// --------------------------------------------------------------------------
// 6. SALES & GROSS PROFIT REPORT TAB
// --------------------------------------------------------------------------
function GrossProfitReportTab({ restaurantId, recipes }: { restaurantId: string; recipes: MenuItemRecipe[] }) {
  const [startDate, setStartDate] = useState("");
  const [endDate, setEndDate] = useState("");
  const [menuItemId, setMenuItemId] = useState("");
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [exporting, setExporting] = useState(false);

  const { data: report, isLoading } = useQuery({
    queryKey: ["sales_gross_profit_report", restaurantId, startDate, endDate, menuItemId],
    queryFn: () => getSalesGrossProfitReport(restaurantId, {
      startDate: startDate || undefined,
      endDate: endDate || undefined,
      menuItemId: menuItemId || undefined,
    }),
  });

  const handleExport = async () => {
    try {
      setExporting(true);
      await exportSalesGrossProfitReport(restaurantId, {
        startDate: startDate || undefined,
        endDate: endDate || undefined,
        menuItemId: menuItemId || undefined,
      });
      toast.success("Sales & Gross Profit Excel downloaded");
    } catch (err: any) {
      toast.error(err.message || "Failed to export report");
    } finally {
      setExporting(false);
    }
  };

  return (
    <div className="space-y-6">
      {/* Filters Bar */}
      <div className="flex flex-wrap items-center justify-between gap-4 rounded-3xl border border-border bg-card p-4 shadow-soft">
        <div className="flex flex-wrap items-center gap-3">
          <div>
            <label className="block text-[11px] font-bold uppercase text-muted-foreground">Start Date (IST)</label>
            <input
              type="date"
              value={startDate}
              onChange={e => setStartDate(e.target.value)}
              className="mt-1 rounded-xl border border-border bg-background px-3 py-1.5 text-xs font-semibold focus:outline-none"
            />
          </div>
          <div>
            <label className="block text-[11px] font-bold uppercase text-muted-foreground">End Date (IST)</label>
            <input
              type="date"
              value={endDate}
              onChange={e => setEndDate(e.target.value)}
              className="mt-1 rounded-xl border border-border bg-background px-3 py-1.5 text-xs font-semibold focus:outline-none"
            />
          </div>
          <div>
            <label className="block text-[11px] font-bold uppercase text-muted-foreground">Filter Menu Dish</label>
            <select
              value={menuItemId}
              onChange={e => setMenuItemId(e.target.value)}
              className="mt-1 rounded-xl border border-border bg-background px-3 py-1.5 text-xs font-semibold focus:outline-none"
            >
              <option value="">All Menu Dishes</option>
              {recipes.map(r => (
                <option key={r.menuItemId} value={r.menuItemId}>{r.menuItemName}</option>
              ))}
            </select>
          </div>
        </div>

        <button
          onClick={handleExport}
          disabled={exporting || isLoading}
          className="inline-flex items-center gap-2 rounded-full bg-emerald-600 hover:bg-emerald-700 px-5 py-2 text-xs font-bold text-white shadow-glow disabled:opacity-50"
        >
          {exporting ? <Loader2 className="h-4 w-4 animate-spin" /> : <Download className="h-4 w-4" />} Export Excel
        </button>
      </div>

      {/* Incomplete Costing Alert Banner */}
      {report && report.incompleteCostingCount > 0 && (
        <div className="rounded-3xl border border-amber-500/30 bg-amber-500/10 p-4 text-xs text-amber-800 dark:text-amber-300 space-y-1">
          <div className="flex items-center gap-2 font-bold text-sm">
            <AlertTriangle className="h-4 w-4 text-amber-600" />
            <span>Incomplete Recipe Costing Detected ({report.incompleteCostingCount} dishes, ₹{report.incompleteSalesAmount.toFixed(2)} sales)</span>
          </div>
          <p className="text-muted-foreground">
            The following dishes were sold without configured recipes or lacked ingredient cost basis. Their revenue is included below, but ingredient COGS is marked incomplete:
          </p>
          <ul className="list-disc list-inside mt-1 font-semibold space-y-0.5">
            {report.incompleteCostingItems.map(item => (
              <li key={item.menuItemId}>
                {item.menuItemName} ({item.quantitySold} portions, ₹{item.netSales.toFixed(2)} sales) — {item.reason}
              </li>
            ))}
          </ul>
        </div>
      )}

      {/* Financial KPI Cards */}
      {report && (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          <div className="rounded-3xl border border-border bg-card p-5 shadow-soft">
            <p className="text-xs font-bold text-muted-foreground uppercase tracking-wider">Net Sales (Excl. Tax)</p>
            <h3 className="font-display text-2xl font-bold mt-1">₹{report.totalNetSalesExcludingTax.toFixed(2)}</h3>
            <p className="mt-1 text-[11px] text-muted-foreground">Gross ₹{report.totalGrossSales.toFixed(2)} − Disc ₹{report.totalDiscounts.toFixed(2)}</p>
          </div>

          <div className="rounded-3xl border border-border bg-card p-5 shadow-soft">
            <p className="text-xs font-bold text-muted-foreground uppercase tracking-wider">Ingredient COGS</p>
            <h3 className="font-display text-2xl font-bold mt-1 text-primary">₹{report.totalCogs.toFixed(2)}</h3>
            <p className="mt-1 text-[11px] text-muted-foreground">Recorded at time of settlement</p>
          </div>

          <div className="rounded-3xl border border-border bg-card p-5 shadow-soft">
            <p className="text-xs font-bold text-muted-foreground uppercase tracking-wider">Gross Profit</p>
            <h3 className="font-display text-2xl font-bold mt-1 text-emerald-600">₹{report.totalGrossProfit.toFixed(2)}</h3>
            <p className="mt-1 text-[11px] text-muted-foreground">Net Sales − Ingredient COGS</p>
          </div>

          <div className="rounded-3xl border border-border bg-card p-5 shadow-soft">
            <p className="text-xs font-bold text-muted-foreground uppercase tracking-wider">Gross Margin %</p>
            <h3 className="font-display text-2xl font-bold mt-1 text-emerald-600">{report.overallGrossMarginPercent.toFixed(1)}%</h3>
            <p className="mt-1 text-[11px] text-muted-foreground">{report.totalPortionsSold} portions across {report.totalBillsSettled} bills</p>
          </div>
        </div>
      )}

      {/* Breakdown Table */}
      <div className="overflow-hidden rounded-3xl border border-border bg-card shadow-soft">
        <table className="w-full text-sm">
          <thead className="border-b border-border bg-secondary/50 text-left text-xs uppercase tracking-wider text-muted-foreground">
            <tr>
              <th className="p-4">Dish</th>
              <th className="p-4">Category</th>
              <th className="p-4 text-right">Sold Qty</th>
              <th className="p-4 text-right">Gross Sales</th>
              <th className="p-4 text-right">Discounts</th>
              <th className="p-4 text-right">Net Sales (Excl. Tax)</th>
              <th className="p-4 text-right">Tax</th>
              <th className="p-4 text-right">Ingredient COGS</th>
              <th className="p-4 text-right">Gross Profit</th>
              <th className="p-4 text-right">Margin %</th>
              <th className="p-4 text-center">Costing</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {isLoading && (
              <tr><td colSpan={11} className="p-10 text-center"><Loader2 className="mx-auto h-5 w-5 animate-spin text-primary" /></td></tr>
            )}
            {!isLoading && (!report || report.rows.length === 0) && (
              <tr><td colSpan={11} className="p-10 text-center text-sm text-muted-foreground">No settled dish sales recorded during this date window.</td></tr>
            )}
            {!isLoading && report?.rows.map(row => {
              const isExpanded = expandedId === row.menuItemId;

              return (
                <React.Fragment key={row.menuItemId}>
                  <tr
                    onClick={() => setExpandedId(isExpanded ? null : row.menuItemId)}
                    className="hover:bg-secondary/20 cursor-pointer transition"
                  >
                    <td className="p-4 font-semibold flex items-center gap-1.5">
                      {isExpanded ? <ChevronDown className="h-4 w-4 text-muted-foreground" /> : <ChevronRight className="h-4 w-4 text-muted-foreground" />}
                      {row.menuItemName}
                    </td>
                    <td className="p-4 text-xs text-muted-foreground">{row.categoryName}</td>
                    <td className="p-4 font-mono text-xs text-right font-bold">{row.quantitySold}</td>
                    <td className="p-4 font-mono text-xs text-right">₹{row.grossSales.toFixed(2)}</td>
                    <td className="p-4 font-mono text-xs text-right text-muted-foreground">
                      {row.totalDiscount > 0 ? `₹${row.totalDiscount.toFixed(2)}` : "—"}
                    </td>
                    <td className="p-4 font-mono text-xs text-right font-bold">₹{row.netSalesExcludingTax.toFixed(2)}</td>
                    <td className="p-4 font-mono text-xs text-right text-muted-foreground">₹{row.totalTax.toFixed(2)}</td>
                    <td className="p-4 font-mono text-xs text-right font-semibold text-primary">₹{row.totalCogs.toFixed(2)}</td>
                    <td className="p-4 font-mono text-xs text-right font-bold text-emerald-600">₹{row.grossProfit.toFixed(2)}</td>
                    <td className="p-4 font-mono text-xs text-right font-bold">
                      <span className={row.grossMarginPercent >= 60 ? "text-emerald-600" : "text-amber-600"}>
                        {row.grossMarginPercent.toFixed(1)}%
                      </span>
                    </td>
                    <td className="p-4 text-center">
                      {row.isCostComplete ? (
                        <span className="rounded-full bg-emerald-500/15 px-2 py-0.5 text-[10px] font-bold text-emerald-600">COMPLETE</span>
                      ) : (
                        <span className="rounded-full bg-amber-500/15 px-2 py-0.5 text-[10px] font-bold text-amber-700" title={row.incompleteReason || "Missing recipe"}>
                          INCOMPLETE
                        </span>
                      )}
                    </td>
                  </tr>

                  {isExpanded && (
                    <tr className="bg-secondary/30">
                      <td colSpan={11} className="p-4">
                        <div className="rounded-2xl border border-border bg-card p-4 space-y-3">
                          <p className="text-xs font-bold uppercase tracking-wider text-muted-foreground">
                            Settled Bills Breakdown & Ingredient Recipe Costs
                          </p>
                          <div className="space-y-3">
                            {row.drillDown.map(bill => (
                              <div key={bill.billId} className="rounded-xl border border-border/70 bg-secondary/20 p-3 space-y-2">
                                <div className="flex flex-wrap items-center justify-between text-xs font-semibold">
                                  <span>Table {bill.tableNumber} {bill.groupLabel ? `(${bill.groupLabel})` : ""} · Settled {bill.settledAtIst}</span>
                                  <span>{bill.quantity} portion{bill.quantity === 1 ? "" : "s"} · Net ₹{bill.netRevenue.toFixed(2)} · COGS ₹{bill.totalIngredientCost.toFixed(2)} (GP: ₹{bill.grossProfit.toFixed(2)})</span>
                                </div>
                                <div className="overflow-x-auto">
                                  <table className="w-full text-xs font-mono">
                                    <thead>
                                      <tr className="text-muted-foreground text-left border-b border-border">
                                        <th className="p-1">Ingredient</th>
                                        <th className="p-1 text-right">Raw Qty / Portion</th>
                                        <th className="p-1 text-right">Total Consumed</th>
                                        <th className="p-1 text-right">Cost Basis</th>
                                        <th className="p-1 text-right">Extended Cost</th>
                                      </tr>
                                    </thead>
                                    <tbody className="divide-y divide-border/40">
                                      {bill.ingredientBreakdown.map(ib => (
                                        <tr key={ib.ingredientId}>
                                          <td className="p-1 font-semibold">{ib.ingredientName}</td>
                                          <td className="p-1 text-right">{ib.rawQuantityPerPortion.toFixed(1)} {ib.baseUnit}</td>
                                          <td className="p-1 text-right">{ib.totalRawQuantity.toFixed(1)} {ib.baseUnit}</td>
                                          <td className="p-1 text-right">₹{ib.unitCostSnapshot.toFixed(4)}</td>
                                          <td className="p-1 text-right font-bold text-primary">₹{ib.totalCost.toFixed(2)}</td>
                                        </tr>
                                      ))}
                                    </tbody>
                                  </table>
                                </div>
                              </div>
                            ))}
                          </div>
                        </div>
                      </td>
                    </tr>
                  )}
                </React.Fragment>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
