import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import {
  ShoppingBag,
  Power,
  RotateCw,
  CheckCircle2,
  AlertCircle,
  Clock,
  Sparkles,
  Zap,
  RefreshCw,
  Send,
  ShieldCheck,
  PauseCircle,
  PlayCircle,
  Layers,
  ArrowRight,
} from "lucide-react";
import { formatINR } from "@/lib/mock-data";
import {
  marketplaceAutomationApi,
  type MarketplaceStore,
  type MarketplaceItem,
  type OutboundUpdate,
  type MarketplaceOrderEvent,
} from "@/services/marketplace-automation";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";

interface MarketplaceAutomationManagerProps {
  restaurantId: string;
}

export function MarketplaceAutomationManager({ restaurantId }: MarketplaceAutomationManagerProps) {
  const queryClient = useQueryClient();
  const [activeTab, setActiveTab] = useState<"stores" | "items" | "webhook" | "outbound">("stores");
  const [lastWebhookResult, setLastWebhookResult] = useState<any>(null);

  // Fetch stores
  const { data: stores = [], isLoading: loadingStores, refetch: refetchStores } = useQuery({
    queryKey: ["marketplaceStores", restaurantId],
    queryFn: () => marketplaceAutomationApi.getStores(restaurantId),
  });

  // Fetch mapped items
  const { data: items = [], isLoading: loadingItems, refetch: refetchItems } = useQuery({
    queryKey: ["marketplaceItems", restaurantId],
    queryFn: () => marketplaceAutomationApi.getItems(restaurantId),
  });

  // Fetch outbound updates
  const { data: outboundUpdates = [], isLoading: loadingOutbound, refetch: refetchOutbound } = useQuery({
    queryKey: ["marketplaceOutbound", restaurantId],
    queryFn: () => marketplaceAutomationApi.getOutboundUpdates(restaurantId),
  });

  // Toggle store status mutation
  const toggleStoreStatusMutation = useMutation({
    mutationFn: ({ provider, status }: { provider: "Swiggy" | "Zomato"; status: "Online" | "Paused" }) =>
      marketplaceAutomationApi.toggleStoreStatus(restaurantId, provider, status),
    onSuccess: (updated) => {
      toast.success(`${updated.provider} store is now ${updated.storeStatus}.`);
      queryClient.invalidateQueries({ queryKey: ["marketplaceStores", restaurantId] });
    },
  });

  // Toggle store auto-accept mutation
  const toggleAutoAcceptMutation = useMutation({
    mutationFn: ({ provider, autoAccept }: { provider: "Swiggy" | "Zomato"; autoAccept: boolean }) =>
      marketplaceAutomationApi.toggleAutoAccept(restaurantId, provider, autoAccept),
    onSuccess: (updated) => {
      toast.success(`Auto-accept ${updated.autoAccept ? "enabled" : "disabled"} for ${updated.provider}.`);
      queryClient.invalidateQueries({ queryKey: ["marketplaceStores", restaurantId] });
    },
  });

  // Toggle item availability mutation
  const toggleItemAvailabilityMutation = useMutation({
    mutationFn: ({ itemId, isAvailable }: { itemId: string; isAvailable: boolean }) =>
      marketplaceAutomationApi.toggleItemAvailability(restaurantId, itemId, isAvailable),
    onSuccess: (updated) => {
      toast.success(`${updated.menuItemName} is now ${updated.isAvailable ? "Available" : "Paused/Out-of-Stock"} on ${updated.provider}.`);
      queryClient.invalidateQueries({ queryKey: ["marketplaceItems", restaurantId] });
    },
  });

  // Simulate inbound order mutation
  const simulateOrderMutation = useMutation({
    mutationFn: (payload: MarketplaceOrderEvent) =>
      marketplaceAutomationApi.simulateInboundOrder(restaurantId, payload),
    onSuccess: (res) => {
      setLastWebhookResult(res);
      if (res.status === "Duplicate") {
        toast.info(res.message);
      } else {
        toast.success(res.message);
      }
      queryClient.invalidateQueries({ queryKey: ["marketplaceOutbound", restaurantId] });
      queryClient.invalidateQueries({ queryKey: ["orders"] });
    },
  });

  // Retry outbound status update mutation
  const retryOutboundMutation = useMutation({
    mutationFn: (updateId: string) =>
      marketplaceAutomationApi.retryOutboundUpdate(restaurantId, updateId),
    onSuccess: (res) => {
      toast.success(res.message);
      queryClient.invalidateQueries({ queryKey: ["marketplaceOutbound", restaurantId] });
    },
  });

  return (
    <div className="space-y-6">
      {/* Top Banner */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-border/60 pb-4">
        <div>
          <div className="flex items-center gap-2">
            <h2 className="font-display text-2xl font-bold flex items-center gap-2">
              <ShoppingBag className="h-6 w-6 text-primary" />
              Automated Online Order Management
            </h2>
            <Badge variant="outline" className="bg-amber-500/10 text-amber-700 border-amber-500/30 text-xs">
              Local Mock Adapter Active
            </Badge>
          </div>
          <p className="text-xs text-muted-foreground mt-1">
            Swiggy &amp; Zomato webhook ingestion, duplicate event suppression, initial kitchen/bar KOT routing, and retryable outbound status sync.
          </p>
        </div>

        {/* Workspace Tab Switcher */}
        <div className="flex rounded-full border border-border bg-card p-1 text-xs">
          <button
            onClick={() => setActiveTab("stores")}
            className={`rounded-full px-3 py-1 font-semibold transition ${
              activeTab === "stores" ? "bg-primary text-primary-foreground shadow-sm" : "text-muted-foreground hover:text-foreground"
            }`}
          >
            Store Outlets ({stores.length})
          </button>
          <button
            onClick={() => setActiveTab("items")}
            className={`rounded-full px-3 py-1 font-semibold transition ${
              activeTab === "items" ? "bg-primary text-primary-foreground shadow-sm" : "text-muted-foreground hover:text-foreground"
            }`}
          >
            Item Mappings ({items.length})
          </button>
          <button
            onClick={() => setActiveTab("webhook")}
            className={`rounded-full px-3 py-1 font-semibold transition ${
              activeTab === "webhook" ? "bg-primary text-primary-foreground shadow-sm" : "text-muted-foreground hover:text-foreground"
            }`}
          >
            Inbound Simulator &amp; Deduplication
          </button>
          <button
            onClick={() => setActiveTab("outbound")}
            className={`rounded-full px-3 py-1 font-semibold transition ${
              activeTab === "outbound" ? "bg-primary text-primary-foreground shadow-sm" : "text-muted-foreground hover:text-foreground"
            }`}
          >
            Outbound Status Queue ({outboundUpdates.length})
          </button>
        </div>
      </div>

      {/* Integration Notice Alert */}
      <div className="rounded-2xl border border-blue-200 bg-blue-50/50 p-4 text-xs text-blue-900 flex items-start gap-3">
        <ShieldCheck className="h-5 w-5 text-blue-600 shrink-0 mt-0.5" />
        <div>
          <p className="font-semibold">Sandbox Contract &amp; Integration Notice</p>
          <p className="mt-0.5 text-blue-800">
            Live Swiggy UrbanPiper / Zomato Food Discovery partner credentials are not configured in local environment. TABLO uses a strictly validated local mock adapter with real deduplication and station KOT generation. Manual intake in Parcel Counter continues without interruption.
          </p>
        </div>
      </div>

      {/* ========================================================
          TAB 1: STORE OUTLETS & STATUS
          ======================================================== */}
      {activeTab === "stores" && (
        <div className="grid gap-4 sm:grid-cols-2">
          {stores.map((store) => {
            const isOnline = store.storeStatus === "Online";
            return (
              <div
                key={store.id}
                className="rounded-3xl border border-border bg-card p-6 shadow-soft space-y-5"
              >
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-3">
                    <div
                      className={`h-11 w-11 rounded-2xl flex items-center justify-center font-bold text-white shadow-soft ${
                        store.provider === "Swiggy" ? "bg-orange-500" : "bg-red-600"
                      }`}
                    >
                      {store.provider === "Swiggy" ? "SW" : "ZM"}
                    </div>
                    <div>
                      <h3 className="font-bold text-base">{store.provider} India Delivery</h3>
                      <p className="text-xs text-muted-foreground">Store ID: {store.externalStoreId}</p>
                    </div>
                  </div>
                  <Badge
                    variant="outline"
                    className={`text-xs px-2.5 py-0.5 font-bold ${
                      isOnline
                        ? "bg-emerald-50 text-emerald-700 border-emerald-300"
                        : "bg-rose-50 text-rose-700 border-rose-300"
                    }`}
                  >
                    {store.storeStatus}
                  </Badge>
                </div>

                <div className="space-y-3 pt-2 border-t border-border/60">
                  <div className="flex items-center justify-between text-xs">
                    <span className="text-muted-foreground">Store Receiving Status</span>
                    <Button
                      size="sm"
                      variant={isOnline ? "destructive" : "default"}
                      className="h-8 text-xs gap-1.5"
                      onClick={() =>
                        toggleStoreStatusMutation.mutate({
                          provider: store.provider,
                          status: isOnline ? "Paused" : "Online",
                        })
                      }
                    >
                      {isOnline ? <PauseCircle className="h-3.5 w-3.5" /> : <PlayCircle className="h-3.5 w-3.5" />}
                      {isOnline ? "Pause Orders" : "Go Online"}
                    </Button>
                  </div>

                  <div className="flex items-center justify-between text-xs pt-2">
                    <span className="text-muted-foreground">Auto-Accept Inbound Orders</span>
                    <button
                      type="button"
                      onClick={() =>
                        toggleAutoAcceptMutation.mutate({
                          provider: store.provider,
                          autoAccept: !store.autoAccept,
                        })
                      }
                      className={`relative inline-flex h-5 w-9 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none ${
                        store.autoAccept ? "bg-primary" : "bg-muted"
                      }`}
                    >
                      <span
                        className={`pointer-events-none inline-block h-4 w-4 transform rounded-full bg-white shadow-sm ring-0 transition duration-200 ease-in-out ${
                          store.autoAccept ? "translate-x-4" : "translate-x-0"
                        }`}
                      />
                    </button>
                  </div>

                  <div className="text-[11px] text-muted-foreground pt-1 flex items-center justify-between">
                    <span>Last heartbeat sync:</span>
                    <span>{new Date(store.lastSyncUtc || Date.now()).toLocaleTimeString("en-IN")}</span>
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}

      {/* ========================================================
          TAB 2: ITEM MAPPINGS & AVAILABILITY
          ======================================================== */}
      {activeTab === "items" && (
        <div className="space-y-4">
          <div className="rounded-2xl border border-border bg-card p-4 flex flex-col sm:flex-row sm:items-center justify-between gap-3 shadow-soft">
            <div>
              <h3 className="font-bold text-sm">Marketplace Item Catalog Mappings</h3>
              <p className="text-xs text-muted-foreground">
                Map TABLO menu items to Swiggy/Zomato item IDs and toggle live availability across platforms.
              </p>
            </div>
            <Button variant="outline" size="sm" onClick={() => refetchItems()} className="text-xs gap-1.5">
              <RefreshCw className="h-3.5 w-3.5" /> Sync Catalog
            </Button>
          </div>

          <div className="rounded-2xl border border-border bg-card overflow-hidden shadow-soft">
            <table className="w-full text-xs">
              <thead className="bg-secondary/50 text-muted-foreground border-b border-border text-left">
                <tr>
                  <th className="p-3">Platform</th>
                  <th className="p-3">Item Name</th>
                  <th className="p-3">External Item ID</th>
                  <th className="p-3">Packaging Charge</th>
                  <th className="p-3">Marketplace Price</th>
                  <th className="p-3 text-right">Online Availability</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border/60">
                {items.map((item) => (
                  <tr key={item.id} className="hover:bg-secondary/20">
                    <td className="p-3">
                      <Badge
                        variant="secondary"
                        className={item.provider === "Swiggy" ? "text-orange-600 bg-orange-50" : "text-red-600 bg-red-50"}
                      >
                        {item.provider}
                      </Badge>
                    </td>
                    <td className="p-3 font-semibold">{item.menuItemName}</td>
                    <td className="p-3 font-mono text-[11px] text-muted-foreground">{item.externalItemId}</td>
                    <td className="p-3">{formatINR(item.packagingCharge)}</td>
                    <td className="p-3 font-semibold">{formatINR(item.customPrice || 350)}</td>
                    <td className="p-3 text-right">
                      <Button
                        size="sm"
                        variant={item.isAvailable ? "outline" : "secondary"}
                        className={`text-xs h-7 px-3 font-semibold ${
                          item.isAvailable ? "border-emerald-500 text-emerald-600" : "text-rose-600 bg-rose-50"
                        }`}
                        onClick={() =>
                          toggleItemAvailabilityMutation.mutate({
                            itemId: item.id,
                            isAvailable: !item.isAvailable,
                          })
                        }
                      >
                        {item.isAvailable ? "Available" : "Sold Out (Paused)"}
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* ========================================================
          TAB 3: INBOUND WEBHOOK SIMULATOR & DEDUPLICATION TEST
          ======================================================== */}
      {activeTab === "webhook" && (
        <div className="grid gap-6 lg:grid-cols-[1fr_360px]">
          <div className="rounded-3xl border border-border bg-card p-6 shadow-soft space-y-5">
            <div>
              <h3 className="font-display text-lg font-bold flex items-center gap-2">
                <Zap className="h-5 w-5 text-amber-500" />
                Inbound Webhook Simulator &amp; Deduplication Test
              </h3>
              <p className="text-xs text-muted-foreground mt-1">
                Simulate inbound order webhooks from Swiggy or Zomato. Verifies that duplicate incoming events with identical event IDs create exactly ONE order and ONE initial KOT per applicable station.
              </p>
            </div>

            <div className="grid gap-3 sm:grid-cols-2">
              {/* Action 1: New Swiggy Order */}
              <div className="rounded-2xl border border-border bg-secondary/30 p-4 space-y-3">
                <div className="flex items-center justify-between">
                  <Badge className="bg-orange-500 text-white font-bold">Swiggy Order</Badge>
                  <span className="text-[11px] text-muted-foreground">Food + Beverage</span>
                </div>
                <p className="text-xs">
                  Simulates Butter Chicken (Food Kitchen) and Fresh Lime Soda (Beverage Station).
                </p>
                <Button
                  size="sm"
                  className="w-full text-xs gap-1.5"
                  onClick={() =>
                    simulateOrderMutation.mutate({
                      eventId: `EVT-SWIGGY-${Date.now()}`,
                      provider: "Swiggy",
                      eventType: "ORDER_PLACED",
                      externalOrderId: `SWIGGY-ORD-${Math.floor(10000 + Math.random() * 90000)}`,
                      externalStoreId: "SWIGGY-BLR-001",
                      totalAmount: 600,
                      customerName: "Rohan Khanna",
                      customerPhone: "9876541234",
                      deliveryAddress: "Apartment 4B, Richmond Road",
                      items: [
                        { externalItemId: "SW-BC-01", name: "Butter Chicken", quantity: 1, price: 480, cookingNotes: "Medium spicy" },
                        { externalItemId: "SW-FLS-04", name: "Fresh Lime Soda", quantity: 1, price: 120 },
                      ],
                    })
                  }
                >
                  <Send className="h-3.5 w-3.5" /> Simulate Inbound Order
                </Button>
              </div>

              {/* Action 2: Duplicate Event Replay Test */}
              <div className="rounded-2xl border border-border bg-secondary/30 p-4 space-y-3">
                <div className="flex items-center justify-between">
                  <Badge variant="destructive" className="font-bold">Deduplication Test</Badge>
                  <span className="text-[11px] text-muted-foreground">Fixed Event ID</span>
                </div>
                <p className="text-xs">
                  Replays a fixed webhook event (`STATIC-EVT-DEDUP-001`). Verifies duplicate ingestion returns Duplicate status without creating extra KOTs.
                </p>
                <Button
                  size="sm"
                  variant="outline"
                  className="w-full text-xs gap-1.5 border-amber-500/40 text-amber-600 hover:bg-amber-50"
                  onClick={() =>
                    simulateOrderMutation.mutate({
                      eventId: "STATIC-EVT-DEDUP-001",
                      provider: "Zomato",
                      eventType: "ORDER_PLACED",
                      externalOrderId: "ZOMATO-STATIC-8899",
                      externalStoreId: "ZOMATO-BLR-002",
                      totalAmount: 420,
                      customerName: "Ananya Roy",
                      customerPhone: "9812344321",
                      deliveryAddress: "Koramangala 5th Block",
                      items: [
                        { externalItemId: "SW-PBM-02", name: "Paneer Butter Masala", quantity: 1, price: 420 },
                      ],
                    })
                  }
                >
                  <RotateCw className="h-3.5 w-3.5" /> Test Replay / Duplicate Event
                </Button>
              </div>
            </div>

            {/* Result Box */}
            {lastWebhookResult && (
              <div
                className={`rounded-2xl border p-4 space-y-2 text-xs ${
                  lastWebhookResult.status === "Duplicate"
                    ? "bg-amber-50 border-amber-200 text-amber-900"
                    : "bg-emerald-50 border-emerald-200 text-emerald-900"
                }`}
              >
                <div className="flex items-center justify-between font-bold">
                  <span className="flex items-center gap-1.5">
                    {lastWebhookResult.status === "Duplicate" ? (
                      <AlertCircle className="h-4 w-4 text-amber-600" />
                    ) : (
                      <CheckCircle2 className="h-4 w-4 text-emerald-600" />
                    )}
                    Webhook Ingestion: {lastWebhookResult.status}
                  </span>
                  {lastWebhookResult.tokenNumber && (
                    <span className="rounded-full bg-emerald-200/60 px-2 py-0.5 text-[11px]">
                      Token #{lastWebhookResult.tokenNumber}
                    </span>
                  )}
                </div>
                <p>{lastWebhookResult.message}</p>
                {lastWebhookResult.generatedKotTicketIds?.length > 0 && (
                  <p className="font-mono text-[11px]">
                    Generated KOTs: {lastWebhookResult.generatedKotTicketIds.join(", ")}
                  </p>
                )}
              </div>
            )}
          </div>

          {/* Right Rules Card */}
          <div className="rounded-3xl border border-border bg-card p-6 shadow-soft space-y-4">
            <h4 className="font-bold text-sm flex items-center gap-1.5">
              <ShieldCheck className="h-4 w-4 text-primary" />
              Ingestion Invariants
            </h4>
            <ul className="text-xs space-y-2 text-muted-foreground">
              <li className="flex items-start gap-2">
                <span className="h-1.5 w-1.5 rounded-full bg-primary mt-1.5 shrink-0" />
                <span><strong>Idempotency:</strong> Replaying webhook events with identical event IDs never creates duplicate orders.</span>
              </li>
              <li className="flex items-start gap-2">
                <span className="h-1.5 w-1.5 rounded-full bg-primary mt-1.5 shrink-0" />
                <span><strong>Station Separation:</strong> Kitchen and bar dishes are split into distinct KOTs per station.</span>
              </li>
              <li className="flex items-start gap-2">
                <span className="h-1.5 w-1.5 rounded-full bg-primary mt-1.5 shrink-0" />
                <span><strong>Tender Isolation:</strong> Settled under Marketplace Collected, excluding local Cash/Card.</span>
              </li>
            </ul>
          </div>
        </div>
      )}

      {/* ========================================================
          TAB 4: OUTBOUND STATUS QUEUE & RETRY
          ======================================================== */}
      {activeTab === "outbound" && (
        <div className="space-y-4">
          <div className="rounded-2xl border border-border bg-card p-4 flex flex-col sm:flex-row sm:items-center justify-between gap-3 shadow-soft">
            <div>
              <h3 className="font-bold text-sm">Outbound Status Updates Queue</h3>
              <p className="text-xs text-muted-foreground">
                Status transitions (Accepted, Preparing, Ready For Pickup) pushed back to Swiggy/Zomato. Recoverable errors can be retried.
              </p>
            </div>
            <Button variant="outline" size="sm" onClick={() => refetchOutbound()} className="text-xs gap-1.5">
              <RefreshCw className="h-3.5 w-3.5" /> Refresh Queue
            </Button>
          </div>

          <div className="rounded-2xl border border-border bg-card overflow-hidden shadow-soft">
            <table className="w-full text-xs">
              <thead className="bg-secondary/50 text-muted-foreground border-b border-border text-left">
                <tr>
                  <th className="p-3">Platform</th>
                  <th className="p-3">External Order Ref</th>
                  <th className="p-3">Target Status</th>
                  <th className="p-3">Attempts</th>
                  <th className="p-3">Status</th>
                  <th className="p-3 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border/60">
                {outboundUpdates.map((update) => (
                  <tr key={update.id} className="hover:bg-secondary/20">
                    <td className="p-3 font-semibold">{update.provider}</td>
                    <td className="p-3 font-mono text-[11px]">{update.externalOrderId}</td>
                    <td className="p-3">
                      <Badge variant="outline" className="text-[10px]">
                        {update.targetStatus}
                      </Badge>
                    </td>
                    <td className="p-3">{update.attempts} of 5</td>
                    <td className="p-3">
                      <Badge
                        variant="secondary"
                        className={
                          update.status === "Success"
                            ? "bg-emerald-100 text-emerald-800"
                            : update.status === "Failed"
                            ? "bg-rose-100 text-rose-800"
                            : "bg-amber-100 text-amber-800"
                        }
                      >
                        {update.status}
                      </Badge>
                    </td>
                    <td className="p-3 text-right">
                      {update.status !== "Success" && (
                        <Button
                          size="sm"
                          variant="outline"
                          className="h-7 text-xs px-2.5 gap-1"
                          onClick={() => retryOutboundMutation.mutate(update.id)}
                        >
                          <RefreshCw className="h-3 w-3" /> Retry Update
                        </Button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}
