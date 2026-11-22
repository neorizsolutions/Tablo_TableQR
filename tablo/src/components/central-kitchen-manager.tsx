import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import {
  Factory,
  Truck,
  ArrowRightLeft,
  CheckCircle2,
  AlertTriangle,
  RotateCw,
  Plus,
  Layers,
  Sparkles,
  Store,
  DollarSign,
  TrendingDown,
} from "lucide-react";
import { formatINR } from "@/lib/mock-data";
import { centralKitchenApi, type CentralProductionBatch, type OutletStockTransfer } from "@/services/central-kitchen";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";

export function CentralKitchenManager({ restaurantId }: { restaurantId: string }) {
  const queryClient = useQueryClient();
  const [activeTab, setActiveTab] = useState<"production" | "transfers" | "menu-publish">("production");

  // New batch modal state
  const [isBatchModalOpen, setIsBatchModalOpen] = useState(false);
  const [recipeName, setRecipeName] = useState("Paneer Butter Gravy Base");
  const [targetQuantity, setTargetQuantity] = useState(20);
  const [unit, setUnit] = useState("kg");
  const [unitCost, setUnitCost] = useState(175);

  // Dispatch modal state
  const [isDispatchModalOpen, setIsDispatchModalOpen] = useState(false);
  const [dispatchItemName, setDispatchItemName] = useState("Paneer Butter Gravy Base");
  const [dispatchQty, setDispatchQty] = useState(10);
  const [destinationOutlet, setDestinationOutlet] = useState("22222222-0000-0000-0000-000000000009");

  // Receive modal state
  const [receivingTransfer, setReceivingTransfer] = useState<OutletStockTransfer | null>(null);
  const [receivedQty, setReceivedQty] = useState(10);
  const [discrepancyReason, setDiscrepancyReason] = useState("");

  // Menu publish modal state
  const [isPublishModalOpen, setIsPublishModalOpen] = useState(false);
  const [publishPreview, setPublishPreview] = useState<any | null>(null);
  const [pbmOverridePrice, setPbmOverridePrice] = useState(360);
  const [birOverridePrice, setBirOverridePrice] = useState(420);

  // Queries
  const batchesQuery = useQuery({
    queryKey: ["central-batches", restaurantId],
    queryFn: () => centralKitchenApi.getProductionBatches(restaurantId),
  });

  const transfersQuery = useQuery({
    queryKey: ["central-transfers", restaurantId],
    queryFn: () => centralKitchenApi.getTransfers(restaurantId),
  });

  // Mutations
  const createBatchMutation = useMutation({
    mutationFn: () =>
      centralKitchenApi.createProductionBatch({
        restaurantId,
        recipeName,
        targetQuantity,
        unit,
        unitProductionCost: unitCost,
      }),
    onSuccess: () => {
      toast.success("Central production batch recorded successfully!");
      setIsBatchModalOpen(false);
      void queryClient.invalidateQueries({ queryKey: ["central-batches", restaurantId] });
    },
  });

  const dispatchTransferMutation = useMutation({
    mutationFn: () =>
      centralKitchenApi.dispatchOutletTransfer({
        sourceKitchenId: restaurantId,
        destinationOutletId: destinationOutlet,
        ingredientId: "ing-gravy-base",
        itemName: dispatchItemName,
        quantityDispatched: dispatchQty,
        unit: "kg",
        unitCost: 180,
      }),
    onSuccess: () => {
      toast.success("Stock transfer dispatched to outlet!");
      setIsDispatchModalOpen(false);
      void queryClient.invalidateQueries({ queryKey: ["central-transfers", restaurantId] });
    },
  });

  const receiveTransferMutation = useMutation({
    mutationFn: () =>
      centralKitchenApi.receiveOutletTransfer({
        transferId: receivingTransfer!.transferId,
        quantityReceived: receivedQty,
        discrepancyReason: discrepancyReason.trim() || undefined,
      }),
    onSuccess: (data) => {
      if (data.shortageQuantity > 0) {
        toast.warning(
          `Transfer received with ${data.shortageQuantity} kg shortage. Loss variance of ${formatINR(data.varianceLossCost)} recorded.`,
        );
      } else {
        toast.success("Transfer received in full without shortages!");
      }
      setReceivingTransfer(null);
      void queryClient.invalidateQueries({ queryKey: ["central-transfers", restaurantId] });
    },
  });

  const loadPublishPreview = async () => {
    const preview = await centralKitchenApi.previewMenuPublish({
      centralRestaurantId: restaurantId,
      targetOutletIds: ["22222222-0000-0000-0000-000000000001", "22222222-0000-0000-0000-000000000009"],
    });
    setPublishPreview(preview);
    setIsPublishModalOpen(true);
  };

  const executePublishMutation = useMutation({
    mutationFn: () =>
      centralKitchenApi.executeMenuPublish({
        centralRestaurantId: restaurantId,
        targetOutletIds: ["22222222-0000-0000-0000-000000000001", "22222222-0000-0000-0000-000000000009"],
        itemOverrides: [
          { menuItemId: "item-pbm", itemName: "Paneer Butter Masala", centralPrice: 320, outletPriceOverride: pbmOverridePrice },
          { menuItemId: "item-bir", itemName: "Hyderabadi Chicken Biryani", centralPrice: 380, outletPriceOverride: birOverridePrice },
        ],
      }),
    onSuccess: (res) => {
      toast.success(`Published catalog updates across ${res.outletsUpdatedCount} outlets!`);
      setIsPublishModalOpen(false);
    },
  });

  return (
    <div className="space-y-6">
      {/* Header and Tab Selector */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 rounded-3xl border border-border bg-card p-6 shadow-xs">
        <div>
          <div className="flex items-center gap-2">
            <Factory className="h-6 w-6 text-primary" />
            <h2 className="text-xl sm:text-2xl font-bold tracking-tight">Central Kitchen & Head-Office Menu</h2>
          </div>
          <p className="text-xs sm:text-sm text-muted-foreground mt-1">
            Produce semi-finished dish bases, dispatch transfers to outlets with shortage reconciliation, and manage multi-outlet menu catalogs.
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <Button
            size="sm"
            variant={activeTab === "production" ? "default" : "outline"}
            onClick={() => setActiveTab("production")}
            className="text-xs gap-1.5"
          >
            <Layers className="h-3.5 w-3.5" /> Production Batches
          </Button>
          <Button
            size="sm"
            variant={activeTab === "transfers" ? "default" : "outline"}
            onClick={() => setActiveTab("transfers")}
            className="text-xs gap-1.5"
          >
            <Truck className="h-3.5 w-3.5" /> Outlet Transfers
          </Button>
          <Button
            size="sm"
            variant={activeTab === "menu-publish" ? "default" : "outline"}
            onClick={() => setActiveTab("menu-publish")}
            className="text-xs gap-1.5"
          >
            <Store className="h-3.5 w-3.5" /> Menu Publishing
          </Button>
        </div>
      </div>

      {/* Tab 1: Production Batches */}
      {activeTab === "production" && (
        <div className="rounded-3xl border border-border bg-card p-6 shadow-xs space-y-4">
          <div className="flex items-center justify-between">
            <div>
              <h3 className="font-bold text-base">Central Production Runs</h3>
              <p className="text-xs text-muted-foreground">Standardized recipe batches produced for outlet dispatches.</p>
            </div>
            <Button size="sm" onClick={() => setIsBatchModalOpen(true)} className="text-xs gap-1.5">
              <Plus className="h-3.5 w-3.5" /> New Production Batch
            </Button>
          </div>

          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 pt-2">
            {(batchesQuery.data || []).map((batch) => (
              <div key={batch.id} className="rounded-2xl border border-border bg-secondary/20 p-4 space-y-2">
                <div className="flex items-center justify-between">
                  <span className="font-mono text-xs font-bold text-muted-foreground">{batch.batchCode}</span>
                  <Badge variant="outline" className={batch.status === "Completed" ? "bg-emerald-500/10 text-emerald-700" : "bg-amber-500/10 text-amber-700"}>
                    {batch.status}
                  </Badge>
                </div>
                <h4 className="font-semibold text-sm">{batch.recipeName}</h4>
                <div className="flex justify-between text-xs text-muted-foreground pt-1 border-t border-border/60">
                  <span>Target Yield</span>
                  <span className="font-medium text-foreground">{batch.targetQuantity} {batch.unit}</span>
                </div>
                <div className="flex justify-between text-xs text-muted-foreground">
                  <span>Unit Cost</span>
                  <span className="font-medium text-foreground">{formatINR(batch.unitProductionCost)} / {batch.unit}</span>
                </div>
                <div className="flex justify-between text-xs font-semibold pt-1 text-primary">
                  <span>Total Batch Cost</span>
                  <span>{formatINR(batch.totalCost)}</span>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Tab 2: Outlet Stock Transfers */}
      {activeTab === "transfers" && (
        <div className="rounded-3xl border border-border bg-card p-6 shadow-xs space-y-4">
          <div className="flex items-center justify-between">
            <div>
              <h3 className="font-bold text-base">Outlet Dispatches & Dock Receipts</h3>
              <p className="text-xs text-muted-foreground">Trace in-transit transfers, detect delivery shortages, and reconcile cost variances.</p>
            </div>
            <Button size="sm" onClick={() => setIsDispatchModalOpen(true)} className="text-xs gap-1.5">
              <Truck className="h-3.5 w-3.5" /> Dispatch to Outlet
            </Button>
          </div>

          <div className="space-y-3 pt-2">
            {(transfersQuery.data || []).map((trf) => (
              <div key={trf.transferId} className="rounded-2xl border border-border bg-secondary/15 p-4 flex flex-col md:flex-row md:items-center justify-between gap-4">
                <div className="space-y-1">
                  <div className="flex items-center gap-2">
                    <span className="font-mono text-xs font-bold">{trf.transferCode}</span>
                    <Badge
                      variant="outline"
                      className={
                        trf.status === "FullyReceived"
                          ? "bg-emerald-500/10 text-emerald-700"
                          : trf.status === "PartiallyReceived"
                          ? "bg-amber-500/10 text-amber-800"
                          : "bg-blue-500/10 text-blue-700"
                      }
                    >
                      {trf.status === "PartiallyReceived" ? `Shortage (${trf.shortageQuantity} kg)` : trf.status}
                    </Badge>
                  </div>
                  <p className="text-sm font-semibold">{trf.itemName}</p>
                  <p className="text-xs text-muted-foreground">
                    Dispatched: {trf.quantityDispatched} kg ({formatINR(trf.dispatchedCost)}) · Received: {trf.quantityReceived} kg ({formatINR(trf.receivedCost)})
                  </p>
                  {trf.discrepancyReason && (
                    <p className="text-xs text-amber-700 font-medium">⚠️ Shortage note: {trf.discrepancyReason}</p>
                  )}
                </div>

                <div className="flex items-center gap-3">
                  {trf.shortageQuantity > 0 && (
                    <div className="text-right">
                      <span className="text-[11px] text-muted-foreground block">Variance Loss</span>
                      <span className="text-xs font-bold text-destructive">-{formatINR(trf.varianceLossCost)}</span>
                    </div>
                  )}

                  {trf.status === "Dispatched" && (
                    <Button
                      size="sm"
                      variant="outline"
                      onClick={() => {
                        setReceivingTransfer(trf);
                        setReceivedQty(trf.quantityDispatched);
                        setDiscrepancyReason("");
                      }}
                      className="text-xs gap-1.5"
                    >
                      <CheckCircle2 className="h-3.5 w-3.5 text-emerald-600" /> Receive at Dock
                    </Button>
                  )}
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Tab 3: Head-Office Menu Publishing */}
      {activeTab === "menu-publish" && (
        <div className="rounded-3xl border border-border bg-card p-6 shadow-xs space-y-4">
          <div className="flex items-center justify-between">
            <div>
              <h3 className="font-bold text-base">Head-Office Multi-Outlet Catalog Sync</h3>
              <p className="text-xs text-muted-foreground">Publish master recipes and dishes to branch outlets with localized price and availability rules.</p>
            </div>
            <Button size="sm" onClick={loadPublishPreview} className="text-xs gap-1.5">
              <Sparkles className="h-3.5 w-3.5" /> Preview & Publish Catalog
            </Button>
          </div>

          <div className="grid gap-4 sm:grid-cols-2 pt-2">
            <div className="rounded-2xl border border-border p-4 bg-secondary/15 space-y-2">
              <div className="flex items-center justify-between">
                <h4 className="font-semibold text-sm">Tablo Heritage Restaurant</h4>
                <Badge variant="outline" className="bg-emerald-500/10 text-emerald-700">Flagship Outlet</Badge>
              </div>
              <p className="text-xs text-muted-foreground">Connaught Place · New Delhi</p>
              <div className="pt-2 text-xs space-y-1">
                <div className="flex justify-between">
                  <span>Catalog Status:</span>
                  <span className="font-medium text-emerald-600">Synced with Master</span>
                </div>
                <div className="flex justify-between">
                  <span>Local Price Overrides:</span>
                  <span className="font-medium">Active (Metro Tier)</span>
                </div>
              </div>
            </div>

            <div className="rounded-2xl border border-border p-4 bg-secondary/15 space-y-2">
              <div className="flex items-center justify-between">
                <h4 className="font-semibold text-sm">Coastal Heritage Bistro</h4>
                <Badge variant="outline" className="bg-blue-500/10 text-blue-700">Branch Outlet</Badge>
              </div>
              <p className="text-xs text-muted-foreground">Marine Drive · Mumbai</p>
              <div className="pt-2 text-xs space-y-1">
                <div className="flex justify-between">
                  <span>Catalog Status:</span>
                  <span className="font-medium text-emerald-600">Synced with Master</span>
                </div>
                <div className="flex justify-between">
                  <span>Local Price Overrides:</span>
                  <span className="font-medium">Active (Coastal Tier +10%)</span>
                </div>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* New Batch Modal */}
      <Dialog open={isBatchModalOpen} onOpenChange={setIsBatchModalOpen}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>New Central Production Batch</DialogTitle>
          </DialogHeader>
          <div className="space-y-3 py-2 text-sm">
            <div>
              <label className="block text-xs font-semibold mb-1">Recipe / Base Name</label>
              <Input value={recipeName} onChange={(e) => setRecipeName(e.target.value)} />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="block text-xs font-semibold mb-1">Target Yield</label>
                <Input type="number" value={targetQuantity} onChange={(e) => setTargetQuantity(Number(e.target.value))} />
              </div>
              <div>
                <label className="block text-xs font-semibold mb-1">Unit</label>
                <Input value={unit} onChange={(e) => setUnit(e.target.value)} />
              </div>
            </div>
            <div>
              <label className="block text-xs font-semibold mb-1">Unit Production Cost (₹)</label>
              <Input type="number" value={unitCost} onChange={(e) => setUnitCost(Number(e.target.value))} />
            </div>
            <div className="p-3 rounded-xl bg-primary/5 text-xs text-primary font-semibold flex justify-between">
              <span>Estimated Batch Total:</span>
              <span>{formatINR(targetQuantity * unitCost)}</span>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsBatchModalOpen(false)}>Cancel</Button>
            <Button onClick={() => createBatchMutation.mutate()}>Post Batch</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Receive Transfer Modal */}
      <Dialog open={!!receivingTransfer} onOpenChange={(open) => !open && setReceivingTransfer(null)}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>Receive Transfer at Outlet Dock</DialogTitle>
          </DialogHeader>
          {receivingTransfer && (
            <div className="space-y-3 py-2 text-sm">
              <p className="text-xs text-muted-foreground">
                Item: <strong>{receivingTransfer.itemName}</strong> · Dispatched: <strong>{receivingTransfer.quantityDispatched} kg</strong>
              </p>
              <div>
                <label className="block text-xs font-semibold mb-1">Physical Quantity Received (kg)</label>
                <Input
                  type="number"
                  value={receivedQty}
                  onChange={(e) => setReceivedQty(Number(e.target.value))}
                />
              </div>

              {receivedQty < receivingTransfer.quantityDispatched && (
                <div className="p-3 rounded-xl bg-amber-500/10 border border-amber-500/30 text-amber-900 text-xs space-y-1">
                  <p className="font-bold flex items-center gap-1">
                    <AlertTriangle className="h-3.5 w-3.5 text-amber-700" />
                    Shortage Detected: {(receivingTransfer.quantityDispatched - receivedQty).toFixed(1)} kg
                  </p>
                  <p>
                    Estimated variance loss:{" "}
                    <strong>
                      {formatINR(
                        ((receivingTransfer.quantityDispatched - receivedQty) * receivingTransfer.dispatchedCost) /
                          receivingTransfer.quantityDispatched,
                      )}
                    </strong>
                  </p>
                  <label className="block font-semibold pt-1">Discrepancy Audit Reason</label>
                  <Input
                    placeholder="e.g. Broken container seal, transit spillage"
                    value={discrepancyReason}
                    onChange={(e) => setDiscrepancyReason(e.target.value)}
                    className="text-xs"
                  />
                </div>
              )}
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setReceivingTransfer(null)}>Cancel</Button>
            <Button onClick={() => receiveTransferMutation.mutate()}>Confirm Receipt</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Menu Publish Modal */}
      <Dialog open={isPublishModalOpen} onOpenChange={setIsPublishModalOpen}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Head-Office Multi-Outlet Menu Publishing</DialogTitle>
          </DialogHeader>
          <div className="space-y-3 py-2 text-sm">
            <p className="text-xs text-muted-foreground">
              Review master item definitions and set branch-specific price overrides before broadcasting.
            </p>

            <div className="space-y-2 border rounded-xl p-3 bg-secondary/15">
              <div className="flex items-center justify-between">
                <div>
                  <p className="font-semibold text-xs">Paneer Butter Masala</p>
                  <span className="text-[11px] text-muted-foreground">Master Base Price: ₹320</span>
                </div>
                <div className="flex items-center gap-2">
                  <span className="text-xs text-muted-foreground">Branch Override:</span>
                  <Input
                    type="number"
                    value={pbmOverridePrice}
                    onChange={(e) => setPbmOverridePrice(Number(e.target.value))}
                    className="w-24 text-xs h-8"
                  />
                </div>
              </div>

              <div className="flex items-center justify-between pt-2 border-t">
                <div>
                  <p className="font-semibold text-xs">Hyderabadi Chicken Biryani</p>
                  <span className="text-[11px] text-muted-foreground">Master Base Price: ₹380</span>
                </div>
                <div className="flex items-center gap-2">
                  <span className="text-xs text-muted-foreground">Branch Override:</span>
                  <Input
                    type="number"
                    value={birOverridePrice}
                    onChange={(e) => setBirOverridePrice(Number(e.target.value))}
                    className="w-24 text-xs h-8"
                  />
                </div>
              </div>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsPublishModalOpen(false)}>Cancel</Button>
            <Button onClick={() => executePublishMutation.mutate()}>Broadcast to 2 Outlets</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
