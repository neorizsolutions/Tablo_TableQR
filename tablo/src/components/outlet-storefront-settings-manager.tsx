import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import {
  Store,
  Clock,
  ShieldCheck,
  MapPin,
  Phone,
  DollarSign,
  AlertTriangle,
  CheckCircle2,
  Save,
  Truck,
} from "lucide-react";
import { formatINR } from "@/lib/mock-data";
import { storefrontApi, type StorefrontOutletSummary } from "@/services/storefront";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";

export function OutletStorefrontSettingsManager({ restaurantId }: { restaurantId: string }) {
  const queryClient = useQueryClient();
  const [selectedOutletId, setSelectedOutletId] = useState<string>("");

  const outletsQuery = useQuery({
    queryKey: ["admin-outlets", restaurantId],
    queryFn: () => storefrontApi.getOutlets(restaurantId),
  });

  const outlets = outletsQuery.data || [];
  const activeOutlet = outlets.find((o) => o.outletId === selectedOutletId) || outlets[0];

  // Local form state
  const [isOpen, setIsOpen] = useState(true);
  const [isPublished, setIsPublished] = useState(true);
  const [openingTime, setOpeningTime] = useState("11:00");
  const [closingTime, setClosingTime] = useState("23:30");
  const [isOvernight, setIsOvernight] = useState(false);
  const [leadTimeMinutes, setLeadTimeMinutes] = useState(25);
  const [minOrderAmount, setMinOrderAmount] = useState(200);
  const [deliveryBaseFee, setDeliveryBaseFee] = useState(40);
  const [freeDeliveryAboveAmount, setFreeDeliveryAboveAmount] = useState(799);
  const [phone, setPhone] = useState("+91 98765 43210");
  const [isTemporarilyClosed, setIsTemporarilyClosed] = useState(false);
  const [closureReason, setClosureReason] = useState("");

  // Sync state when activeOutlet changes
  const [syncedOutletId, setSyncedOutletId] = useState<string | null>(null);
  if (activeOutlet && activeOutlet.outletId !== syncedOutletId) {
    setSyncedOutletId(activeOutlet.outletId);
    setIsOpen(activeOutlet.isOpenNow);
    setIsPublished(activeOutlet.isPublished ?? true);
    const [op, cl] = (activeOutlet.operatingHours || "11:00 - 23:30").split(" - ");
    setOpeningTime(op || "11:00");
    setClosingTime(cl || "23:30");
    setLeadTimeMinutes(activeOutlet.leadTimeMinutes || 25);
    setMinOrderAmount(activeOutlet.minOrderAmount || 200);
    setDeliveryBaseFee(activeOutlet.deliveryBaseFee || 40);
    setFreeDeliveryAboveAmount(activeOutlet.freeDeliveryAboveAmount || 799);
    setPhone(activeOutlet.phone || "+91 98765 43210");
    setIsTemporarilyClosed(activeOutlet.isTemporarilyClosed || false);
    setClosureReason(activeOutlet.closureReason || "");
  }

  const updateMutation = useMutation({
    mutationFn: (updated: Partial<StorefrontOutletSummary>) =>
      storefrontApi.updateOutletSettings(activeOutlet.outletId, updated),
    onSuccess: () => {
      toast.success("Outlet settings saved successfully!");
      void queryClient.invalidateQueries({ queryKey: ["admin-outlets", restaurantId] });
      void queryClient.invalidateQueries({ queryKey: ["storefront-outlets"] });
    },
    onError: (err: any) => {
      toast.error(err.message || "Failed to update outlet settings.");
    },
  });

  const handleSave = () => {
    if (!activeOutlet) return;
    updateMutation.mutate({
      isOpen,
      isPublished,
      openingTime,
      closingTime,
      isOvernight: closingTime < openingTime,
      leadTimeMinutes: Number(leadTimeMinutes),
      minOrderAmount: Number(minOrderAmount),
      deliveryBaseFee: Number(deliveryBaseFee),
      freeDeliveryAboveAmount: Number(freeDeliveryAboveAmount),
      phone,
      isTemporarilyClosed,
      closureReason: closureReason.trim() || undefined,
    });
  };

  if (outletsQuery.isLoading) {
    return <div className="p-6 text-sm text-muted-foreground">Loading outlet configurations...</div>;
  }

  if (outlets.length === 0) {
    return (
      <div className="rounded-3xl border border-dashed p-8 text-center space-y-2">
        <Store className="h-8 w-8 text-muted-foreground mx-auto" />
        <h3 className="font-semibold text-sm">No Outlets Configured</h3>
        <p className="text-xs text-muted-foreground">
          No outlets exist for this restaurant. Create an outlet in settings before enabling the digital storefront.
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Header and Outlet Selector */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 rounded-3xl border bg-card p-6 shadow-xs">
        <div>
          <div className="flex items-center gap-2 mb-1">
            <Badge variant="outline" className="bg-primary/10 text-primary text-xs">
              Direct Ordering & Outlet Config
            </Badge>
            {isTemporarilyClosed ? (
              <Badge variant="destructive" className="text-xs">
                Temporarily Closed
              </Badge>
            ) : isOpen ? (
              <Badge className="bg-emerald-600 text-white text-xs">
                Active & Accepting Orders
              </Badge>
            ) : (
              <Badge variant="secondary" className="text-xs">
                Closed
              </Badge>
            )}
          </div>
          <h2 className="text-xl font-bold font-display">{activeOutlet?.name}</h2>
          <p className="text-xs text-muted-foreground mt-0.5 flex items-center gap-1.5">
            <MapPin className="h-3.5 w-3.5" /> {activeOutlet?.address}
          </p>
        </div>

        {outlets.length > 1 && (
          <div className="flex items-center gap-2">
            <label className="text-xs font-semibold text-muted-foreground">Select Outlet:</label>
            <select
              value={activeOutlet?.outletId}
              onChange={(e) => setSelectedOutletId(e.target.value)}
              className="rounded-full border border-border bg-background px-3 py-1.5 text-xs font-semibold"
            >
              {outlets.map((o) => (
                <option key={o.outletId} value={o.outletId}>
                  {o.name}
                </option>
              ))}
            </select>
          </div>
        )}
      </div>

      <div className="grid gap-6 md:grid-cols-2">
        {/* Operating Hours & Closures */}
        <div className="rounded-3xl border bg-card p-6 space-y-4 shadow-xs">
          <div className="flex items-center gap-2 border-b pb-3">
            <Clock className="h-4 w-4 text-primary" />
            <h3 className="font-bold text-sm">Operating Hours & Closures</h3>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-semibold mb-1">Opening Time</label>
              <Input
                type="time"
                value={openingTime}
                onChange={(e) => setOpeningTime(e.target.value)}
                className="text-xs"
              />
            </div>
            <div>
              <label className="block text-xs font-semibold mb-1">Closing Time</label>
              <Input
                type="time"
                value={closingTime}
                onChange={(e) => setClosingTime(e.target.value)}
                className="text-xs"
              />
            </div>
          </div>
          {closingTime < openingTime && (
            <p className="text-[11px] text-amber-700 font-medium">
              Overnight operating hours detected: Open from {openingTime} until {closingTime} the next morning.
            </p>
          )}

          <div className="pt-2 border-t space-y-3">
            <div className="flex items-center justify-between">
              <div>
                <label className="text-xs font-semibold block">Accepting Orders (Master Switch)</label>
                <span className="text-[11px] text-muted-foreground">When disabled, customer cannot place new orders.</span>
              </div>
              <input
                type="checkbox"
                checked={isOpen}
                onChange={(e) => setIsOpen(e.target.checked)}
                className="h-4 w-4 rounded accent-primary cursor-pointer"
              />
            </div>

            <div className="flex items-center justify-between">
              <div>
                <label className="text-xs font-semibold block">Publish on Digital Storefront</label>
                <span className="text-[11px] text-muted-foreground">When unchecked, outlet is hidden from public guests.</span>
              </div>
              <input
                type="checkbox"
                checked={isPublished}
                onChange={(e) => setIsPublished(e.target.checked)}
                className="h-4 w-4 rounded accent-primary cursor-pointer"
              />
            </div>

            <div className="flex items-center justify-between">
              <div>
                <label className="text-xs font-semibold block text-destructive">Temporary Emergency Closure</label>
                <span className="text-[11px] text-muted-foreground">Pause ordering immediately (e.g. weather, rush).</span>
              </div>
              <input
                type="checkbox"
                checked={isTemporarilyClosed}
                onChange={(e) => setIsTemporarilyClosed(e.target.checked)}
                className="h-4 w-4 rounded accent-destructive cursor-pointer"
              />
            </div>

            {isTemporarilyClosed && (
              <div>
                <label className="block text-xs font-semibold mb-1">Customer-Facing Closure Reason</label>
                <Input
                  value={closureReason}
                  onChange={(e) => setClosureReason(e.target.value)}
                  placeholder="e.g. Kitchen deep cleaning, reopening at 6:00 PM"
                  className="text-xs"
                />
              </div>
            )}
          </div>
        </div>

        {/* Pricing, Delivery & Lead Time Rules */}
        <div className="rounded-3xl border bg-card p-6 space-y-4 shadow-xs">
          <div className="flex items-center gap-2 border-b pb-3">
            <Truck className="h-4 w-4 text-primary" />
            <h3 className="font-bold text-sm">Fulfillment, Lead Times & Delivery Rules</h3>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-semibold mb-1">Prep Lead Time (Mins)</label>
              <Input
                type="number"
                min={5}
                value={leadTimeMinutes}
                onChange={(e) => setLeadTimeMinutes(Number(e.target.value))}
                className="text-xs"
              />
            </div>
            <div>
              <label className="block text-xs font-semibold mb-1">Min. Order Amount (₹)</label>
              <Input
                type="number"
                min={0}
                value={minOrderAmount}
                onChange={(e) => setMinOrderAmount(Number(e.target.value))}
                className="text-xs"
              />
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-semibold mb-1">Delivery Base Fee (₹)</label>
              <Input
                type="number"
                min={0}
                value={deliveryBaseFee}
                onChange={(e) => setDeliveryBaseFee(Number(e.target.value))}
                className="text-xs"
              />
            </div>
            <div>
              <label className="block text-xs font-semibold mb-1">Free Delivery Threshold (₹)</label>
              <Input
                type="number"
                min={0}
                value={freeDeliveryAboveAmount}
                onChange={(e) => setFreeDeliveryAboveAmount(Number(e.target.value))}
                className="text-xs"
              />
            </div>
          </div>

          <div>
            <label className="block text-xs font-semibold mb-1">Storefront Contact Phone</label>
            <Input
              value={phone}
              onChange={(e) => setPhone(e.target.value)}
              className="text-xs"
            />
          </div>

          <div className="pt-3 border-t flex justify-end">
            <Button
              onClick={handleSave}
              disabled={updateMutation.isPending}
              className="rounded-full gap-2 font-bold px-6"
            >
              <Save className="h-4 w-4" />
              <span>{updateMutation.isPending ? "Saving..." : "Save Outlet Configuration"}</span>
            </Button>
          </div>
        </div>
      </div>
    </div>
  );
}
