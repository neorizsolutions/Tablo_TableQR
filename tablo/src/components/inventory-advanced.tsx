import { confirmClose } from "@/lib/confirmation";
import React, { useState, useMemo } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  Truck,
  ShoppingCart,
  FlaskConical,
  Boxes,
  ArrowLeftRight,
  Settings,
  Plus,
  Search,
  Loader2,
  AlertTriangle,
  AlertCircle,
  CheckCircle2,
  X,
  ArrowRight,
  TrendingUp,
  SlidersHorizontal,
  ChevronDown,
  RotateCcw
} from "lucide-react";
import { toast } from "sonner";
import {
  getSuppliers,
  createSupplier,
  updateSupplier,
  getSupplierIngredients,
  setSupplierIngredient,
  compareIngredientPrices,
  getPurchaseOrders,
  createPurchaseOrder,
  updatePurchaseOrderStatus,
  receiveGoods,
  getInvoices,
  getPurchaseReturns,
  createPurchaseReturn,
  getPreparationRecipes,
  savePreparationRecipe,
  recordPreparationBatch,
  getPreparationBatches,
  getLots,
  quarantineLot,
  writeOffLot,
  getInventorySettings,
  updateInventorySettings,
  getOutlets,
  createOutlet,
  getTransfers,
  createTransfer,
  dispatchTransfer,
  receiveTransfer,
  getMenuAvailability,
  type Supplier,
  type Ingredient,
  type PurchaseOrder,
  type PurchaseInvoice,
  type PurchaseReturn,
  type PreparationRecipe,
  type StockLot,
  type Outlet,
  type StockTransfer,
  type IngredientPriceComparison
} from "@/services/inventory";
import { parcelApi, type TransferDiscrepancyReportDto } from "@/services/parcel";

function getErrorMessage(err: unknown) {
  if (err instanceof Error) return err.message;
  if (typeof err === "object" && err !== null && "message" in err) {
    return String((err as { message: unknown }).message);
  }
  return "An unexpected error occurred.";
}

// ============================================================================
// 1. SUPPLIERS TAB
// ============================================================================
export function SuppliersTab({ restaurantId, ingredients }: { restaurantId: string; ingredients: Ingredient[] }) {
  const cache = useQueryClient();
  const [query, setQuery] = useState("");
  const [createModal, setCreateModal] = useState(false);
  const [manageSupplier, setManageSupplier] = useState<Supplier | null>(null);
  const [compareIngredientId, setCompareIngredientId] = useState<string | null>(null);

  const suppliersQuery = useQuery({
    queryKey: ["inventory_suppliers", restaurantId],
    queryFn: () => getSuppliers(restaurantId),
  });

  const suppliers = suppliersQuery.data || [];
  const filtered = useMemo(() => {
    return suppliers.filter(s =>
      s.name.toLowerCase().includes(query.toLowerCase()) ||
      (s.contactPerson && s.contactPerson.toLowerCase().includes(query.toLowerCase()))
    );
  }, [suppliers, query]);

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-border bg-card p-3 shadow-soft">
        <div className="relative min-w-[220px] flex-1">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <input
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Search suppliers by name or contact…"
            className="w-full rounded-full border border-border bg-background py-2 pl-9 pr-3 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
          />
        </div>
        <div className="flex items-center gap-2">
          {ingredients.length > 0 && (
            <select
              value={compareIngredientId || ""}
              onChange={(e) => setCompareIngredientId(e.target.value || null)}
              className="rounded-full border border-border bg-background px-3 py-2 text-xs font-semibold focus:outline-none focus:ring-2 focus:ring-ring"
            >
              <option value="">Compare rates for ingredient…</option>
              {ingredients.map(ing => (
                <option key={ing.id} value={ing.id}>{ing.name} ({ing.baseUnit})</option>
              ))}
            </select>
          )}
          <button
            onClick={() => setCreateModal(true)}
            className="inline-flex items-center gap-1.5 rounded-full bg-gradient-primary px-4 py-2 text-xs font-semibold text-primary-foreground shadow-glow"
          >
            <Plus className="h-3.5 w-3.5" /> Add Supplier
          </button>
        </div>
      </div>

      <div className="overflow-hidden rounded-3xl border border-border bg-card shadow-soft">
        <table className="w-full text-sm">
          <thead className="border-b border-border bg-secondary/50 text-left text-xs uppercase tracking-wider text-muted-foreground">
            <tr>
              <th className="p-4">Supplier</th>
              <th className="p-4">Contact Person</th>
              <th className="p-4">Phone / Email</th>
              <th className="p-4">GST / Tax ID</th>
              <th className="p-4">Packs / Items</th>
              <th className="p-4">Status</th>
              <th className="p-4 text-right">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {suppliersQuery.isLoading && (
              <tr>
                <td colSpan={7} className="p-10 text-center">
                  <Loader2 className="mx-auto h-5 w-5 animate-spin text-primary" />
                </td>
              </tr>
            )}
            {!suppliersQuery.isLoading && filtered.length === 0 && (
              <tr>
                <td colSpan={7} className="p-10 text-center text-sm text-muted-foreground">
                  No suppliers found. Click &quot;Add Supplier&quot; to configure your restaurant suppliers.
                </td>
              </tr>
            )}
            {!suppliersQuery.isLoading && filtered.map(s => (
              <tr key={s.id} className="hover:bg-secondary/20">
                <td className="p-4 font-semibold">{s.name}</td>
                <td className="p-4 text-muted-foreground">{s.contactPerson || "—"}</td>
                <td className="p-4 text-xs font-mono text-muted-foreground">
                  {s.phone && <div>{s.phone}</div>}
                  {s.email && <div>{s.email}</div>}
                  {!s.phone && !s.email && "—"}
                </td>
                <td className="p-4 text-xs font-mono">{s.taxId || "—"}</td>
                <td className="p-4 text-xs font-semibold">
                  <span className="rounded-full bg-secondary px-2.5 py-1 text-foreground">
                    {s.ingredientsCount} item{s.ingredientsCount === 1 ? "" : "s"}
                  </span>
                </td>
                <td className="p-4">
                  {s.isActive ? (
                    <span className="rounded-full bg-emerald-500/15 px-2 py-0.5 text-xs font-bold text-emerald-600">Active</span>
                  ) : (
                    <span className="rounded-full bg-muted px-2 py-0.5 text-xs font-bold text-muted-foreground">Archived</span>
                  )}
                </td>
                <td className="p-4 text-right">
                  <button
                    onClick={() => setManageSupplier(s)}
                    className="rounded-full border border-border bg-background px-3 py-1 text-xs font-semibold hover:bg-secondary"
                  >
                    Manage Packs & Rates
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {createModal && (
        <CreateSupplierModal
          restaurantId={restaurantId}
          onClose={() => setCreateModal(false)}
          onSuccess={() => {
            setCreateModal(false);
            cache.invalidateQueries({ queryKey: ["inventory_suppliers", restaurantId] });
          }}
        />
      )}

      {manageSupplier && (
        <SupplierIngredientsModal
          restaurantId={restaurantId}
          supplier={manageSupplier}
          ingredients={ingredients}
          onClose={() => setManageSupplier(null)}
        />
      )}

      {compareIngredientId && (
        <PriceComparisonModal
          restaurantId={restaurantId}
          ingredientId={compareIngredientId}
          onClose={() => setCompareIngredientId(null)}
        />
      )}
    </div>
  );
}

function CreateSupplierModal({ restaurantId, onClose, onSuccess }: { restaurantId: string; onClose: () => void; onSuccess: () => void }) {
  const [name, setName] = useState("");
  const [contactPerson, setContactPerson] = useState("");
  const [phone, setPhone] = useState("");
  const [email, setEmail] = useState("");
  const [address, setAddress] = useState("");
  const [taxId, setTaxId] = useState("");
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) return toast.error("Supplier name is required.");
    setLoading(true);
    try {
      await createSupplier(restaurantId, {
        name: name.trim(),
        contactPerson: contactPerson.trim() || undefined,
        phone: phone.trim() || undefined,
        email: email.trim() || undefined,
        address: address.trim() || undefined,
        taxId: taxId.trim() || undefined
      });
      toast.success(`Supplier '${name.trim()}' added.`);
      onSuccess();
    } catch (err: unknown) {
      toast.error(getErrorMessage(err));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
      <div className="w-full max-w-lg rounded-3xl border border-border bg-card p-6 shadow-glow">
        <div className="flex items-center justify-between border-b border-border pb-3">
          <h2 className="font-display text-lg font-bold">Add New Supplier</h2>
          <button onClick={() => void confirmClose(onClose)} className="rounded-full p-1 text-muted-foreground hover:bg-secondary"><X className="h-5 w-5" /></button>
        </div>
        <form onSubmit={handleSubmit} className="mt-4 space-y-4 text-sm">
          <div>
            <label className="text-xs font-bold text-muted-foreground">Supplier / Business Name *</label>
            <input required value={name} onChange={e => setName(e.target.value)} placeholder="e.g. Malabar Rice Mills" className="mt-1 w-full rounded-2xl border border-border bg-background p-2.5" />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="text-xs font-bold text-muted-foreground">Contact Person</label>
              <input value={contactPerson} onChange={e => setContactPerson(e.target.value)} placeholder="e.g. Ashraf" className="mt-1 w-full rounded-2xl border border-border bg-background p-2.5" />
            </div>
            <div>
              <label className="text-xs font-bold text-muted-foreground">Phone Number</label>
              <input value={phone} onChange={e => setPhone(e.target.value)} placeholder="e.g. +91 9876543210" className="mt-1 w-full rounded-2xl border border-border bg-background p-2.5" />
            </div>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="text-xs font-bold text-muted-foreground">Email</label>
              <input type="email" value={email} onChange={e => setEmail(e.target.value)} placeholder="orders@supplier.com" className="mt-1 w-full rounded-2xl border border-border bg-background p-2.5" />
            </div>
            <div>
              <label className="text-xs font-bold text-muted-foreground">GSTIN / Tax ID</label>
              <input value={taxId} onChange={e => setTaxId(e.target.value)} placeholder="e.g. 32AAAAA0000A1Z5" className="mt-1 w-full rounded-2xl border border-border bg-background p-2.5 font-mono" />
            </div>
          </div>
          <div>
            <label className="text-xs font-bold text-muted-foreground">Office / Warehouse Address</label>
            <textarea rows={2} value={address} onChange={e => setAddress(e.target.value)} placeholder="Market Road, Calicut..." className="mt-1 w-full rounded-2xl border border-border bg-background p-2.5" />
          </div>
          <div className="flex justify-end gap-2 pt-2">
            <button type="button" onClick={() => void confirmClose(onClose)} className="rounded-full px-4 py-2 font-semibold text-muted-foreground hover:bg-secondary">Cancel</button>
            <button type="submit" disabled={loading} className="inline-flex items-center gap-1.5 rounded-full bg-gradient-primary px-5 py-2 font-semibold text-primary-foreground">
              {loading && <Loader2 className="h-4 w-4 animate-spin" />} Save Supplier
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

function SupplierIngredientsModal({ restaurantId, supplier, ingredients, onClose }: { restaurantId: string; supplier: Supplier; ingredients: Ingredient[]; onClose: () => void }) {
  const cache = useQueryClient();
  const [selectedIngredientId, setSelectedIngredientId] = useState("");
  const [packUnit, setPackUnit] = useState("25 kg sack");
  const [packQty, setPackQty] = useState(25);
  const [packUnitType, setPackUnitType] = useState("kg");
  const [pricePerPack, setPricePerPack] = useState(2250);
  const [saving, setSaving] = useState(false);

  const packsQuery = useQuery({
    queryKey: ["supplier_ingredients", restaurantId, supplier.id],
    queryFn: () => getSupplierIngredients(restaurantId, supplier.id),
  });

  const selectedIng = ingredients.find(i => i.id === selectedIngredientId);

  const handleAddPack = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedIngredientId) return toast.error("Please choose an ingredient.");
    setSaving(true);
    try {
      const convFactor = packUnitType === "kg" && selectedIng?.baseUnit === "g" ? packQty * 1000
        : packUnitType === "L" && selectedIng?.baseUnit === "ml" ? packQty * 1000
        : packQty;

      await setSupplierIngredient(restaurantId, supplier.id, {
        supplierId: supplier.id,
        ingredientId: selectedIngredientId,
        packUnit: packUnit.trim(),
        packQuantity: packQty,
        packUnitType,
        conversionFactor: convFactor,
        lastPurchasePricePerPack: pricePerPack
      });
      toast.success("Pack configuration updated.");
      cache.invalidateQueries({ queryKey: ["supplier_ingredients", restaurantId, supplier.id] });
      cache.invalidateQueries({ queryKey: ["inventory_suppliers", restaurantId] });
      setSelectedIngredientId("");
    } catch (err: unknown) {
      toast.error(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
      <div className="w-full max-w-2xl max-h-[90vh] overflow-y-auto rounded-3xl border border-border bg-card p-6 shadow-glow">
        <div className="flex items-center justify-between border-b border-border pb-3">
          <div>
            <h2 className="font-display text-lg font-bold">Supplier Packs & Rates: {supplier.name}</h2>
            <p className="text-xs text-muted-foreground">Configure standard pack sizes (e.g. 25 kg sack) and purchase pricing.</p>
          </div>
          <button onClick={() => void confirmClose(onClose)} className="rounded-full p-1 text-muted-foreground hover:bg-secondary"><X className="h-5 w-5" /></button>
        </div>

        <form onSubmit={handleAddPack} className="mt-4 rounded-2xl border border-border bg-secondary/30 p-4 space-y-3">
          <p className="text-xs font-bold uppercase tracking-wider text-primary">Add / Update Supplied Ingredient</p>
          <div className="grid grid-cols-2 gap-3 text-xs">
            <div>
              <label className="font-bold text-muted-foreground">Ingredient *</label>
              <select
                required
                value={selectedIngredientId}
                onChange={e => {
                  setSelectedIngredientId(e.target.value);
                  const ing = ingredients.find(i => i.id === e.target.value);
                  if (ing?.baseUnit === "g") { setPackUnitType("kg"); setPackUnit("25 kg sack"); setPackQty(25); }
                  else if (ing?.baseUnit === "ml") { setPackUnitType("L"); setPackUnit("5 L tin"); setPackQty(5); }
                  else { setPackUnitType("pieces"); setPackUnit("100 pc crate"); setPackQty(100); }
                }}
                className="mt-1 w-full rounded-xl border border-border bg-background p-2"
              >
                <option value="">Select ingredient…</option>
                {ingredients.map(i => (
                  <option key={i.id} value={i.id}>{i.name} (Base: {i.baseUnit})</option>
                ))}
              </select>
            </div>
            <div>
              <label className="font-bold text-muted-foreground">Pack Name</label>
              <input required value={packUnit} onChange={e => setPackUnit(e.target.value)} placeholder="e.g. 25 kg sack" className="mt-1 w-full rounded-xl border border-border bg-background p-2" />
            </div>
          </div>
          <div className="grid grid-cols-3 gap-3 text-xs">
            <div>
              <label className="font-bold text-muted-foreground">Pack Qty</label>
              <input type="number" step="any" min="0.01" required value={packQty} onChange={e => setPackQty(parseFloat(e.target.value) || 1)} className="mt-1 w-full rounded-xl border border-border bg-background p-2 font-mono" />
            </div>
            <div>
              <label className="font-bold text-muted-foreground">Pack Unit Type</label>
              <select value={packUnitType} onChange={e => setPackUnitType(e.target.value)} className="mt-1 w-full rounded-xl border border-border bg-background p-2">
                <option value="kg">kg (Kilograms)</option>
                <option value="g">g (Grams)</option>
                <option value="L">L (Litres)</option>
                <option value="ml">ml (Millilitres)</option>
                <option value="pieces">pieces (Count)</option>
              </select>
            </div>
            <div>
              <label className="font-bold text-muted-foreground">Price Per Pack (₹)</label>
              <input type="number" step="any" min="0" required value={pricePerPack} onChange={e => setPricePerPack(parseFloat(e.target.value) || 0)} className="mt-1 w-full rounded-xl border border-border bg-background p-2 font-mono font-bold" />
            </div>
          </div>
          <div className="flex justify-end pt-1">
            <button type="submit" disabled={saving || !selectedIngredientId} className="rounded-full bg-primary px-4 py-2 text-xs font-semibold text-primary-foreground">
              {saving ? "Saving…" : "Save Pack Rate"}
            </button>
          </div>
        </form>

        <div className="mt-6">
          <p className="text-xs font-bold uppercase tracking-wider text-muted-foreground mb-2">Configured Supplied Packs</p>
          <div className="overflow-hidden rounded-2xl border border-border bg-card">
            <table className="w-full text-xs">
              <thead className="border-b border-border bg-secondary/50 text-left font-bold text-muted-foreground uppercase">
                <tr>
                  <th className="p-3">Ingredient</th>
                  <th className="p-3">Pack Format</th>
                  <th className="p-3 text-right">Pack Price</th>
                  <th className="p-3 text-right">Normalized Base Unit Rate</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {packsQuery.isLoading && (
                  <tr><td colSpan={4} className="p-4 text-center"><Loader2 className="h-4 w-4 animate-spin mx-auto text-primary" /></td></tr>
                )}
                {!packsQuery.isLoading && (packsQuery.data || []).length === 0 && (
                  <tr><td colSpan={4} className="p-4 text-center text-muted-foreground">No packs configured yet for this supplier.</td></tr>
                )}
                {packsQuery.data?.map(p => (
                  <tr key={p.id} className="hover:bg-secondary/20">
                    <td className="p-3 font-semibold">{p.ingredientName}</td>
                    <td className="p-3 text-muted-foreground">{p.packUnit} ({p.packQuantity} {p.packUnitType})</td>
                    <td className="p-3 text-right font-mono font-bold">₹{p.lastPurchasePricePerPack.toFixed(2)}</td>
                    <td className="p-3 text-right font-mono text-emerald-600 font-bold">
                      ₹{p.normalizedPricePerBaseUnit.toFixed(4)} / {p.baseUnit}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        <div className="mt-6 flex justify-end">
          <button onClick={() => void confirmClose(onClose)} className="rounded-full bg-secondary px-5 py-2 text-xs font-semibold">Close</button>
        </div>
      </div>
    </div>
  );
}

function PriceComparisonModal({ restaurantId, ingredientId, onClose }: { restaurantId: string; ingredientId: string; onClose: () => void }) {
  const query = useQuery({
    queryKey: ["price_comparison", restaurantId, ingredientId],
    queryFn: () => compareIngredientPrices(restaurantId, ingredientId),
  });

  const data: IngredientPriceComparison | undefined = query.data;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
      <div className="w-full max-w-lg rounded-3xl border border-border bg-card p-6 shadow-glow">
        <div className="flex items-center justify-between border-b border-border pb-3">
          <div>
            <h2 className="font-display text-lg font-bold">Supplier Price Comparison</h2>
            <p className="text-xs text-muted-foreground">
              {data ? `${data.ingredientName} (normalized to INR / ${data.baseUnit})` : "Loading..."}
            </p>
          </div>
          <button onClick={() => void confirmClose(onClose)} className="rounded-full p-1 text-muted-foreground hover:bg-secondary"><X className="h-5 w-5" /></button>
        </div>

        <div className="mt-4">
          {query.isLoading && (
            <div className="p-8 text-center"><Loader2 className="h-6 w-6 animate-spin mx-auto text-primary" /></div>
          )}
          {data && data.suppliers.length === 0 && (
            <div className="p-8 text-center text-sm text-muted-foreground">No suppliers currently configure this ingredient.</div>
          )}
          {data && data.suppliers.length > 0 && (
            <div className="space-y-3">
              {data.suppliers.map((s, idx) => (
                <div key={s.supplierId} className={`flex items-center justify-between rounded-2xl border p-3.5 ${idx === 0 ? "border-emerald-500/50 bg-emerald-500/10" : "border-border bg-card"}`}>
                  <div>
                    <div className="flex items-center gap-2">
                      <span className="font-bold text-sm">{s.supplierName}</span>
                      {idx === 0 && <span className="rounded-full bg-emerald-600 px-2 py-0.5 text-[10px] font-bold text-white">BEST RATE</span>}
                    </div>
                    <p className="text-xs text-muted-foreground mt-0.5">{s.packUnit} @ ₹{s.lastPricePerPack.toFixed(2)}</p>
                  </div>
                  <div className="text-right font-mono">
                    <p className="text-base font-bold text-foreground">₹{s.normalizedPricePerBaseUnit.toFixed(4)}</p>
                    <p className="text-[11px] text-muted-foreground">per {data.baseUnit}</p>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        <div className="mt-6 flex justify-end">
          <button onClick={() => void confirmClose(onClose)} className="rounded-full bg-secondary px-5 py-2 text-xs font-semibold">Close</button>
        </div>
      </div>
    </div>
  );
}

// ============================================================================
// 2. PURCHASING & RECEIVING TAB (PO, Goods Receipts, Returns)
// ============================================================================
export function PurchasingTab({ restaurantId, ingredients }: { restaurantId: string; ingredients: Ingredient[] }) {
  const cache = useQueryClient();
  const [subTab, setSubTab] = useState<"pos" | "invoices" | "returns">("pos");
  const [createPoModal, setCreatePoModal] = useState(false);
  const [receiveModal, setReceiveModal] = useState<{ po?: PurchaseOrder } | null>(null);
  const [returnModal, setReturnModal] = useState<{ invoice?: PurchaseInvoice } | null>(null);

  const posQuery = useQuery({ queryKey: ["purchase_orders", restaurantId], queryFn: () => getPurchaseOrders(restaurantId) });
  const invoicesQuery = useQuery({ queryKey: ["purchase_invoices", restaurantId], queryFn: () => getInvoices(restaurantId) });
  const returnsQuery = useQuery({ queryKey: ["purchase_returns", restaurantId], queryFn: () => getPurchaseReturns(restaurantId) });
  const suppliersQuery = useQuery({ queryKey: ["inventory_suppliers", restaurantId], queryFn: () => getSuppliers(restaurantId) });

  const suppliers = suppliersQuery.data || [];

  return (
    <div className="space-y-4">
      {/* Sub-tab Navigation */}
      <div className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-border bg-card p-3 shadow-soft">
        <div className="flex items-center gap-2">
          <button
            onClick={() => setSubTab("pos")}
            className={`rounded-full px-4 py-2 text-xs font-semibold transition ${subTab === "pos" ? "bg-primary text-primary-foreground" : "bg-secondary text-muted-foreground hover:bg-secondary/70"}`}
          >
            Purchase Orders ({posQuery.data?.length || 0})
          </button>
          <button
            onClick={() => setSubTab("invoices")}
            className={`rounded-full px-4 py-2 text-xs font-semibold transition ${subTab === "invoices" ? "bg-primary text-primary-foreground" : "bg-secondary text-muted-foreground hover:bg-secondary/70"}`}
          >
            Goods Receipts & Invoices ({invoicesQuery.data?.length || 0})
          </button>
          <button
            onClick={() => setSubTab("returns")}
            className={`rounded-full px-4 py-2 text-xs font-semibold transition ${subTab === "returns" ? "bg-primary text-primary-foreground" : "bg-secondary text-muted-foreground hover:bg-secondary/70"}`}
          >
            Purchase Returns ({returnsQuery.data?.length || 0})
          </button>
        </div>

        <div className="flex items-center gap-2">
          {subTab === "pos" && (
            <button
              onClick={() => setCreatePoModal(true)}
              className="inline-flex items-center gap-1.5 rounded-full bg-gradient-primary px-4 py-2 text-xs font-semibold text-primary-foreground shadow-glow"
            >
              <Plus className="h-3.5 w-3.5" /> Create Purchase Order
            </button>
          )}
          {subTab === "invoices" && (
            <button
              onClick={() => setReceiveModal({})}
              className="inline-flex items-center gap-1.5 rounded-full bg-gradient-primary px-4 py-2 text-xs font-semibold text-primary-foreground shadow-glow"
            >
              <ShoppingCart className="h-3.5 w-3.5" /> Direct Purchase Receipt (No PO)
            </button>
          )}
        </div>
      </div>

      {/* PO View */}
      {subTab === "pos" && (
        <div className="overflow-hidden rounded-3xl border border-border bg-card shadow-soft">
          <table className="w-full text-sm">
            <thead className="border-b border-border bg-secondary/50 text-left text-xs uppercase tracking-wider text-muted-foreground">
              <tr>
                <th className="p-4">PO Number</th>
                <th className="p-4">Supplier</th>
                <th className="p-4">Order Date</th>
                <th className="p-4">Lines / Items</th>
                <th className="p-4 text-right">Total Amount</th>
                <th className="p-4">Status</th>
                <th className="p-4 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {posQuery.isLoading && (
                <tr><td colSpan={7} className="p-8 text-center"><Loader2 className="h-5 w-5 animate-spin mx-auto text-primary" /></td></tr>
              )}
              {!posQuery.isLoading && (posQuery.data || []).length === 0 && (
                <tr><td colSpan={7} className="p-8 text-center text-sm text-muted-foreground">No purchase orders found.</td></tr>
              )}
              {posQuery.data?.map(po => (
                <tr key={po.id} className="hover:bg-secondary/20">
                  <td className="p-4 font-mono font-bold text-primary">{po.orderNumber}</td>
                  <td className="p-4 font-semibold">{po.supplierName}</td>
                  <td className="p-4 text-xs font-mono text-muted-foreground">{new Date(po.orderDateUtc).toLocaleDateString()}</td>
                  <td className="p-4 text-xs">
                    {po.items.map(i => `${i.orderedPackQuantity} ${i.packUnit} ${i.ingredientName}`).join(", ")}
                  </td>
                  <td className="p-4 text-right font-mono font-bold">₹{po.totalAmount.toFixed(2)}</td>
                  <td className="p-4">
                    <span className={`rounded-full px-2.5 py-0.5 text-xs font-bold ${
                      po.status === "Received" ? "bg-emerald-500/15 text-emerald-600"
                      : po.status === "Approved" ? "bg-blue-500/15 text-blue-600"
                      : po.status === "PartiallyReceived" ? "bg-amber-500/15 text-amber-600"
                      : "bg-secondary text-muted-foreground"
                    }`}>
                      {po.status}
                    </span>
                  </td>
                  <td className="p-4 text-right space-x-2">
                    {po.status === "Draft" && (
                      <button
                        onClick={async () => {
                          try {
                            await updatePurchaseOrderStatus(restaurantId, po.id, "Approved");
                            toast.success(`PO ${po.orderNumber} approved.`);
                            cache.invalidateQueries({ queryKey: ["purchase_orders", restaurantId] });
                          } catch (err: unknown) { toast.error(getErrorMessage(err)); }
                        }}
                        className="rounded-full bg-blue-600 px-3 py-1 text-xs font-bold text-white hover:bg-blue-700"
                      >
                        Approve PO
                      </button>
                    )}
                    {(po.status === "Approved" || po.status === "PartiallyReceived") && (
                      <button
                        onClick={() => setReceiveModal({ po })}
                        className="rounded-full bg-emerald-600 px-3 py-1 text-xs font-bold text-white hover:bg-emerald-700"
                      >
                        Receive Goods
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Invoices View */}
      {subTab === "invoices" && (
        <div className="overflow-hidden rounded-3xl border border-border bg-card shadow-soft">
          <table className="w-full text-sm">
            <thead className="border-b border-border bg-secondary/50 text-left text-xs uppercase tracking-wider text-muted-foreground">
              <tr>
                <th className="p-4">Invoice #</th>
                <th className="p-4">Supplier</th>
                <th className="p-4">Received Date</th>
                <th className="p-4">PO Ref</th>
                <th className="p-4">Received Items & Costs</th>
                <th className="p-4 text-right">Grand Total</th>
                <th className="p-4 text-right">Action</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {invoicesQuery.isLoading && (
                <tr><td colSpan={7} className="p-8 text-center"><Loader2 className="h-5 w-5 animate-spin mx-auto text-primary" /></td></tr>
              )}
              {!invoicesQuery.isLoading && (invoicesQuery.data || []).length === 0 && (
                <tr><td colSpan={7} className="p-8 text-center text-sm text-muted-foreground">No goods receipt invoices found.</td></tr>
              )}
              {invoicesQuery.data?.map(inv => (
                <tr key={inv.id} className="hover:bg-secondary/20">
                  <td className="p-4 font-mono font-bold text-foreground">{inv.invoiceNumber}</td>
                  <td className="p-4 font-semibold">{inv.supplierName}</td>
                  <td className="p-4 text-xs font-mono text-muted-foreground">{new Date(inv.receivedAtUtc).toLocaleDateString()}</td>
                  <td className="p-4 text-xs font-mono text-muted-foreground">{inv.purchaseOrderNumber || "Direct Purchase"}</td>
                  <td className="p-4 text-xs">
                    <div className="space-y-1">
                      {inv.items.map(i => (
                        <div key={i.id} className="flex items-center gap-2">
                          <span className="font-semibold">{i.receivedPackQuantity} {i.packUnit} {i.ingredientName}</span>
                          <span className="text-muted-foreground">({i.baseQuantity} {i.baseUnit} @ ₹{i.unitCostPerBaseUnit.toFixed(4)}/{i.baseUnit})</span>
                          {i.returnedBaseQuantity > 0 && (
                            <span className="rounded-full bg-destructive/15 px-1.5 py-0.2 text-[10px] font-bold text-destructive">
                              {i.returnedBaseQuantity} {i.baseUnit} returned
                            </span>
                          )}
                        </div>
                      ))}
                    </div>
                  </td>
                  <td className="p-4 text-right font-mono font-bold text-emerald-600">₹{inv.grandTotal.toFixed(2)}</td>
                  <td className="p-4 text-right">
                    <button
                      onClick={() => setReturnModal({ invoice: inv })}
                      className="rounded-full border border-border bg-background px-3 py-1 text-xs font-semibold hover:bg-destructive/10 hover:text-destructive"
                    >
                      Return Goods
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Returns View */}
      {subTab === "returns" && (
        <div className="overflow-hidden rounded-3xl border border-border bg-card shadow-soft">
          <table className="w-full text-sm">
            <thead className="border-b border-border bg-secondary/50 text-left text-xs uppercase tracking-wider text-muted-foreground">
              <tr>
                <th className="p-4">Return #</th>
                <th className="p-4">Supplier</th>
                <th className="p-4">Credit Note Ref</th>
                <th className="p-4">Reason</th>
                <th className="p-4">Items Returned</th>
                <th className="p-4 text-right">Total Credit</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {returnsQuery.isLoading && (
                <tr><td colSpan={6} className="p-8 text-center"><Loader2 className="h-5 w-5 animate-spin mx-auto text-primary" /></td></tr>
              )}
              {!returnsQuery.isLoading && (returnsQuery.data || []).length === 0 && (
                <tr><td colSpan={6} className="p-8 text-center text-sm text-muted-foreground">No purchase returns recorded.</td></tr>
              )}
              {returnsQuery.data?.map(ret => (
                <tr key={ret.id} className="hover:bg-secondary/20">
                  <td className="p-4 font-mono font-bold text-destructive">{ret.returnNumber}</td>
                  <td className="p-4 font-semibold">{ret.supplierName}</td>
                  <td className="p-4 font-mono text-xs">{ret.creditNoteReference}</td>
                  <td className="p-4 text-xs font-semibold text-muted-foreground">{ret.reason}</td>
                  <td className="p-4 text-xs">
                    {ret.items.map(i => `${i.returnQuantity} ${i.baseUnit} ${i.ingredientName}`).join(", ")}
                  </td>
                  <td className="p-4 text-right font-mono font-bold text-destructive">₹{ret.totalCreditAmount.toFixed(2)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {createPoModal && (
        <CreatePurchaseOrderModal
          restaurantId={restaurantId}
          suppliers={suppliers}
          ingredients={ingredients}
          onClose={() => setCreatePoModal(false)}
          onSuccess={() => {
            setCreatePoModal(false);
            cache.invalidateQueries({ queryKey: ["purchase_orders", restaurantId] });
          }}
        />
      )}

      {receiveModal && (
        <ReceiveGoodsModal
          restaurantId={restaurantId}
          po={receiveModal.po}
          suppliers={suppliers}
          ingredients={ingredients}
          onClose={() => setReceiveModal(null)}
          onSuccess={() => {
            setReceiveModal(null);
            cache.invalidateQueries({ queryKey: ["purchase_invoices", restaurantId] });
            cache.invalidateQueries({ queryKey: ["purchase_orders", restaurantId] });
            cache.invalidateQueries({ queryKey: ["inventory_ingredients", restaurantId] });
            cache.invalidateQueries({ queryKey: ["inventory_lots", restaurantId] });
          }}
        />
      )}

      {returnModal && returnModal.invoice && (
        <CreatePurchaseReturnModal
          restaurantId={restaurantId}
          invoice={returnModal.invoice}
          onClose={() => setReturnModal(null)}
          onSuccess={() => {
            setReturnModal(null);
            cache.invalidateQueries({ queryKey: ["purchase_returns", restaurantId] });
            cache.invalidateQueries({ queryKey: ["purchase_invoices", restaurantId] });
            cache.invalidateQueries({ queryKey: ["inventory_ingredients", restaurantId] });
            cache.invalidateQueries({ queryKey: ["inventory_lots", restaurantId] });
          }}
        />
      )}
    </div>
  );
}

function CreatePurchaseOrderModal({ restaurantId, suppliers, ingredients, onClose, onSuccess }: {
  restaurantId: string;
  suppliers: Supplier[];
  ingredients: Ingredient[];
  onClose: () => void;
  onSuccess: () => void;
}) {
  const [supplierId, setSupplierId] = useState(suppliers[0]?.id || "");
  const [deliveryDate, setDeliveryDate] = useState("");
  const [notes, setNotes] = useState("");
  const [lines, setLines] = useState<{ ingredientId: string; packUnit: string; orderedPackQuantity: number; unitPricePerPack: number; conversionFactor: number }[]>([
    { ingredientId: ingredients[0]?.id || "", packUnit: "25 kg sack", orderedPackQuantity: 1, unitPricePerPack: 2000, conversionFactor: 25000 }
  ]);
  const [loading, setLoading] = useState(false);

  const total = lines.reduce((acc, l) => acc + (l.orderedPackQuantity * l.unitPricePerPack), 0);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!supplierId) return toast.error("Please choose a supplier.");
    if (lines.length === 0) return toast.error("Add at least one line item.");
    setLoading(true);
    try {
      await createPurchaseOrder(restaurantId, {
        supplierId,
        expectedDeliveryDateUtc: deliveryDate ? new Date(deliveryDate).toISOString() : undefined,
        notes: notes.trim() || undefined,
        items: lines
      });
      toast.success("Purchase order created in Draft status.");
      onSuccess();
    } catch (err: unknown) {
      toast.error(getErrorMessage(err));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
      <div className="w-full max-w-2xl max-h-[90vh] overflow-y-auto rounded-3xl border border-border bg-card p-6 shadow-glow">
        <div className="flex items-center justify-between border-b border-border pb-3">
          <h2 className="font-display text-lg font-bold">New Purchase Order</h2>
          <button onClick={() => void confirmClose(onClose)} className="rounded-full p-1 text-muted-foreground hover:bg-secondary"><X className="h-5 w-5" /></button>
        </div>
        <form onSubmit={handleSubmit} className="mt-4 space-y-4 text-xs">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="font-bold text-muted-foreground">Supplier *</label>
              <select required value={supplierId} onChange={e => setSupplierId(e.target.value)} className="mt-1 w-full rounded-xl border border-border bg-background p-2 text-sm">
                <option value="">Select supplier…</option>
                {suppliers.filter(s => s.isActive).map(s => <option key={s.id} value={s.id}>{s.name}</option>)}
              </select>
            </div>
            <div>
              <label className="font-bold text-muted-foreground">Expected Delivery Date</label>
              <input type="date" value={deliveryDate} onChange={e => setDeliveryDate(e.target.value)} className="mt-1 w-full rounded-xl border border-border bg-background p-2 text-sm" />
            </div>
          </div>

          <div>
            <div className="flex items-center justify-between mb-2">
              <label className="font-bold uppercase tracking-wider text-muted-foreground">Line Items</label>
              <button
                type="button"
                onClick={() => setLines([...lines, { ingredientId: ingredients[0]?.id || "", packUnit: "Pack", orderedPackQuantity: 1, unitPricePerPack: 100, conversionFactor: 1 }])}
                className="rounded-full bg-secondary px-3 py-1 font-semibold hover:bg-secondary/70"
              >
                + Add Line
              </button>
            </div>
            <div className="space-y-2">
              {lines.map((line, idx) => (
                <div key={idx} className="grid grid-cols-12 gap-2 items-center rounded-xl border border-border bg-secondary/20 p-2.5">
                  <div className="col-span-4">
                    <select
                      value={line.ingredientId}
                      onChange={e => {
                        const copy = [...lines];
                        copy[idx].ingredientId = e.target.value;
                        setLines(copy);
                      }}
                      className="w-full rounded-lg border border-border bg-background p-1.5"
                    >
                      {ingredients.map(i => <option key={i.id} value={i.id}>{i.name} ({i.baseUnit})</option>)}
                    </select>
                  </div>
                  <div className="col-span-3">
                    <input
                      placeholder="Pack Unit (e.g. 25kg sack)"
                      value={line.packUnit}
                      onChange={e => {
                        const copy = [...lines];
                        copy[idx].packUnit = e.target.value;
                        setLines(copy);
                      }}
                      className="w-full rounded-lg border border-border bg-background p-1.5"
                    />
                  </div>
                  <div className="col-span-2">
                    <input
                      type="number"
                      step="any"
                      min="0.01"
                      placeholder="Qty"
                      value={line.orderedPackQuantity}
                      onChange={e => {
                        const copy = [...lines];
                        copy[idx].orderedPackQuantity = parseFloat(e.target.value) || 1;
                        setLines(copy);
                      }}
                      className="w-full rounded-lg border border-border bg-background p-1.5 font-mono"
                    />
                  </div>
                  <div className="col-span-2">
                    <input
                      type="number"
                      step="any"
                      placeholder="₹ Rate"
                      value={line.unitPricePerPack}
                      onChange={e => {
                        const copy = [...lines];
                        copy[idx].unitPricePerPack = parseFloat(e.target.value) || 0;
                        setLines(copy);
                      }}
                      className="w-full rounded-lg border border-border bg-background p-1.5 font-mono font-bold"
                    />
                  </div>
                  <div className="col-span-1 text-right">
                    {lines.length > 1 && (
                      <button type="button" onClick={() => setLines(lines.filter((_, i) => i !== idx))} className="text-destructive hover:bg-destructive/10 p-1 rounded-md">
                        <X className="h-4 w-4" />
                      </button>
                    )}
                  </div>
                </div>
              ))}
            </div>
          </div>

          <div>
            <label className="font-bold text-muted-foreground">Notes / Instructions</label>
            <input value={notes} onChange={e => setNotes(e.target.value)} placeholder="e.g. Deliver before 11 AM at rear entrance" className="mt-1 w-full rounded-xl border border-border bg-background p-2 text-sm" />
          </div>

          <div className="flex items-center justify-between border-t border-border pt-4">
            <span className="font-mono text-base font-bold">Total: ₹{total.toFixed(2)}</span>
            <div className="flex gap-2">
              <button type="button" onClick={() => void confirmClose(onClose)} className="rounded-full px-4 py-2 font-semibold text-muted-foreground hover:bg-secondary">Cancel</button>
              <button type="submit" disabled={loading} className="rounded-full bg-gradient-primary px-5 py-2 font-semibold text-primary-foreground">
                {loading ? "Creating…" : "Save Draft PO"}
              </button>
            </div>
          </div>
        </form>
      </div>
    </div>
  );
}

function ReceiveGoodsModal({ restaurantId, po, suppliers, ingredients, onClose, onSuccess }: {
  restaurantId: string;
  po?: PurchaseOrder;
  suppliers: Supplier[];
  ingredients: Ingredient[];
  onClose: () => void;
  onSuccess: () => void;
}) {
  const [supplierId, setSupplierId] = useState(po?.supplierId || suppliers[0]?.id || "");
  const [invoiceNumber, setInvoiceNumber] = useState("");
  const [shippingCharges, setShippingCharges] = useState(0);
  const [taxAmount, setTaxAmount] = useState(0);
  interface ReceiveLineState {
    ingredientId: string;
    purchaseOrderItemId?: string;
    packUnit: string;
    receivedPackQuantity: number;
    conversionFactor: number;
    packPrice: number;
    expiryDate: string;
    lotCode: string;
  }

  const [lines, setLines] = useState<ReceiveLineState[]>(
    po?.items.map(i => ({
      ingredientId: i.ingredientId,
      purchaseOrderItemId: i.id,
      packUnit: i.packUnit,
      receivedPackQuantity: i.orderedPackQuantity - i.receivedPackQuantity,
      conversionFactor: i.conversionFactor,
      packPrice: i.unitPricePerPack,
      expiryDate: "",
      lotCode: ""
    })) || [
      {
        ingredientId: ingredients[0]?.id || "",
        purchaseOrderItemId: undefined,
        packUnit: "10 kg crate",
        receivedPackQuantity: 1,
        conversionFactor: 10000,
        packPrice: 1000,
        expiryDate: "",
        lotCode: ""
      }
    ]
  );
  const [loading, setLoading] = useState(false);

  const subtotal = lines.reduce((acc, l) => acc + (l.receivedPackQuantity * l.packPrice), 0);
  const grandTotal = subtotal + shippingCharges + taxAmount;

  const handleReceive = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!invoiceNumber.trim()) return toast.error("Supplier Invoice Number is required.");
    setLoading(true);
    try {
      await receiveGoods(restaurantId, {
        operationId: crypto.randomUUID(),
        supplierId,
        invoiceNumber: invoiceNumber.trim(),
        invoiceDate: new Date().toISOString(),
        subtotal,
        discountAmount: 0,
        taxAmount,
        shippingCharges,
        grandTotal,
        purchaseOrderId: po?.id,
        items: lines.map(l => {
          // Proportionate landed freight/tax allocation
          const lineFreight = subtotal > 0 ? (shippingCharges * (l.receivedPackQuantity * l.packPrice)) / subtotal : 0;
          const lineTax = subtotal > 0 ? (taxAmount * (l.receivedPackQuantity * l.packPrice)) / subtotal : 0;
          const totalLineCost = (l.receivedPackQuantity * l.packPrice) + lineFreight + lineTax;
          return {
            ingredientId: l.ingredientId,
            purchaseOrderItemId: l.purchaseOrderItemId,
            packUnit: l.packUnit,
            receivedPackQuantity: l.receivedPackQuantity,
            conversionFactor: l.conversionFactor,
            packPrice: l.packPrice,
            lineDiscount: 0,
            lineTax,
            totalLineCost,
            expiryDate: l.expiryDate ? new Date(l.expiryDate).toISOString() : undefined,
            lotCode: l.lotCode.trim() || undefined
          };
        })
      });
      toast.success(`Goods received against Invoice ${invoiceNumber.trim()}. Stock and lots updated.`);
      onSuccess();
    } catch (err: unknown) {
      toast.error(getErrorMessage(err));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
      <div className="w-full max-w-3xl max-h-[90vh] overflow-y-auto rounded-3xl border border-border bg-card p-6 shadow-glow">
        <div className="flex items-center justify-between border-b border-border pb-3">
          <div>
            <h2 className="font-display text-lg font-bold">Receive Goods & Post Invoice</h2>
            <p className="text-xs text-muted-foreground">
              {po ? `Receiving against ${po.orderNumber} (${po.supplierName})` : "Direct Purchase Receipt without PO"}
            </p>
          </div>
          <button onClick={() => void confirmClose(onClose)} className="rounded-full p-1 text-muted-foreground hover:bg-secondary"><X className="h-5 w-5" /></button>
        </div>

        <form onSubmit={handleReceive} className="mt-4 space-y-4 text-xs">
          <div className="grid grid-cols-3 gap-3">
            <div>
              <label className="font-bold text-muted-foreground">Supplier *</label>
              <select required disabled={!!po} value={supplierId} onChange={e => setSupplierId(e.target.value)} className="mt-1 w-full rounded-xl border border-border bg-background p-2 text-sm">
                {suppliers.map(s => <option key={s.id} value={s.id}>{s.name}</option>)}
              </select>
            </div>
            <div>
              <label className="font-bold text-muted-foreground">Supplier Invoice # *</label>
              <input required value={invoiceNumber} onChange={e => setInvoiceNumber(e.target.value)} placeholder="e.g. INV-2026-9812" className="mt-1 w-full rounded-xl border border-border bg-background p-2 text-sm font-mono font-bold" />
            </div>
            <div>
              <label className="font-bold text-muted-foreground">Shipping / Freight (₹)</label>
              <input type="number" step="any" min="0" value={shippingCharges} onChange={e => setShippingCharges(parseFloat(e.target.value) || 0)} className="mt-1 w-full rounded-xl border border-border bg-background p-2 text-sm font-mono" />
            </div>
          </div>

          <div>
            <label className="font-bold uppercase tracking-wider text-muted-foreground mb-2 block">Received Line Items & Lots</label>
            <div className="space-y-3">
              {lines.map((l, idx) => (
                <div key={idx} className="rounded-2xl border border-border bg-secondary/20 p-3 space-y-2">
                  <div className="grid grid-cols-12 gap-2 items-center">
                    <div className="col-span-4">
                      <select
                        disabled={!!po}
                        value={l.ingredientId}
                        onChange={e => {
                          const copy = [...lines];
                          copy[idx].ingredientId = e.target.value;
                          setLines(copy);
                        }}
                        className="w-full rounded-lg border border-border bg-background p-1.5 font-semibold"
                      >
                        {ingredients.map(i => <option key={i.id} value={i.id}>{i.name} ({i.baseUnit})</option>)}
                      </select>
                    </div>
                    <div className="col-span-3">
                      <input
                        placeholder="Pack Unit"
                        value={l.packUnit}
                        onChange={e => {
                          const copy = [...lines];
                          copy[idx].packUnit = e.target.value;
                          setLines(copy);
                        }}
                        className="w-full rounded-lg border border-border bg-background p-1.5"
                      />
                    </div>
                    <div className="col-span-2">
                      <input
                        type="number"
                        step="any"
                        min="0.01"
                        placeholder="Received Qty"
                        value={l.receivedPackQuantity}
                        onChange={e => {
                          const copy = [...lines];
                          copy[idx].receivedPackQuantity = parseFloat(e.target.value) || 0;
                          setLines(copy);
                        }}
                        className="w-full rounded-lg border border-border bg-background p-1.5 font-mono"
                      />
                    </div>
                    <div className="col-span-3">
                      <input
                        type="number"
                        step="any"
                        placeholder="Price / Pack"
                        value={l.packPrice}
                        onChange={e => {
                          const copy = [...lines];
                          copy[idx].packPrice = parseFloat(e.target.value) || 0;
                          setLines(copy);
                        }}
                        className="w-full rounded-lg border border-border bg-background p-1.5 font-mono font-bold"
                      />
                    </div>
                  </div>

                  <div className="grid grid-cols-3 gap-2 pt-1 border-t border-border/50">
                    <div>
                      <span className="text-[10px] text-muted-foreground font-semibold">Conversion to Base Unit:</span>
                      <input
                        type="number"
                        step="any"
                        value={l.conversionFactor}
                        onChange={e => {
                          const copy = [...lines];
                          copy[idx].conversionFactor = parseFloat(e.target.value) || 1;
                          setLines(copy);
                        }}
                        placeholder="e.g. 25000 for 25kg->g"
                        className="mt-0.5 w-full rounded-lg border border-border bg-background p-1 font-mono text-[11px]"
                      />
                    </div>
                    <div>
                      <span className="text-[10px] text-muted-foreground font-semibold">Lot / Batch Code:</span>
                      <input
                        value={l.lotCode}
                        onChange={e => {
                          const copy = [...lines];
                          copy[idx].lotCode = e.target.value;
                          setLines(copy);
                        }}
                        placeholder="Auto-generated if empty"
                        className="mt-0.5 w-full rounded-lg border border-border bg-background p-1 font-mono text-[11px]"
                      />
                    </div>
                    <div>
                      <span className="text-[10px] text-muted-foreground font-semibold">Expiry Date (Optional):</span>
                      <input
                        type="date"
                        value={l.expiryDate}
                        onChange={e => {
                          const copy = [...lines];
                          copy[idx].expiryDate = e.target.value;
                          setLines(copy);
                        }}
                        className="mt-0.5 w-full rounded-lg border border-border bg-background p-1 text-[11px]"
                      />
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </div>

          <div className="flex items-center justify-between border-t border-border pt-4">
            <div>
              <p className="font-mono text-base font-bold text-emerald-600">Grand Total: ₹{grandTotal.toFixed(2)}</p>
              <p className="text-[11px] text-muted-foreground">Subtotal: ₹{subtotal.toFixed(2)} + Freight: ₹{shippingCharges.toFixed(2)}</p>
            </div>
            <div className="flex gap-2">
              <button type="button" onClick={() => void confirmClose(onClose)} className="rounded-full px-4 py-2 font-semibold text-muted-foreground hover:bg-secondary">Cancel</button>
              <button type="submit" disabled={loading} className="rounded-full bg-emerald-600 px-5 py-2 font-semibold text-white hover:bg-emerald-700">
                {loading ? "Posting Goods Receipt…" : "Post Goods Receipt & Update Stock"}
              </button>
            </div>
          </div>
        </form>
      </div>
    </div>
  );
}

function CreatePurchaseReturnModal({ restaurantId, invoice, onClose, onSuccess }: {
  restaurantId: string;
  invoice: PurchaseInvoice;
  onClose: () => void;
  onSuccess: () => void;
}) {
  const [selectedItemId, setSelectedItemId] = useState(invoice.items[0]?.id || "");
  const [returnQty, setReturnQty] = useState(0);
  const [creditNoteRef, setCreditNoteRef] = useState("");
  const [reason, setReason] = useState("Damaged pack");
  const [loading, setLoading] = useState(false);

  const item = invoice.items.find(i => i.id === selectedItemId);
  const maxReturnable = item ? item.baseQuantity - item.returnedBaseQuantity : 0;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!creditNoteRef.trim()) return toast.error("Supplier Credit Note Reference is required.");
    if (returnQty <= 0 || returnQty > maxReturnable) return toast.error(`Return quantity must be between 0.01 and ${maxReturnable}`);
    setLoading(true);
    try {
      await createPurchaseReturn(restaurantId, {
        operationId: crypto.randomUUID(),
        purchaseInvoiceId: invoice.id,
        creditNoteReference: creditNoteRef.trim(),
        reason: reason.trim(),
        items: [{ purchaseInvoiceItemId: selectedItemId, returnBaseQuantity: returnQty, reason }]
      });
      toast.success("Purchase return recorded and stock reversed.");
      onSuccess();
    } catch (err: unknown) {
      toast.error(getErrorMessage(err));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
      <div className="w-full max-w-lg rounded-3xl border border-border bg-card p-6 shadow-glow">
        <div className="flex items-center justify-between border-b border-border pb-3">
          <div>
            <h2 className="font-display text-lg font-bold">Return Goods to Supplier</h2>
            <p className="text-xs text-muted-foreground">Invoice #{invoice.invoiceNumber} · {invoice.supplierName}</p>
          </div>
          <button onClick={() => void confirmClose(onClose)} className="rounded-full p-1 text-muted-foreground hover:bg-secondary"><X className="h-5 w-5" /></button>
        </div>

        <form onSubmit={handleSubmit} className="mt-4 space-y-4 text-xs">
          <div>
            <label className="font-bold text-muted-foreground">Item to Return *</label>
            <select
              value={selectedItemId}
              onChange={e => {
                setSelectedItemId(e.target.value);
                setReturnQty(0);
              }}
              className="mt-1 w-full rounded-xl border border-border bg-background p-2.5 text-sm font-semibold"
            >
              {invoice.items.map(i => (
                <option key={i.id} value={i.id}>
                  {i.ingredientName} (Remaining returnable: {i.baseQuantity - i.returnedBaseQuantity} {i.baseUnit})
                </option>
              ))}
            </select>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="font-bold text-muted-foreground">Return Quantity ({item?.baseUnit}) *</label>
              <input
                type="number"
                step="any"
                min="0.01"
                max={maxReturnable}
                required
                value={returnQty}
                onChange={e => setReturnQty(parseFloat(e.target.value) || 0)}
                className="mt-1 w-full rounded-xl border border-border bg-background p-2 text-sm font-mono font-bold"
              />
              <span className="text-[10px] text-muted-foreground mt-0.5 block">Max returnable: {maxReturnable} {item?.baseUnit}</span>
            </div>
            <div>
              <label className="font-bold text-muted-foreground">Supplier Credit Note Ref *</label>
              <input
                required
                value={creditNoteRef}
                onChange={e => setCreditNoteRef(e.target.value)}
                placeholder="e.g. CN-MRM-2026-01"
                className="mt-1 w-full rounded-xl border border-border bg-background p-2 text-sm font-mono font-bold"
              />
            </div>
          </div>

          <div>
            <label className="font-bold text-muted-foreground">Return Reason</label>
            <select value={reason} onChange={e => setReason(e.target.value)} className="mt-1 w-full rounded-xl border border-border bg-background p-2 text-sm">
              <option value="Damaged pack">Damaged packaging</option>
              <option value="Quality issue / Spoiled">Quality issue / Spoiled</option>
              <option value="Short expiry date">Short expiry date</option>
              <option value="Wrong item delivered">Wrong item delivered</option>
              <option value="Over-shipped quantity">Over-shipped quantity</option>
            </select>
          </div>

          <div className="rounded-xl border border-border bg-secondary/30 p-3">
            <span className="text-xs text-muted-foreground">Estimated Supplier Credit Amount: </span>
            <span className="font-mono text-sm font-bold text-destructive">
              ₹{((returnQty * (item?.unitCostPerBaseUnit || 0))).toFixed(2)}
            </span>
          </div>

          <div className="flex justify-end gap-2 pt-2">
            <button type="button" onClick={() => void confirmClose(onClose)} className="rounded-full px-4 py-2 font-semibold text-muted-foreground hover:bg-secondary">Cancel</button>
            <button type="submit" disabled={loading || returnQty <= 0} className="rounded-full bg-destructive px-5 py-2 font-semibold text-destructive-foreground">
              {loading ? "Processing…" : "Confirm Return & Reverse Stock"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

// ============================================================================
// 3. PREPARATIONS TAB (Intermediate Prepared Ingredients & Batch Execution)
// ============================================================================
export function PreparationsTab({ restaurantId, ingredients }: { restaurantId: string; ingredients: Ingredient[] }) {
  const cache = useQueryClient();
  const [recipeModal, setRecipeModal] = useState(false);
  const [batchModal, setBatchModal] = useState<PreparationRecipe | null>(null);

  const recipesQuery = useQuery({ queryKey: ["prep_recipes", restaurantId], queryFn: () => getPreparationRecipes(restaurantId) });
  const batchesQuery = useQuery({ queryKey: ["prep_batches", restaurantId], queryFn: () => getPreparationBatches(restaurantId) });

  return (
    <div className="space-y-6">
      {/* Header action */}
      <div className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-border bg-card p-3 shadow-soft">
        <div>
          <p className="font-bold text-sm">Intermediate Preparations</p>
          <p className="text-xs text-muted-foreground">Manage prepared spices, gravies, and marinades that consume raw inputs and become stockable lots.</p>
        </div>
        <button
          onClick={() => setRecipeModal(true)}
          className="inline-flex items-center gap-1.5 rounded-full bg-gradient-primary px-4 py-2 text-xs font-semibold text-primary-foreground shadow-glow"
        >
          <Plus className="h-3.5 w-3.5" /> Define Preparation Recipe
        </button>
      </div>

      {/* Preparation Recipes List */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {recipesQuery.isLoading && (
          <div className="col-span-2 p-8 text-center"><Loader2 className="h-6 w-6 animate-spin mx-auto text-primary" /></div>
        )}
        {!recipesQuery.isLoading && (recipesQuery.data || []).length === 0 && (
          <div className="col-span-2 rounded-3xl border border-border bg-card p-8 text-center text-sm text-muted-foreground">
            No preparation recipes defined. Create items like &quot;Biryani Masala&quot; or &quot;Base Gravy&quot;.
          </div>
        )}
        {recipesQuery.data?.map(recipe => {
          const totalCost = recipe.inputs.reduce((acc, i) => acc + i.extendedCost, 0);
          const costPerUnit = recipe.expectedYieldQuantity > 0 ? totalCost / recipe.expectedYieldQuantity : 0;
          return (
            <div key={recipe.id} className="rounded-3xl border border-border bg-card p-5 shadow-soft space-y-4">
              <div className="flex items-start justify-between">
                <div>
                  <h3 className="font-display text-base font-bold text-foreground">{recipe.outputIngredientName}</h3>
                  <p className="text-xs text-muted-foreground">Standard Yield: {recipe.expectedYieldQuantity} {recipe.outputBaseUnit}</p>
                </div>
                <button
                  onClick={() => setBatchModal(recipe)}
                  className="rounded-full bg-primary px-4 py-1.5 text-xs font-semibold text-primary-foreground hover:bg-primary/90"
                >
                  Cook / Prepare Batch
                </button>
              </div>

              <div>
                <p className="text-[11px] font-bold uppercase tracking-wider text-muted-foreground mb-1.5">Raw Ingredients Required</p>
                <div className="space-y-1 text-xs">
                  {recipe.inputs.map(inp => (
                    <div key={inp.id} className="flex justify-between items-center text-muted-foreground">
                      <span>{inp.inputIngredientName} ({inp.quantity} {inp.inputBaseUnit} @ {inp.yieldPercent}% yield)</span>
                      <span className="font-mono text-foreground font-semibold">₹{inp.extendedCost.toFixed(2)}</span>
                    </div>
                  ))}
                </div>
              </div>

              <div className="flex justify-between items-center border-t border-border pt-3 text-xs font-mono">
                <span className="text-muted-foreground">Cost per {recipe.outputBaseUnit}:</span>
                <span className="font-bold text-emerald-600">₹{costPerUnit.toFixed(4)} / {recipe.outputBaseUnit}</span>
              </div>
            </div>
          );
        })}
      </div>

      {/* Batches Log */}
      <div className="space-y-3">
        <p className="font-bold text-sm">Recent Preparation Batches Log</p>
        <div className="overflow-hidden rounded-3xl border border-border bg-card shadow-soft">
          <table className="w-full text-xs">
            <thead className="border-b border-border bg-secondary/50 text-left uppercase tracking-wider text-muted-foreground font-bold">
              <tr>
                <th className="p-3">Batch Date</th>
                <th className="p-3">Prepared Item</th>
                <th className="p-3">Lot Code</th>
                <th className="p-3 text-right">Multiplier</th>
                <th className="p-3 text-right">Actual Yield</th>
                <th className="p-3 text-right">Waste</th>
                <th className="p-3 text-right">Input Cost</th>
                <th className="p-3 text-right">Unit Cost Basis</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {batchesQuery.isLoading && (
                <tr><td colSpan={8} className="p-6 text-center"><Loader2 className="h-4 w-4 animate-spin mx-auto text-primary" /></td></tr>
              )}
              {!batchesQuery.isLoading && (batchesQuery.data || []).length === 0 && (
                <tr><td colSpan={8} className="p-6 text-center text-muted-foreground">No batch production recorded yet.</td></tr>
              )}
              {batchesQuery.data?.map(b => (
                <tr key={b.id} className="hover:bg-secondary/20">
                  <td className="p-3 font-mono text-muted-foreground">{new Date(b.preparedAtUtc).toLocaleString()}</td>
                  <td className="p-3 font-bold text-foreground">{b.outputIngredientName}</td>
                  <td className="p-3 font-mono text-primary font-bold">{b.lotCode || "—"}</td>
                  <td className="p-3 text-right font-mono">{b.multiplier}x</td>
                  <td className="p-3 text-right font-mono font-bold">{b.actualYieldQuantity} {b.outputBaseUnit}</td>
                  <td className="p-3 text-right font-mono text-muted-foreground">
                    {b.wasteQuantity > 0 ? `${b.wasteQuantity} ${b.outputBaseUnit}` : "—"}
                  </td>
                  <td className="p-3 text-right font-mono font-semibold">₹{b.totalInputCost.toFixed(2)}</td>
                  <td className="p-3 text-right font-mono font-bold text-emerald-600">
                    ₹{b.costPerBaseUnit.toFixed(4)} / {b.outputBaseUnit}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {recipeModal && (
        <SavePreparationRecipeModal
          restaurantId={restaurantId}
          ingredients={ingredients}
          onClose={() => setRecipeModal(false)}
          onSuccess={() => {
            setRecipeModal(false);
            cache.invalidateQueries({ queryKey: ["prep_recipes", restaurantId] });
            cache.invalidateQueries({ queryKey: ["inventory_ingredients", restaurantId] });
          }}
        />
      )}

      {batchModal && (
        <RecordBatchModal
          restaurantId={restaurantId}
          recipe={batchModal}
          onClose={() => setBatchModal(null)}
          onSuccess={() => {
            setBatchModal(null);
            cache.invalidateQueries({ queryKey: ["prep_batches", restaurantId] });
            cache.invalidateQueries({ queryKey: ["inventory_ingredients", restaurantId] });
            cache.invalidateQueries({ queryKey: ["inventory_lots", restaurantId] });
          }}
        />
      )}
    </div>
  );
}

function SavePreparationRecipeModal({ restaurantId, ingredients, onClose, onSuccess }: {
  restaurantId: string;
  ingredients: Ingredient[];
  onClose: () => void;
  onSuccess: () => void;
}) {
  const [outputId, setOutputId] = useState("");
  const [expectedYield, setExpectedYield] = useState(500);
  const [notes, setNotes] = useState("");
  const [inputs, setInputs] = useState<{ inputIngredientId: string; quantity: number; yieldPercent: number }[]>([
    { inputIngredientId: ingredients[0]?.id || "", quantity: 100, yieldPercent: 100 }
  ]);
  const [saving, setSaving] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!outputId) return toast.error("Please choose output ingredient.");
    setSaving(true);
    try {
      await savePreparationRecipe(restaurantId, {
        outputIngredientId: outputId,
        expectedYieldQuantity: expectedYield,
        notes: notes.trim() || undefined,
        inputs
      });
      toast.success("Preparation recipe saved.");
      onSuccess();
    } catch (err: unknown) {
      toast.error(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
      <div className="w-full max-w-xl max-h-[90vh] overflow-y-auto rounded-3xl border border-border bg-card p-6 shadow-glow">
        <div className="flex items-center justify-between border-b border-border pb-3">
          <h2 className="font-display text-lg font-bold">Define Preparation Recipe</h2>
          <button onClick={() => void confirmClose(onClose)} className="rounded-full p-1 text-muted-foreground hover:bg-secondary"><X className="h-5 w-5" /></button>
        </div>

        <form onSubmit={handleSubmit} className="mt-4 space-y-4 text-xs">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="font-bold text-muted-foreground">Output Prepared Ingredient *</label>
              <select required value={outputId} onChange={e => setOutputId(e.target.value)} className="mt-1 w-full rounded-xl border border-border bg-background p-2.5 text-sm">
                <option value="">Select or create ingredient…</option>
                {ingredients.map(i => <option key={i.id} value={i.id}>{i.name} ({i.baseUnit})</option>)}
              </select>
            </div>
            <div>
              <label className="font-bold text-muted-foreground">Expected Standard Yield *</label>
              <input type="number" step="any" min="0.01" required value={expectedYield} onChange={e => setExpectedYield(parseFloat(e.target.value) || 1)} className="mt-1 w-full rounded-xl border border-border bg-background p-2.5 text-sm font-mono font-bold" />
            </div>
          </div>

          <div>
            <div className="flex items-center justify-between mb-2">
              <label className="font-bold uppercase tracking-wider text-muted-foreground">Raw Input Ingredients</label>
              <button
                type="button"
                onClick={() => setInputs([...inputs, { inputIngredientId: ingredients[0]?.id || "", quantity: 50, yieldPercent: 100 }])}
                className="rounded-full bg-secondary px-3 py-1 font-semibold hover:bg-secondary/70"
              >
                + Add Input
              </button>
            </div>
            <div className="space-y-2">
              {inputs.map((inp, idx) => (
                <div key={idx} className="grid grid-cols-12 gap-2 items-center rounded-xl border border-border bg-secondary/20 p-2.5">
                  <div className="col-span-6">
                    <select
                      value={inp.inputIngredientId}
                      onChange={e => {
                        const copy = [...inputs];
                        copy[idx].inputIngredientId = e.target.value;
                        setInputs(copy);
                      }}
                      className="w-full rounded-lg border border-border bg-background p-1.5 font-semibold"
                    >
                      {ingredients.filter(i => i.id !== outputId).map(i => (
                        <option key={i.id} value={i.id}>{i.name} ({i.baseUnit})</option>
                      ))}
                    </select>
                  </div>
                  <div className="col-span-3">
                    <input
                      type="number"
                      step="any"
                      placeholder="Quantity"
                      value={inp.quantity}
                      onChange={e => {
                        const copy = [...inputs];
                        copy[idx].quantity = parseFloat(e.target.value) || 0;
                        setInputs(copy);
                      }}
                      className="w-full rounded-lg border border-border bg-background p-1.5 font-mono"
                    />
                  </div>
                  <div className="col-span-2">
                    <input
                      type="number"
                      step="any"
                      placeholder="Yield %"
                      value={inp.yieldPercent}
                      onChange={e => {
                        const copy = [...inputs];
                        copy[idx].yieldPercent = parseFloat(e.target.value) || 100;
                        setInputs(copy);
                      }}
                      className="w-full rounded-lg border border-border bg-background p-1.5 font-mono"
                    />
                  </div>
                  <div className="col-span-1 text-right">
                    {inputs.length > 1 && (
                      <button type="button" onClick={() => setInputs(inputs.filter((_, i) => i !== idx))} className="text-destructive p-1 rounded-md">
                        <X className="h-4 w-4" />
                      </button>
                    )}
                  </div>
                </div>
              ))}
            </div>
          </div>

          <div className="flex justify-end gap-2 pt-2 border-t border-border">
            <button type="button" onClick={() => void confirmClose(onClose)} className="rounded-full px-4 py-2 font-semibold text-muted-foreground hover:bg-secondary">Cancel</button>
            <button type="submit" disabled={saving || !outputId} className="rounded-full bg-gradient-primary px-5 py-2 font-semibold text-primary-foreground">
              {saving ? "Saving…" : "Save Preparation Recipe"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

function RecordBatchModal({ restaurantId, recipe, onClose, onSuccess }: {
  restaurantId: string;
  recipe: PreparationRecipe;
  onClose: () => void;
  onSuccess: () => void;
}) {
  const [multiplier, setMultiplier] = useState(1);
  const [actualYield, setActualYield] = useState(recipe.expectedYieldQuantity);
  const [wasteQty, setWasteQty] = useState(0);
  const [wasteReason, setWasteReason] = useState("");
  const [expiryDate, setExpiryDate] = useState("");
  const [saving, setSaving] = useState(false);

  const expectedTotal = recipe.expectedYieldQuantity * multiplier;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (actualYield <= 0) return toast.error("Actual yield must be greater than zero.");
    setSaving(true);
    try {
      await recordPreparationBatch(restaurantId, {
        operationId: crypto.randomUUID(),
        preparationRecipeId: recipe.id,
        multiplier,
        actualYieldQuantity: actualYield,
        wasteQuantity: wasteQty,
        wasteReason: wasteReason.trim() || undefined,
        expiryDate: expiryDate ? new Date(expiryDate).toISOString() : undefined
      });
      toast.success(`Produced ${actualYield} ${recipe.outputBaseUnit} of ${recipe.outputIngredientName}. Raw ingredients deducted.`);
      onSuccess();
    } catch (err: unknown) {
      toast.error(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
      <div className="w-full max-w-lg rounded-3xl border border-border bg-card p-6 shadow-glow">
        <div className="flex items-center justify-between border-b border-border pb-3">
          <div>
            <h2 className="font-display text-lg font-bold">Cook / Prepare Batch</h2>
            <p className="text-xs text-muted-foreground">{recipe.outputIngredientName} (Standard: {recipe.expectedYieldQuantity} {recipe.outputBaseUnit})</p>
          </div>
          <button onClick={() => void confirmClose(onClose)} className="rounded-full p-1 text-muted-foreground hover:bg-secondary"><X className="h-5 w-5" /></button>
        </div>

        <form onSubmit={handleSubmit} className="mt-4 space-y-4 text-xs">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="font-bold text-muted-foreground">Batch Multiplier (e.g. 1x, 2x)</label>
              <input
                type="number"
                step="0.1"
                min="0.1"
                required
                value={multiplier}
                onChange={e => {
                  const m = parseFloat(e.target.value) || 1;
                  setMultiplier(m);
                  setActualYield(recipe.expectedYieldQuantity * m);
                }}
                className="mt-1 w-full rounded-xl border border-border bg-background p-2 text-sm font-mono font-bold"
              />
            </div>
            <div>
              <label className="font-bold text-muted-foreground">Actual Measured Yield ({recipe.outputBaseUnit}) *</label>
              <input
                type="number"
                step="any"
                min="0.01"
                required
                value={actualYield}
                onChange={e => setActualYield(parseFloat(e.target.value) || 0)}
                className="mt-1 w-full rounded-xl border border-border bg-background p-2 text-sm font-mono font-bold"
              />
              <span className="text-[10px] text-muted-foreground mt-0.5 block">Expected: {expectedTotal} {recipe.outputBaseUnit}</span>
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="font-bold text-muted-foreground">Preparation Waste ({recipe.outputBaseUnit})</label>
              <input
                type="number"
                step="any"
                min="0"
                value={wasteQty}
                onChange={e => setWasteQty(parseFloat(e.target.value) || 0)}
                className="mt-1 w-full rounded-xl border border-border bg-background p-2 text-sm font-mono"
              />
            </div>
            <div>
              <label className="font-bold text-muted-foreground">Waste Reason</label>
              <input
                value={wasteReason}
                onChange={e => setWasteReason(e.target.value)}
                placeholder="e.g. Grinding loss / burned"
                className="mt-1 w-full rounded-xl border border-border bg-background p-2 text-sm"
              />
            </div>
          </div>

          <div>
            <label className="font-bold text-muted-foreground">Expiry Date for this Prepared Lot</label>
            <input type="date" value={expiryDate} onChange={e => setExpiryDate(e.target.value)} className="mt-1 w-full rounded-xl border border-border bg-background p-2 text-sm" />
          </div>

          <div className="rounded-xl border border-border bg-secondary/20 p-3">
            <p className="font-bold text-muted-foreground uppercase text-[10px] mb-1">Inputs to Deduct from Raw Stock</p>
            <div className="space-y-0.5">
              {recipe.inputs.map(i => (
                <div key={i.id} className="flex justify-between text-muted-foreground">
                  <span>{i.inputIngredientName}</span>
                  <span className="font-mono text-foreground font-semibold">{(i.quantity * multiplier).toFixed(1)} {i.inputBaseUnit}</span>
                </div>
              ))}
            </div>
          </div>

          <div className="flex justify-end gap-2 pt-2 border-t border-border">
            <button type="button" onClick={() => void confirmClose(onClose)} className="rounded-full px-4 py-2 font-semibold text-muted-foreground hover:bg-secondary">Cancel</button>
            <button type="submit" disabled={saving} className="rounded-full bg-gradient-primary px-5 py-2 font-semibold text-primary-foreground">
              {saving ? "Posting Batch…" : "Confirm Production & Update Stock"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

// ============================================================================
// 4. LOTS & EXPIRY DASHBOARD (FEFO picking, Expiry, Quarantine, Write-off)
// ============================================================================
export function LotsAndExpiryTab({ restaurantId }: { restaurantId: string }) {
  const cache = useQueryClient();
  const [filter, setFilter] = useState<"all" | "expiringSoon" | "quarantined">("all");
  const [quarantineModal, setQuarantineModal] = useState<StockLot | null>(null);
  const [writeOffModal, setWriteOffModal] = useState<StockLot | null>(null);

  const lotsQuery = useQuery({
    queryKey: ["inventory_lots", restaurantId, filter],
    queryFn: () => getLots(
      restaurantId,
      filter === "quarantined" ? "Quarantined" : undefined,
      filter === "expiringSoon" ? true : undefined
    ),
  });

  const lots = lotsQuery.data || [];
  const expiredCount = lots.filter(l => l.isExpired).length;
  const expiringSoonCount = lots.filter(l => l.isExpiringSoon).length;

  return (
    <div className="space-y-4">
      {/* Alert Banner */}
      {(expiredCount > 0 || expiringSoonCount > 0) && (
        <div className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-destructive/40 bg-destructive/10 p-4">
          <div className="flex items-center gap-3">
            <AlertTriangle className="h-5 w-5 text-destructive shrink-0" />
            <div>
              <p className="font-bold text-sm text-destructive">Expiry Attention Required</p>
              <p className="text-xs text-muted-foreground">
                {expiredCount > 0 && `${expiredCount} lot${expiredCount === 1 ? "" : "s"} expired. `}
                {expiringSoonCount > 0 && `${expiringSoonCount} lot${expiringSoonCount === 1 ? "" : "s"} expiring within 3 days.`}
              </p>
            </div>
          </div>
        </div>
      )}

      {/* Filter Tabs */}
      <div className="flex items-center gap-2">
        <button
          onClick={() => setFilter("all")}
          className={`rounded-full px-4 py-2 text-xs font-semibold ${filter === "all" ? "bg-foreground text-background" : "bg-card text-muted-foreground hover:bg-secondary"}`}
        >
          Active Lots ({lots.length})
        </button>
        <button
          onClick={() => setFilter("expiringSoon")}
          className={`rounded-full px-4 py-2 text-xs font-semibold ${filter === "expiringSoon" ? "bg-amber-600 text-white" : "bg-card text-muted-foreground hover:bg-secondary"}`}
        >
          Expiring Soon
        </button>
        <button
          onClick={() => setFilter("quarantined")}
          className={`rounded-full px-4 py-2 text-xs font-semibold ${filter === "quarantined" ? "bg-destructive text-destructive-foreground" : "bg-card text-muted-foreground hover:bg-secondary"}`}
        >
          Quarantined
        </button>
      </div>

      <div className="overflow-hidden rounded-3xl border border-border bg-card shadow-soft">
        <table className="w-full text-sm">
          <thead className="border-b border-border bg-secondary/50 text-left text-xs uppercase tracking-wider text-muted-foreground">
            <tr>
              <th className="p-4">Lot Code</th>
              <th className="p-4">Ingredient</th>
              <th className="p-4">Source</th>
              <th className="p-4 text-right">Available Qty</th>
              <th className="p-4 text-right">Unit Cost</th>
              <th className="p-4">Expiry Date</th>
              <th className="p-4">Status</th>
              <th className="p-4 text-right">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {lotsQuery.isLoading && (
              <tr><td colSpan={8} className="p-8 text-center"><Loader2 className="h-5 w-5 animate-spin mx-auto text-primary" /></td></tr>
            )}
            {!lotsQuery.isLoading && lots.length === 0 && (
              <tr><td colSpan={8} className="p-8 text-center text-sm text-muted-foreground">No lots found matching criteria.</td></tr>
            )}
            {lots.map(l => (
              <tr key={l.id} className="hover:bg-secondary/20">
                <td className="p-4 font-mono font-bold text-primary">{l.lotCode}</td>
                <td className="p-4 font-semibold">{l.ingredientName}</td>
                <td className="p-4 text-xs font-mono text-muted-foreground">{l.sourceType} {l.sourceReferenceId ? `(${l.sourceReferenceId})` : ""}</td>
                <td className="p-4 text-right font-mono font-bold">{l.availableQuantity} / {l.initialQuantity} {l.baseUnit}</td>
                <td className="p-4 text-right font-mono font-semibold">₹{l.unitCost.toFixed(4)}</td>
                <td className="p-4 text-xs font-mono">
                  {l.expiryDate ? (
                    <span className={l.isExpired ? "font-bold text-destructive" : l.isExpiringSoon ? "font-bold text-amber-600" : "text-muted-foreground"}>
                      {new Date(l.expiryDate).toLocaleDateString()}
                    </span>
                  ) : "—"}
                </td>
                <td className="p-4">
                  <span className={`rounded-full px-2.5 py-0.5 text-xs font-bold ${
                    l.status === "Active" ? "bg-emerald-500/15 text-emerald-600"
                    : l.status === "Quarantined" ? "bg-amber-500/15 text-amber-600"
                    : "bg-destructive/15 text-destructive"
                  }`}>
                    {l.status}
                  </span>
                </td>
                <td className="p-4 text-right space-x-2">
                  {l.status === "Active" && (
                    <button
                      onClick={() => setQuarantineModal(l)}
                      className="rounded-full border border-border bg-background px-3 py-1 text-xs font-semibold hover:bg-amber-500/10 hover:text-amber-600"
                    >
                      Quarantine
                    </button>
                  )}
                  {l.availableQuantity > 0 && (
                    <button
                      onClick={() => setWriteOffModal(l)}
                      className="rounded-full border border-border bg-background px-3 py-1 text-xs font-semibold hover:bg-destructive/10 hover:text-destructive"
                    >
                      Write-off
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {quarantineModal && (
        <QuarantineLotModal
          restaurantId={restaurantId}
          lot={quarantineModal}
          onClose={() => setQuarantineModal(null)}
          onSuccess={() => {
            setQuarantineModal(null);
            cache.invalidateQueries({ queryKey: ["inventory_lots", restaurantId] });
          }}
        />
      )}

      {writeOffModal && (
        <WriteOffLotModal
          restaurantId={restaurantId}
          lot={writeOffModal}
          onClose={() => setWriteOffModal(null)}
          onSuccess={() => {
            setWriteOffModal(null);
            cache.invalidateQueries({ queryKey: ["inventory_lots", restaurantId] });
            cache.invalidateQueries({ queryKey: ["inventory_ingredients", restaurantId] });
          }}
        />
      )}
    </div>
  );
}

function QuarantineLotModal({ restaurantId, lot, onClose, onSuccess }: {
  restaurantId: string;
  lot: StockLot;
  onClose: () => void;
  onSuccess: () => void;
}) {
  const [reason, setReason] = useState("Suspected contamination / off-odour");
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    try {
      await quarantineLot(restaurantId, lot.id, reason);
      toast.success(`Lot ${lot.lotCode} quarantined.`);
      onSuccess();
    } catch (err: unknown) {
      toast.error(getErrorMessage(err));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
      <div className="w-full max-w-md rounded-3xl border border-border bg-card p-6 shadow-glow">
        <h2 className="font-display text-lg font-bold">Quarantine Lot: {lot.lotCode}</h2>
        <p className="mt-1 text-xs text-muted-foreground">{lot.ingredientName} ({lot.availableQuantity} {lot.baseUnit} available). Quarantined lots are excluded from menu availability.</p>
        <form onSubmit={handleSubmit} className="mt-4 space-y-4 text-xs">
          <div>
            <label className="font-bold text-muted-foreground">Reason for Quarantine *</label>
            <input required value={reason} onChange={e => setReason(e.target.value)} className="mt-1 w-full rounded-xl border border-border bg-background p-2.5 text-sm" />
          </div>
          <div className="flex justify-end gap-2 pt-2">
            <button type="button" onClick={() => void confirmClose(onClose)} className="rounded-full px-4 py-2 font-semibold text-muted-foreground hover:bg-secondary">Cancel</button>
            <button type="submit" disabled={loading} className="rounded-full bg-amber-600 px-5 py-2 font-semibold text-white hover:bg-amber-700">
              {loading ? "Quarantining…" : "Confirm Quarantine"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

function WriteOffLotModal({ restaurantId, lot, onClose, onSuccess }: {
  restaurantId: string;
  lot: StockLot;
  onClose: () => void;
  onSuccess: () => void;
}) {
  const [quantity, setQuantity] = useState(lot.availableQuantity);
  const [reason, setReason] = useState("Spoiled / Expired");
  const [notes, setNotes] = useState("");
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (quantity <= 0 || quantity > lot.availableQuantity) return toast.error(`Write-off quantity must be between 0.01 and ${lot.availableQuantity}`);
    setLoading(true);
    try {
      await writeOffLot(restaurantId, lot.id, quantity, reason, notes.trim() || undefined);
      toast.success(`Wrote off ${quantity} ${lot.baseUnit} from lot ${lot.lotCode}. Loss recorded in ledger.`);
      onSuccess();
    } catch (err: unknown) {
      toast.error(getErrorMessage(err));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
      <div className="w-full max-w-md rounded-3xl border border-border bg-card p-6 shadow-glow">
        <h2 className="font-display text-lg font-bold text-destructive">Write Off Lot: {lot.lotCode}</h2>
        <p className="mt-1 text-xs text-muted-foreground">{lot.ingredientName} · Unit cost basis: ₹{lot.unitCost.toFixed(4)}/{lot.baseUnit}</p>
        <form onSubmit={handleSubmit} className="mt-4 space-y-4 text-xs">
          <div>
            <label className="font-bold text-muted-foreground">Quantity to Write Off ({lot.baseUnit}) *</label>
            <input
              type="number"
              step="any"
              min="0.01"
              max={lot.availableQuantity}
              required
              value={quantity}
              onChange={e => setQuantity(parseFloat(e.target.value) || 0)}
              className="mt-1 w-full rounded-xl border border-border bg-background p-2.5 text-sm font-mono font-bold"
            />
            <span className="text-[10px] text-muted-foreground mt-0.5 block">Max available: {lot.availableQuantity} {lot.baseUnit}</span>
          </div>
          <div>
            <label className="font-bold text-muted-foreground">Reason *</label>
            <select value={reason} onChange={e => setReason(e.target.value)} className="mt-1 w-full rounded-xl border border-border bg-background p-2.5 text-sm">
              <option value="Spoiled / Expired">Spoiled / Expired</option>
              <option value="Torn / Damaged packaging">Torn / Damaged packaging</option>
              <option value="Contaminated">Contaminated</option>
              <option value="Kitchen spill">Kitchen spill</option>
            </select>
          </div>
          <div>
            <label className="font-bold text-muted-foreground">Notes</label>
            <input value={notes} onChange={e => setNotes(e.target.value)} placeholder="Audit notes..." className="mt-1 w-full rounded-xl border border-border bg-background p-2.5 text-sm" />
          </div>
          <div className="rounded-xl border border-border bg-secondary/30 p-3">
            <span className="text-xs text-muted-foreground">Inventory Valuation Loss: </span>
            <span className="font-mono text-sm font-bold text-destructive">₹{(quantity * lot.unitCost).toFixed(2)}</span>
          </div>
          <div className="flex justify-end gap-2 pt-2">
            <button type="button" onClick={() => void confirmClose(onClose)} className="rounded-full px-4 py-2 font-semibold text-muted-foreground hover:bg-secondary">Cancel</button>
            <button type="submit" disabled={loading || quantity <= 0} className="rounded-full bg-destructive px-5 py-2 font-semibold text-destructive-foreground">
              {loading ? "Writing off…" : "Confirm Loss Write-Off"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

// ============================================================================
// 5. TRANSFERS TAB (Central Kitchen & Outlets)
// ============================================================================
export function TransfersTab({ restaurantId, ingredients }: { restaurantId: string; ingredients: Ingredient[] }) {
  const cache = useQueryClient();
  const [createOutletModal, setCreateOutletModal] = useState(false);
  const [createTransferModal, setCreateTransferModal] = useState(false);
  const [dispatchModal, setDispatchModal] = useState<StockTransfer | null>(null);
  const [receiveModal, setReceiveModal] = useState<StockTransfer | null>(null);

  const [shortageAuditOpen, setShortageAuditOpen] = useState(false);
  const [auditData, setAuditData] = useState<TransferDiscrepancyReportDto | null>(null);
  const [loadingAudit, setLoadingAudit] = useState(false);
  const [applyingCorrection, setApplyingCorrection] = useState(false);

  const runAudit = async () => {
    setLoadingAudit(true);
    try {
      const data = await parcelApi.getTransferShortageDryRun(restaurantId);
      setAuditData(data);
      setShortageAuditOpen(true);
    } catch (e: any) {
      toast.error(e.message || "Failed to run shortage dry-run.");
    } finally {
      setLoadingAudit(false);
    }
  };

  const handleApplyCorrection = async () => {
    setApplyingCorrection(true);
    try {
      const res = await parcelApi.applyTransferShortageCorrection(restaurantId);
      toast.success(res.message || "Traceable compensating movements applied successfully.");
      const refreshed = await parcelApi.getTransferShortageDryRun(restaurantId);
      setAuditData(refreshed);
      cache.invalidateQueries({ queryKey: ["transfers", restaurantId] });
      cache.invalidateQueries({ queryKey: ["ingredients", restaurantId] });
    } catch (e: any) {
      toast.error(e.message || "Failed to apply correction.");
    } finally {
      setApplyingCorrection(false);
    }
  };

  const outletsQuery = useQuery({ queryKey: ["outlets", restaurantId], queryFn: () => getOutlets(restaurantId) });
  const transfersQuery = useQuery({ queryKey: ["transfers", restaurantId], queryFn: () => getTransfers(restaurantId) });

  const outlets = outletsQuery.data || [];

  return (
    <div className="space-y-6">
      {/* Header action */}
      <div className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-border bg-card p-3 shadow-soft">
        <div>
          <p className="font-bold text-sm">Multi-Outlet & Central Kitchen Transfers</p>
          <p className="text-xs text-muted-foreground">Manage stock requisitions, dispatch in-transit items, and destination receipts with discrepancy audit.</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <button
            onClick={runAudit}
            disabled={loadingAudit}
            className="inline-flex items-center gap-1.5 rounded-full border border-amber-300 bg-amber-50 dark:bg-amber-950/40 px-4 py-2 text-xs font-semibold text-amber-800 dark:text-amber-300 hover:bg-amber-100"
          >
            {loadingAudit ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <AlertTriangle className="h-3.5 w-3.5" />}
            Shortage Audit & Correction
          </button>
          <button
            onClick={() => setCreateOutletModal(true)}
            className="rounded-full border border-border bg-background px-4 py-2 text-xs font-semibold hover:bg-secondary"
          >
            + Add Outlet
          </button>
          <button
            onClick={() => setCreateTransferModal(true)}
            className="inline-flex items-center gap-1.5 rounded-full bg-gradient-primary px-4 py-2 text-xs font-semibold text-primary-foreground shadow-glow"
          >
            <ArrowLeftRight className="h-3.5 w-3.5" /> Requisition / Transfer Stock
          </button>
        </div>
      </div>

      {/* Outlets Summary */}
      <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-3">
        {outlets.map(o => (
          <div key={o.id} className="rounded-2xl border border-border bg-card p-3.5 space-y-1">
            <div className="flex items-center justify-between">
              <span className="font-mono text-xs font-bold text-primary">{o.code}</span>
              {o.isCentralKitchen && <span className="rounded-full bg-blue-500/15 px-2 py-0.2 text-[10px] font-bold text-blue-600">CENTRAL KITCHEN</span>}
            </div>
            <p className="font-semibold text-sm">{o.name}</p>
            {o.address && <p className="text-xs text-muted-foreground truncate">{o.address}</p>}
          </div>
        ))}
      </div>

      {/* Transfers Table */}
      <div className="overflow-hidden rounded-3xl border border-border bg-card shadow-soft">
        <table className="w-full text-sm">
          <thead className="border-b border-border bg-secondary/50 text-left text-xs uppercase tracking-wider text-muted-foreground">
            <tr>
              <th className="p-4">Transfer #</th>
              <th className="p-4">Source Outlet</th>
              <th className="p-4">Destination Outlet</th>
              <th className="p-4">Items Transferred</th>
              <th className="p-4">Date</th>
              <th className="p-4">Status</th>
              <th className="p-4 text-right">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {transfersQuery.isLoading && (
              <tr><td colSpan={7} className="p-8 text-center"><Loader2 className="h-5 w-5 animate-spin mx-auto text-primary" /></td></tr>
            )}
            {!transfersQuery.isLoading && (transfersQuery.data || []).length === 0 && (
              <tr><td colSpan={7} className="p-8 text-center text-sm text-muted-foreground">No stock transfers found.</td></tr>
            )}
            {transfersQuery.data?.map(t => (
              <tr key={t.id} className="hover:bg-secondary/20">
                <td className="p-4 font-mono font-bold text-primary">{t.transferNumber}</td>
                <td className="p-4 font-semibold">{t.sourceOutletName}</td>
                <td className="p-4 font-semibold">{t.destinationOutletName}</td>
                <td className="p-4 text-xs">
                  {t.items.map(i => (
                    <div key={i.id}>
                      {i.requestedQuantity} {i.baseUnit} {i.ingredientName}
                      {i.discrepancyQuantity > 0 && (
                        <span className="ml-1 text-destructive font-bold">({i.discrepancyQuantity} {i.baseUnit} shortfall)</span>
                      )}
                    </div>
                  ))}
                </td>
                <td className="p-4 text-xs font-mono text-muted-foreground">{new Date(t.createdAtUtc).toLocaleDateString()}</td>
                <td className="p-4">
                  <span className={`rounded-full px-2.5 py-0.5 text-xs font-bold ${
                    t.status === "Received" ? "bg-emerald-500/15 text-emerald-600"
                    : t.status === "Dispatched" ? "bg-blue-500/15 text-blue-600"
                    : "bg-secondary text-muted-foreground"
                  }`}>
                    {t.status}
                  </span>
                </td>
                <td className="p-4 text-right space-x-2">
                  {(t.status === "Requested" || t.status === "Approved") && (
                    <button
                      onClick={() => setDispatchModal(t)}
                      className="rounded-full bg-blue-600 px-3 py-1 text-xs font-semibold text-white hover:bg-blue-700"
                    >
                      Dispatch
                    </button>
                  )}
                  {t.status === "Dispatched" && (
                    <button
                      onClick={() => setReceiveModal(t)}
                      className="rounded-full bg-emerald-600 px-3 py-1 text-xs font-semibold text-white hover:bg-emerald-700"
                    >
                      Receive & Verify
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {createOutletModal && (
        <CreateOutletModal
          restaurantId={restaurantId}
          onClose={() => setCreateOutletModal(false)}
          onSuccess={() => {
            setCreateOutletModal(false);
            cache.invalidateQueries({ queryKey: ["outlets", restaurantId] });
          }}
        />
      )}

      {createTransferModal && (
        <CreateTransferModal
          restaurantId={restaurantId}
          outlets={outlets}
          ingredients={ingredients}
          onClose={() => setCreateTransferModal(false)}
          onSuccess={() => {
            setCreateTransferModal(false);
            cache.invalidateQueries({ queryKey: ["transfers", restaurantId] });
          }}
        />
      )}

      {dispatchModal && (
        <DispatchModal
          restaurantId={restaurantId}
          transfer={dispatchModal}
          onClose={() => setDispatchModal(null)}
          onSuccess={() => {
            setDispatchModal(null);
            cache.invalidateQueries({ queryKey: ["transfers", restaurantId] });
            cache.invalidateQueries({ queryKey: ["inventory_ingredients", restaurantId] });
          }}
        />
      )}

      {receiveModal && (
        <ReceiveTransferModal
          restaurantId={restaurantId}
          transfer={receiveModal}
          onClose={() => setReceiveModal(null)}
          onSuccess={() => {
            setReceiveModal(null);
            cache.invalidateQueries({ queryKey: ["transfers", restaurantId] });
            cache.invalidateQueries({ queryKey: ["inventory_ingredients", restaurantId] });
          }}
        />
      )}

      {/* Shortage Audit & Compensating Correction Modal */}
      {shortageAuditOpen && auditData && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-background/80 p-4 backdrop-blur-sm">
          <div className="w-full max-w-2xl rounded-3xl border border-border bg-card p-6 shadow-card space-y-4">
            <div className="flex items-start justify-between">
              <div>
                <h3 className="font-display text-lg font-bold flex items-center gap-2">
                  <AlertTriangle className="h-5 w-5 text-amber-600" />
                  Outlet Transfer Shortage Dry-Run Audit
                </h3>
                <p className="mt-1 text-xs text-muted-foreground">
                  Validates physical stock ledger movements for past transfers where dispatch quantity exceeded receipt quantity.
                </p>
              </div>
              <button
                onClick={() => void confirmClose(() => setShortageAuditOpen(false))}
                className="rounded-full p-1.5 text-muted-foreground hover:bg-secondary"
              >
                <X className="h-5 w-5" />
              </button>
            </div>

            <div className="grid grid-cols-3 gap-3 text-xs">
              <div className="p-3 bg-muted/40 rounded-xl border">
                <span className="text-[11px] text-muted-foreground block font-medium">Affected Transfers</span>
                <span className="text-base font-bold">{auditData.totalAffectedTransfers} past transfer(s)</span>
              </div>
              <div className="p-3 bg-amber-50/40 dark:bg-amber-950/20 border border-amber-200 dark:border-amber-900 rounded-xl">
                <span className="text-[11px] text-amber-700 dark:text-amber-400 block font-medium">Shortage Units</span>
                <span className="text-base font-bold font-mono text-amber-600 dark:text-amber-400">
                  {auditData.totalShortageUnits.toFixed(2)} units
                </span>
              </div>
              <div className="p-3 bg-emerald-50/40 dark:bg-emerald-950/20 border border-emerald-200 dark:border-emerald-900 rounded-xl">
                <span className="text-[11px] text-emerald-700 dark:text-emerald-400 block font-medium">Excess Loss Valuation</span>
                <span className="text-base font-bold font-mono text-emerald-600 dark:text-emerald-400">
                  ₹{auditData.totalExcessLossValuation.toFixed(2)}
                </span>
              </div>
            </div>

            <div className="max-h-60 overflow-y-auto border rounded-xl divide-y text-xs">
              {auditData.items.length === 0 ? (
                <div className="p-6 text-center text-muted-foreground">
                  No transfers with shortage discrepancies found. All transfers have balanced stock ledger accounting.
                </div>
              ) : (
                auditData.items.map((item) => (
                  <div key={item.transferId} className="p-3 flex justify-between items-center">
                    <div>
                      <div className="font-bold flex items-center gap-1.5">
                        <span>{item.transferNumber}</span>
                        <span className="text-muted-foreground">({item.sourceOutletName} → {item.destinationOutletName})</span>
                        {item.isCorrected && (
                          <span className="rounded-full bg-emerald-100 text-emerald-800 text-[10px] font-bold px-2 py-0.5">
                            ✓ Compensated
                          </span>
                        )}
                      </div>
                      <div className="text-[11px] text-muted-foreground mt-0.5">
                        {item.stockItemName}: Dispatched {item.dispatchedQuantity} {item.unit}, Received {item.receivedQuantity} {item.unit} (Shortage: {item.shortageQuantity} {item.unit})
                      </div>
                    </div>
                    <div className="text-right">
                      <div className="font-mono font-bold text-amber-600">₹{item.excessLossValuation.toFixed(2)}</div>
                      <div className="text-[10px] text-muted-foreground">@ ₹{item.unitCostSnapshot.toFixed(2)}/{item.unit}</div>
                    </div>
                  </div>
                ))
              )}
            </div>

            <div className="rounded-xl bg-blue-50/40 dark:bg-blue-950/20 border border-blue-200 dark:border-blue-900 p-3 text-xs text-blue-900 dark:text-blue-300">
              <span className="font-bold">Ledger Preservation Policy: </span>
              Historical movements are never altered or deleted. Compensating movements append traceable corrections that restore inventory on-hand balances and valuations without rewriting audit trails.
            </div>

            <div className="flex justify-end gap-2 pt-2">
              <button
                type="button"
                onClick={() => void confirmClose(() => setShortageAuditOpen(false))}
                className="rounded-full border border-border px-4 py-2 text-xs font-semibold"
              >
                Close
              </button>
              {auditData.uncorrectedCount > 0 && (
                <button
                  type="button"
                  disabled={applyingCorrection}
                  onClick={handleApplyCorrection}
                  className="rounded-full bg-gradient-primary px-5 py-2 text-xs font-semibold text-primary-foreground shadow-glow"
                >
                  {applyingCorrection ? <Loader2 className="h-3.5 w-3.5 animate-spin mr-1" /> : null}
                  Apply Compensating Movements ({auditData.uncorrectedCount})
                </button>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function CreateOutletModal({ restaurantId, onClose, onSuccess }: { restaurantId: string; onClose: () => void; onSuccess: () => void }) {
  const [name, setName] = useState("");
  const [code, setCode] = useState("");
  const [isCentralKitchen, setIsCentralKitchen] = useState(false);
  const [address, setAddress] = useState("");
  const [saving, setSaving] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim() || !code.trim()) return toast.error("Name and Code are required.");
    setSaving(true);
    try {
      await createOutlet(restaurantId, { name: name.trim(), code: code.trim().toUpperCase(), isCentralKitchen, address: address.trim() || undefined });
      toast.success("Outlet added.");
      onSuccess();
    } catch (err: unknown) {
      toast.error(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
      <div className="w-full max-w-md rounded-3xl border border-border bg-card p-6 shadow-glow">
        <h2 className="font-display text-lg font-bold">Add Outlet / Warehouse</h2>
        <form onSubmit={handleSubmit} className="mt-4 space-y-4 text-xs">
          <div>
            <label className="font-bold text-muted-foreground">Outlet Name *</label>
            <input required value={name} onChange={e => setName(e.target.value)} placeholder="e.g. Downtown Branch" className="mt-1 w-full rounded-xl border border-border bg-background p-2.5 text-sm" />
          </div>
          <div>
            <label className="font-bold text-muted-foreground">Outlet Code *</label>
            <input required value={code} onChange={e => setCode(e.target.value)} placeholder="e.g. DT01" className="mt-1 w-full rounded-xl border border-border bg-background p-2.5 text-sm font-mono font-bold uppercase" />
          </div>
          <div className="flex items-center gap-2 pt-1">
            <input type="checkbox" id="ck" checked={isCentralKitchen} onChange={e => setIsCentralKitchen(e.target.checked)} className="rounded" />
            <label htmlFor="ck" className="font-semibold text-foreground">Is Central Kitchen / Commissary</label>
          </div>
          <div>
            <label className="font-bold text-muted-foreground">Address</label>
            <input value={address} onChange={e => setAddress(e.target.value)} placeholder="Street address..." className="mt-1 w-full rounded-xl border border-border bg-background p-2.5 text-sm" />
          </div>
          <div className="flex justify-end gap-2 pt-2">
            <button type="button" onClick={() => void confirmClose(onClose)} className="rounded-full px-4 py-2 font-semibold text-muted-foreground hover:bg-secondary">Cancel</button>
            <button type="submit" disabled={saving} className="rounded-full bg-gradient-primary px-5 py-2 font-semibold text-primary-foreground">
              {saving ? "Saving…" : "Save Outlet"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

function CreateTransferModal({ restaurantId, outlets, ingredients, onClose, onSuccess }: {
  restaurantId: string;
  outlets: Outlet[];
  ingredients: Ingredient[];
  onClose: () => void;
  onSuccess: () => void;
}) {
  const [sourceId, setSourceId] = useState(outlets[0]?.id || "");
  const [destId, setDestId] = useState(outlets[1]?.id || outlets[0]?.id || "");
  const [lines, setLines] = useState<{ ingredientId: string; requestedQuantity: number }[]>([
    { ingredientId: ingredients[0]?.id || "", requestedQuantity: 1000 }
  ]);
  const [saving, setSaving] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (sourceId === destId) return toast.error("Source and destination outlets must be different.");
    setSaving(true);
    try {
      await createTransfer(restaurantId, {
        sourceOutletId: sourceId,
        destinationOutletId: destId,
        items: lines
      });
      toast.success("Stock transfer requested.");
      onSuccess();
    } catch (err: unknown) {
      toast.error(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
      <div className="w-full max-w-lg rounded-3xl border border-border bg-card p-6 shadow-glow">
        <h2 className="font-display text-lg font-bold">Request Stock Transfer</h2>
        <form onSubmit={handleSubmit} className="mt-4 space-y-4 text-xs">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="font-bold text-muted-foreground">Source Outlet *</label>
              <select value={sourceId} onChange={e => setSourceId(e.target.value)} className="mt-1 w-full rounded-xl border border-border bg-background p-2 text-sm font-semibold">
                {outlets.map(o => <option key={o.id} value={o.id}>{o.name}</option>)}
              </select>
            </div>
            <div>
              <label className="font-bold text-muted-foreground">Destination Outlet *</label>
              <select value={destId} onChange={e => setDestId(e.target.value)} className="mt-1 w-full rounded-xl border border-border bg-background p-2 text-sm font-semibold">
                {outlets.map(o => <option key={o.id} value={o.id}>{o.name}</option>)}
              </select>
            </div>
          </div>

          <div>
            <label className="font-bold uppercase tracking-wider text-muted-foreground mb-1 block">Items to Transfer</label>
            <div className="space-y-2">
              {lines.map((l, idx) => (
                <div key={idx} className="grid grid-cols-12 gap-2 items-center rounded-xl border border-border bg-secondary/20 p-2">
                  <div className="col-span-8">
                    <select
                      value={l.ingredientId}
                      onChange={e => {
                        const copy = [...lines];
                        copy[idx].ingredientId = e.target.value;
                        setLines(copy);
                      }}
                      className="w-full rounded-lg border border-border bg-background p-1.5 font-semibold"
                    >
                      {ingredients.map(i => <option key={i.id} value={i.id}>{i.name} ({i.baseUnit})</option>)}
                    </select>
                  </div>
                  <div className="col-span-4">
                    <input
                      type="number"
                      step="any"
                      min="0.01"
                      value={l.requestedQuantity}
                      onChange={e => {
                        const copy = [...lines];
                        copy[idx].requestedQuantity = parseFloat(e.target.value) || 0;
                        setLines(copy);
                      }}
                      className="w-full rounded-lg border border-border bg-background p-1.5 font-mono"
                    />
                  </div>
                </div>
              ))}
            </div>
          </div>

          <div className="flex justify-end gap-2 pt-2 border-t border-border">
            <button type="button" onClick={() => void confirmClose(onClose)} className="rounded-full px-4 py-2 font-semibold text-muted-foreground hover:bg-secondary">Cancel</button>
            <button type="submit" disabled={saving} className="rounded-full bg-gradient-primary px-5 py-2 font-semibold text-primary-foreground">
              {saving ? "Creating…" : "Create Transfer Request"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

function DispatchModal({ restaurantId, transfer, onClose, onSuccess }: {
  restaurantId: string;
  transfer: StockTransfer;
  onClose: () => void;
  onSuccess: () => void;
}) {
  const [items, setItems] = useState(
    transfer.items.map(i => ({ itemId: i.id, ingredientName: i.ingredientName, baseUnit: i.baseUnit, dispatchedQuantity: i.requestedQuantity }))
  );
  const [saving, setSaving] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    try {
      await dispatchTransfer(restaurantId, transfer.id, items);
      toast.success(`Transfer ${transfer.transferNumber} dispatched.`);
      onSuccess();
    } catch (err: unknown) {
      toast.error(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
      <div className="w-full max-w-md rounded-3xl border border-border bg-card p-6 shadow-glow">
        <h2 className="font-display text-lg font-bold">Dispatch Transfer: {transfer.transferNumber}</h2>
        <p className="mt-1 text-xs text-muted-foreground">{transfer.sourceOutletName} → {transfer.destinationOutletName}</p>
        <form onSubmit={handleSubmit} className="mt-4 space-y-4 text-xs">
          <div className="space-y-2">
            {items.map((it, idx) => (
              <div key={it.itemId} className="flex justify-between items-center rounded-xl border border-border p-2.5">
                <span className="font-semibold">{it.ingredientName}</span>
                <div className="flex items-center gap-1 font-mono">
                  <input
                    type="number"
                    step="any"
                    value={it.dispatchedQuantity}
                    onChange={e => {
                      const copy = [...items];
                      copy[idx].dispatchedQuantity = parseFloat(e.target.value) || 0;
                      setItems(copy);
                    }}
                    className="w-24 rounded-lg border border-border bg-background p-1 font-bold text-right"
                  />
                  <span>{it.baseUnit}</span>
                </div>
              </div>
            ))}
          </div>
          <div className="flex justify-end gap-2 pt-2">
            <button type="button" onClick={() => void confirmClose(onClose)} className="rounded-full px-4 py-2 font-semibold text-muted-foreground hover:bg-secondary">Cancel</button>
            <button type="submit" disabled={saving} className="rounded-full bg-blue-600 px-5 py-2 font-semibold text-white hover:bg-blue-700">
              {saving ? "Dispatching…" : "Confirm Dispatch"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

function ReceiveTransferModal({ restaurantId, transfer, onClose, onSuccess }: {
  restaurantId: string;
  transfer: StockTransfer;
  onClose: () => void;
  onSuccess: () => void;
}) {
  const [items, setItems] = useState(
    transfer.items.map(i => ({
      itemId: i.id,
      ingredientName: i.ingredientName,
      baseUnit: i.baseUnit,
      dispatchedQuantity: i.dispatchedQuantity,
      receivedQuantity: i.dispatchedQuantity,
      discrepancyReason: ""
    }))
  );
  const [saving, setSaving] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    try {
      await receiveTransfer(restaurantId, transfer.id, items);
      toast.success(`Transfer ${transfer.transferNumber} received at destination.`);
      onSuccess();
    } catch (err: unknown) {
      toast.error(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
      <div className="w-full max-w-lg rounded-3xl border border-border bg-card p-6 shadow-glow">
        <h2 className="font-display text-lg font-bold">Receive Transfer: {transfer.transferNumber}</h2>
        <p className="mt-1 text-xs text-muted-foreground">Verify quantities received at {transfer.destinationOutletName}</p>
        <form onSubmit={handleSubmit} className="mt-4 space-y-4 text-xs">
          <div className="space-y-3">
            {items.map((it, idx) => {
              const diff = it.dispatchedQuantity - it.receivedQuantity;
              return (
                <div key={it.itemId} className="rounded-xl border border-border p-3 space-y-2">
                  <div className="flex justify-between items-center">
                    <span className="font-bold text-sm">{it.ingredientName}</span>
                    <span className="text-muted-foreground font-mono">Dispatched: {it.dispatchedQuantity} {it.baseUnit}</span>
                  </div>
                  <div className="flex items-center gap-2">
                    <label className="text-muted-foreground font-semibold">Received Qty:</label>
                    <input
                      type="number"
                      step="any"
                      value={it.receivedQuantity}
                      onChange={e => {
                        const copy = [...items];
                        copy[idx].receivedQuantity = parseFloat(e.target.value) || 0;
                        setItems(copy);
                      }}
                      className="w-28 rounded-lg border border-border bg-background p-1.5 font-mono font-bold"
                    />
                    <span>{it.baseUnit}</span>
                  </div>
                  {diff > 0 && (
                    <div className="pt-1">
                      <span className="text-destructive font-semibold">Discrepancy shortfall: {diff} {it.baseUnit}</span>
                      <input
                        placeholder="Discrepancy reason (e.g. Broken in transit)"
                        value={it.discrepancyReason}
                        onChange={e => {
                          const copy = [...items];
                          copy[idx].discrepancyReason = e.target.value;
                          setItems(copy);
                        }}
                        className="mt-1 w-full rounded-lg border border-border bg-background p-1.5 text-xs"
                      />
                    </div>
                  )}
                </div>
              );
            })}
          </div>
          <div className="flex justify-end gap-2 pt-2 border-t border-border">
            <button type="button" onClick={() => void confirmClose(onClose)} className="rounded-full px-4 py-2 font-semibold text-muted-foreground hover:bg-secondary">Cancel</button>
            <button type="submit" disabled={saving} className="rounded-full bg-emerald-600 px-5 py-2 font-semibold text-white hover:bg-emerald-700">
              {saving ? "Posting Receipt…" : "Confirm Receipt & Post Inward Stock"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

// ============================================================================
// 6. INVENTORY SETTINGS MODAL (Costing Method & Availability)
// ============================================================================
export function InventorySettingsModal({ restaurantId, onClose }: { restaurantId: string; onClose: () => void }) {
  const cache = useQueryClient();
  const settingsQuery = useQuery({ queryKey: ["inventory_settings", restaurantId], queryFn: () => getInventorySettings(restaurantId) });
  const [costingMethod, setCostingMethod] = useState<"WeightedAverage" | "FIFO" | "LIFO">("WeightedAverage");
  const [autoManageAvailability, setAutoManageAvailability] = useState(false);
  const [saving, setSaving] = useState(false);

  React.useEffect(() => {
    if (settingsQuery.data) {
      setCostingMethod(settingsQuery.data.costingMethod);
      setAutoManageAvailability(settingsQuery.data.autoManageAvailability);
    }
  }, [settingsQuery.data]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    try {
      await updateInventorySettings(restaurantId, { costingMethod, autoManageAvailability });
      toast.success("Inventory costing & availability settings updated.");
      cache.invalidateQueries({ queryKey: ["inventory_settings", restaurantId] });
      onClose();
    } catch (err: unknown) {
      toast.error(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
      <div className="w-full max-w-md rounded-3xl border border-border bg-card p-6 shadow-glow">
        <div className="flex items-center justify-between border-b border-border pb-3">
          <h2 className="font-display text-lg font-bold">Inventory & Costing Configuration</h2>
          <button onClick={() => void confirmClose(onClose)} className="rounded-full p-1 text-muted-foreground hover:bg-secondary"><X className="h-5 w-5" /></button>
        </div>

        <form onSubmit={handleSubmit} className="mt-4 space-y-4 text-xs">
          <div>
            <label className="font-bold text-muted-foreground">Inventory Valuation & Costing Method</label>
            <div className="mt-2 space-y-2">
              <label className="flex items-start gap-2.5 rounded-xl border border-border p-3 cursor-pointer hover:bg-secondary/20">
                <input
                  type="radio"
                  name="costing"
                  value="WeightedAverage"
                  checked={costingMethod === "WeightedAverage"}
                  onChange={() => setCostingMethod("WeightedAverage")}
                  className="mt-0.5"
                />
                <div>
                  <span className="font-bold text-foreground">Moving Weighted Average (Default)</span>
                  <p className="text-muted-foreground text-[11px] mt-0.5">Smooths price volatility by continuously updating average unit cost upon receipt.</p>
                </div>
              </label>

              <label className="flex items-start gap-2.5 rounded-xl border border-border p-3 cursor-pointer hover:bg-secondary/20">
                <input
                  type="radio"
                  name="costing"
                  value="FIFO"
                  checked={costingMethod === "FIFO"}
                  onChange={() => setCostingMethod("FIFO")}
                  className="mt-0.5"
                />
                <div>
                  <span className="font-bold text-foreground">FIFO (First-In, First-Out)</span>
                  <p className="text-muted-foreground text-[11px] mt-0.5">Depletes the oldest acquired cost layers first during order settlement.</p>
                </div>
              </label>

              <label className="flex items-start gap-2.5 rounded-xl border border-border p-3 cursor-pointer hover:bg-secondary/20">
                <input
                  type="radio"
                  name="costing"
                  value="LIFO"
                  checked={costingMethod === "LIFO"}
                  onChange={() => setCostingMethod("LIFO")}
                  className="mt-0.5"
                />
                <div>
                  <span className="font-bold text-foreground">LIFO (Last-In, First-Out)</span>
                  <p className="text-muted-foreground text-[11px] mt-0.5">Depletes the newest acquired cost layers first during order settlement.</p>
                </div>
              </label>
            </div>
          </div>

          <div className="rounded-2xl border border-border bg-secondary/20 p-3.5">
            <label className="flex items-start gap-2.5 cursor-pointer">
              <input
                type="checkbox"
                checked={autoManageAvailability}
                onChange={e => setAutoManageAvailability(e.target.checked)}
                className="mt-0.5 rounded"
              />
              <div>
                <span className="font-bold text-foreground">Automatic 86&apos;d Menu Availability</span>
                <p className="text-muted-foreground text-[11px] mt-0.5">
                  When enabled, menu items are automatically marked unavailable when ingredient stock is exhausted, preventing overselling via live order reservations.
                </p>
              </div>
            </label>
          </div>

          <div className="flex justify-end gap-2 pt-2 border-t border-border">
            <button type="button" onClick={() => void confirmClose(onClose)} className="rounded-full px-4 py-2 font-semibold text-muted-foreground hover:bg-secondary">Cancel</button>
            <button type="submit" disabled={saving} className="rounded-full bg-gradient-primary px-5 py-2 font-semibold text-primary-foreground">
              {saving ? "Saving…" : "Save Settings"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
