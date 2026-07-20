import { createFileRoute } from "@tanstack/react-router";
import { useEffect, useMemo, useState } from "react";
import { Check, ChefHat, CircleDot, Flame, Languages, Loader2, Minus, PackageCheck, Plus, Search, ShoppingBag, Sparkles, Utensils, X } from "lucide-react";
import { toast } from "sonner";
import { formatINR } from "@/lib/mock-data";
import { Category, OrderStatus } from "@/types";
import { useMenu } from "@/hooks/use-menu";
import { useCart } from "@/hooks/use-cart";
import { useOrderTracking } from "@/hooks/use-orders";
import { useQRResolution } from "@/hooks/use-qr";
import { placeOrder, getActiveOrderForTable } from "@/services/api";
import { useQuery } from "@tanstack/react-query";

type SearchParams = {
  r?: string;
  t?: number;
};

export const Route = createFileRoute("/t/$qrToken")({
  validateSearch: (search: Record<string, unknown>): SearchParams => {
    return {
      r: (search.r as string) || "saffron-sage",
      t: Number(search.t) || 4,
    };
  },
  head: () => ({
    meta: [
      { title: "Menu — Tablo demo" },
      { name: "description", content: "Mobile-first QR ordering experience for diners." },
    ],
  }),
  component: CustomerPage,
});

const stages: { key: OrderStatus; label: string; icon: typeof CircleDot }[] = [
  { key: "placed", label: "Order placed", icon: CircleDot },
  { key: "preparing", label: "Preparing", icon: Flame },
  { key: "ready", label: "Ready to serve", icon: PackageCheck },
  { key: "served", label: "Served", icon: Check },
];

type Stage = "menu" | "confirmed";

function CustomerPage() {
  const { qrToken } = Route.useParams();
  
  const [stage, setStage] = useState<Stage>("menu");
  const [selectedCat, setSelectedCat] = useState<Category | null>(null);
  const [query, setQuery] = useState("");
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [orderId, setOrderId] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const { data: qrData, isLoading: loadingQR, error: qrError } = useQRResolution(qrToken);
  const { data: menu = [], isLoading: loadingMenu } = useMenu(qrData?.restaurantId || null);
  const { cart, cartCount, cartTotal, updateQty, qtyOf, clearCart } = useCart();

  useEffect(() => {
    const savedOrderId = localStorage.getItem(`activeOrderId_${qrToken}`);
    if (savedOrderId) {
      setOrderId(savedOrderId);
    }
  }, [qrToken]);

  // Auto-recover active order from backend for this table if not in localStorage
  const { data: activeOrder, error: activeOrderError } = useQuery({
    queryKey: ["activeOrder", qrData?.restaurantId, qrData?.tableId],
    queryFn: () => getActiveOrderForTable(qrData!.restaurantId, qrData!.tableId),
    enabled: !!qrData?.restaurantId && !!qrData?.tableId && !orderId,
    retry: false, // Don't retry on 404
    staleTime: 0,
  });

  useEffect(() => {
    if (activeOrder && !orderId) {
      setOrderId(activeOrder.id);
      localStorage.setItem(`activeOrderId_${qrToken}`, activeOrder.id);
    }
  }, [activeOrder, orderId, qrToken]);

  const { data: currentOrder } = useOrderTracking(qrData?.restaurantId || null, orderId);

  useEffect(() => {
    if (currentOrder?.status === "served") {
      localStorage.removeItem(`activeOrderId_${qrToken}`);
      // Keep orderId in state so they can see the "Served" screen until they click "Order something else"
      // But we removed it from localStorage so it won't persist on refresh.
    }
  }, [currentOrder?.status, qrToken]);

  const loading = loadingQR || (qrData && loadingMenu);

  const categories = useMemo(() => Array.from(new Set(menu.map((m) => m.category))), [menu]);
  const activeCat = selectedCat || categories[0] || "";
  const setActiveCat = setSelectedCat;

  const visible = useMemo(() => {
    return menu.filter((m) => m.category === activeCat && m.name.toLowerCase().includes(query.toLowerCase()));
  }, [activeCat, query, menu]);

  const handlePlaceOrder = async () => {
    if (!qrData || cart.length === 0) return;
    setSubmitting(true);
    try {
      const { orderId: newOrderId } = await placeOrder(qrToken, cart);
      setOrderId(newOrderId);
      localStorage.setItem(`activeOrderId_${qrToken}`, newOrderId);
      setDrawerOpen(false);
      setStage("confirmed");
      toast.success("Order sent to the kitchen!");
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not place order");
    } finally {
      setSubmitting(false);
    }
  };

  if (qrError) {
    return (
      <div className="grid min-h-screen place-items-center bg-secondary/40">
        <div className="text-center p-6">
          <Utensils className="mx-auto h-8 w-8 text-muted-foreground mb-4" />
          <h2 className="text-lg font-bold">Invalid QR Code</h2>
          <p className="text-sm text-muted-foreground mt-2">This table does not exist or is currently inactive.</p>
        </div>
      </div>
    );
  }

  if (loading || !qrData) {
    return (
      <div className="grid min-h-screen place-items-center bg-secondary/40">
        <Loader2 className="h-6 w-6 animate-spin text-primary" />
      </div>
    );
  }

  if (stage === "confirmed") {
    const displayStatus = currentOrder?.status || "placed";
    const displayCart = currentOrder ? currentOrder.items.map((i: any) => ({ item: i, qty: i.qty })) : cart;
    const displayTotal = currentOrder ? currentOrder.total : cartTotal;

    return (
      <Confirmation 
        cart={displayCart} 
        total={displayTotal} 
        stage={displayStatus} 
        onDone={() => { 
          setStage("menu"); 
          clearCart(); 
          // Do not remove orderId so they can continue tracking it!
        }} 
      />
    );
  }

  return (
    <div className="min-h-screen bg-secondary/40 pb-32">
      <div className="mx-auto max-w-md bg-background shadow-card sm:my-6 sm:rounded-[2rem]">
        <div className="rounded-b-[2rem] bg-gradient-primary px-5 pb-6 pt-8 text-primary-foreground sm:rounded-[2rem]">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-xs uppercase tracking-wider opacity-80">{qrData.restaurantTagline}</p>
              <h1 className="font-display text-2xl font-bold">{qrData.restaurantName}</h1>
            </div>
            <span className="rounded-full bg-white/15 px-3 py-1 text-xs font-medium backdrop-blur">{qrData.tableNameOrNumber}</span>
          </div>
          <div className="mt-3 flex items-center gap-3 text-xs opacity-90">
            <span className="inline-flex items-center gap-1"><Languages className="h-3.5 w-3.5" /> EN · മലയാളം</span>
            <span className="inline-flex items-center gap-1"><Sparkles className="h-3.5 w-3.5" /> Daily specials live</span>
          </div>
          <div className="relative mt-5">
            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-foreground/60" />
            <input
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder="Search the menu…"
              className="w-full rounded-full border-0 bg-white py-3 pl-10 pr-4 text-sm text-foreground placeholder:text-foreground/50 focus:outline-none focus:ring-2 focus:ring-white/40"
            />
          </div>
        </div>

        <div className="sticky top-0 z-10 -mt-3 flex gap-2 overflow-x-auto bg-background/80 px-5 py-3 backdrop-blur">
          {categories.map((c) => (
            <button
              key={c}
              onClick={() => setActiveCat(c)}
              className={`shrink-0 rounded-full px-4 py-1.5 text-xs font-semibold transition-colors ${activeCat === c ? "bg-foreground text-background" : "bg-secondary text-muted-foreground hover:text-foreground"}`}
            >
              {c}
            </button>
          ))}
        </div>

        <div className="space-y-4 px-5 pb-6 pt-2">
          {visible.length === 0 ? (
            <EmptyState />
          ) : (
            visible.map((m) => (
              <article key={m.id} className="animate-float-up overflow-hidden rounded-3xl border border-border bg-card shadow-soft">
                <div className="flex gap-3 p-3">
                  <div className="relative h-24 w-24 shrink-0 overflow-hidden rounded-2xl">
                    <img src={m.image} alt={m.name} loading="lazy" width={768} height={768} className={`h-full w-full object-cover transition-transform ${!m.available ? "grayscale" : "hover:scale-105"}`} />
                    {m.special && (
                      <span className="absolute left-1.5 top-1.5 rounded-full bg-warning/90 px-1.5 py-0.5 text-[10px] font-bold text-foreground">Today's Special</span>
                    )}
                  </div>
                  <div className="min-w-0 flex-1">
                    <div className="flex items-start justify-between gap-2">
                      <h3 className="font-display text-base font-semibold leading-tight">{m.name}</h3>
                      {m.popular && <span className="shrink-0 rounded-full bg-primary-soft px-2 py-0.5 text-[10px] font-semibold text-primary">Popular</span>}
                    </div>
                    <p className="mt-1 line-clamp-2 text-xs text-muted-foreground">{m.description}</p>
                    <div className="mt-2 flex items-center justify-between">
                      <p className="font-display text-sm font-bold">{formatINR(m.price)}</p>
                      {!m.available ? (
                        <span className="rounded-full bg-destructive/10 px-2 py-0.5 text-[10px] font-semibold text-destructive">Sold out</span>
                      ) : qtyOf(m.id) === 0 ? (
                        <button onClick={() => updateQty(m, +1)} className="inline-flex items-center gap-1 rounded-full bg-foreground px-3 py-1.5 text-xs font-semibold text-background transition-transform active:scale-95">
                          <Plus className="h-3 w-3" /> Add
                        </button>
                      ) : (
                        <div className="flex items-center gap-2 rounded-full bg-primary-soft p-0.5">
                          <button onClick={() => updateQty(m, -1)} className="grid h-7 w-7 place-items-center rounded-full bg-card text-primary"><Minus className="h-3 w-3" /></button>
                          <span className="min-w-[1ch] text-center text-xs font-bold text-primary">{qtyOf(m.id)}</span>
                          <button onClick={() => updateQty(m, +1)} className="grid h-7 w-7 place-items-center rounded-full bg-gradient-primary text-primary-foreground"><Plus className="h-3 w-3" /></button>
                        </div>
                      )}
                    </div>
                  </div>
                </div>
              </article>
            ))
          )}
        </div>
      </div>

      {stage === "menu" && orderId && currentOrder?.status && currentOrder.status !== "served" && (
        <button
          onClick={() => setStage("confirmed")}
          className="fixed inset-x-3 bottom-20 z-20 mx-auto flex w-full max-w-md items-center justify-between rounded-full bg-foreground px-5 py-3.5 text-background shadow-glow sm:bottom-24"
        >
          <span className="inline-flex items-center gap-2 font-semibold text-sm">
            <PackageCheck className="h-4 w-4 text-primary" />
            Active order in progress
          </span>
          <span className="text-sm font-bold text-primary">Track Order</span>
        </button>
      )}

      {cartCount > 0 && (
        <button
          onClick={() => setDrawerOpen(true)}
          className="fixed inset-x-3 bottom-3 z-30 mx-auto flex w-full max-w-md items-center justify-between rounded-full bg-gradient-primary px-5 py-4 text-primary-foreground shadow-glow sm:bottom-6"
        >
          <span className="inline-flex items-center gap-2 font-semibold">
            <span className="grid h-7 w-7 place-items-center rounded-full bg-white/20"><ShoppingBag className="h-4 w-4" /></span>
            {cartCount} item{cartCount > 1 ? "s" : ""}
          </span>
          <span className="font-display text-base font-bold">{formatINR(cartTotal)} · View cart</span>
        </button>
      )}

      {drawerOpen && (
        <div className="fixed inset-0 z-40">
          <div className="absolute inset-0 bg-foreground/40 backdrop-blur-sm" onClick={() => setDrawerOpen(false)} />
          <div className="absolute inset-x-0 bottom-0 mx-auto max-w-md animate-float-up rounded-t-[2rem] bg-card p-5 shadow-glow sm:bottom-6 sm:left-1/2 sm:-translate-x-1/2 sm:rounded-[2rem]">
            <div className="mx-auto mb-3 h-1 w-10 rounded-full bg-border" />
            <div className="flex items-center justify-between">
              <h2 className="font-display text-lg font-bold">Your order · {qrData.tableNameOrNumber}</h2>
              <button onClick={() => setDrawerOpen(false)} className="grid h-8 w-8 place-items-center rounded-full bg-secondary"><X className="h-4 w-4" /></button>
            </div>
            <ul className="mt-4 max-h-[40vh] divide-y divide-border overflow-y-auto">
              {cart.map((l) => (
                <li key={l.item.id} className="flex items-center gap-3 py-3">
                  <img src={l.item.image} alt="" className="h-12 w-12 rounded-xl object-cover" loading="lazy" />
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm font-semibold">{l.item.name}</p>
                    <p className="text-xs text-muted-foreground">{formatINR(l.item.price)}</p>
                  </div>
                  <div className="flex items-center gap-2 rounded-full bg-secondary p-0.5">
                    <button onClick={() => updateQty(l.item, -1)} className="grid h-7 w-7 place-items-center rounded-full bg-card"><Minus className="h-3 w-3" /></button>
                    <span className="min-w-[1ch] text-center text-xs font-bold">{l.qty}</span>
                    <button onClick={() => updateQty(l.item, +1)} className="grid h-7 w-7 place-items-center rounded-full bg-gradient-primary text-primary-foreground"><Plus className="h-3 w-3" /></button>
                  </div>
                </li>
              ))}
            </ul>
            <div className="mt-3 space-y-1 border-t border-border pt-3 text-sm">
              <div className="flex justify-between text-muted-foreground"><span>Subtotal</span><span>{formatINR(cartTotal)}</span></div>
              <div className="flex justify-between text-muted-foreground"><span>GST (5%)</span><span>{formatINR(Math.round(cartTotal * 0.05))}</span></div>
              <div className="flex justify-between pt-1 font-display text-base font-bold"><span>Total</span><span>{formatINR(cartTotal + Math.round(cartTotal * 0.05))}</span></div>
            </div>
            <button
              onClick={handlePlaceOrder}
              disabled={submitting || cart.some(c => !c.item.available)}
              className="mt-4 inline-flex w-full items-center justify-center gap-2 rounded-full bg-gradient-primary py-3.5 text-sm font-semibold text-primary-foreground shadow-glow disabled:opacity-60"
            >
              {submitting && <Loader2 className="h-4 w-4 animate-spin" />}
              {cart.some(c => !c.item.available) ? "Some items sold out" : "Place order"}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

function EmptyState() {
  return (
    <div className="rounded-3xl border border-dashed border-border bg-card p-10 text-center">
      <Utensils className="mx-auto h-8 w-8 text-muted-foreground" />
      <p className="mt-3 text-sm font-medium">No dishes match that search.</p>
      <p className="text-xs text-muted-foreground">Try a different category or keyword.</p>
    </div>
  );
}

function Confirmation({ cart, total, stage, onDone }: { cart: { item: any, qty: number }[]; total: number; stage: OrderStatus; onDone: () => void }) {
  const displayStage = stage === "accepted" ? "placed" : stage;
  const idx = stages.findIndex((s) => s.key === displayStage);
  return (
    <div className="min-h-screen bg-secondary/40 pb-12">
      <div className="mx-auto max-w-md bg-background sm:my-6 sm:rounded-[2rem] sm:shadow-card">
        <div className="rounded-b-[2rem] bg-gradient-primary p-7 text-center text-primary-foreground sm:rounded-[2rem]">
          <div className="mx-auto grid h-16 w-16 place-items-center rounded-full bg-white/20 backdrop-blur">
            <ChefHat className="h-7 w-7" />
          </div>
          <h1 className="mt-4 font-display text-2xl font-bold">Order placed!</h1>
          <p className="mt-1 text-sm opacity-90">Your kitchen ticket is live. Sit back, we'll bring it over.</p>
        </div>
        <div className="px-5 py-6">
          <ol className="relative space-y-5 border-l border-border pl-6">
            {stages.map((s, i) => {
              const reached = i <= idx || (idx === -1 && i === 0);
              const Icon = s.icon;
              return (
                <li key={s.key} className="relative">
                  <span className={`absolute -left-[34px] grid h-7 w-7 place-items-center rounded-full border-2 ${reached ? "border-primary bg-primary text-primary-foreground" : "border-border bg-card text-muted-foreground"}`}>
                    {i === idx ? <span className="h-2 w-2 animate-pulse-dot rounded-full bg-current" /> : <Icon className="h-3.5 w-3.5" />}
                  </span>
                  <p className={`text-sm font-semibold ${reached ? "text-foreground" : "text-muted-foreground"}`}>{s.label}</p>
                  {i === idx && displayStage !== "served" && <p className="text-xs text-primary">In progress…</p>}
                </li>
              );
            })}
          </ol>
          <div className="mt-6 rounded-2xl border border-border bg-card p-4">
            <p className="text-xs uppercase tracking-wider text-muted-foreground">Order summary</p>
            <ul className="mt-2 divide-y divide-border text-sm">
              {cart.map((l) => (
                <li key={l.item.id} className="flex justify-between py-2">
                  <span>{l.qty} × {l.item.name}</span>
                  <span className="font-semibold">{formatINR(l.qty * l.item.price)}</span>
                </li>
              ))}
            </ul>
            <div className="mt-2 flex justify-between border-t border-border pt-2 font-display font-bold">
              <span>Total</span><span>{formatINR(total + Math.round(total * 0.05))}</span>
            </div>
          </div>
          <button onClick={onDone} className="mt-5 w-full rounded-full border border-border bg-card py-3 text-sm font-semibold">
            Order something else
          </button>
        </div>
      </div>
    </div>
  );
}
