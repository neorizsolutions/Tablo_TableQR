import { useState } from "react";
import { useQuery, useMutation } from "@tanstack/react-query";
import { QRCodeSVG } from "qrcode.react";
import { toast } from "sonner";
import {
  ShoppingBag,
  Clock,
  MapPin,
  UtensilsCrossed,
  CheckCircle2,
  Calendar,
  AlertCircle,
  Truck,
  ArrowRight,
  ShieldCheck,
  ExternalLink,
  Copy,
} from "lucide-react";
import { formatINR } from "@/lib/mock-data";
import { storefrontApi, type StorefrontOutletSummary, type StorefrontCheckoutResponse } from "@/services/storefront";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";

export function StorefrontOrdering() {
  const [selectedOutletId, setSelectedOutletId] = useState("22222222-0000-0000-0000-000000000001");
  const [channel, setChannel] = useState<"DirectDelivery" | "TakeawayPickup" | "DineInDirect">("DirectDelivery");
  const [timing, setTiming] = useState<"Immediate" | "Scheduled">("Immediate");
  const [scheduledTime, setScheduledTime] = useState("2026-11-22T19:30");

  const [customerName, setCustomerName] = useState("Aarav Mehta");
  const [customerPhone, setCustomerPhone] = useState("9823456789");
  const [customerAddress, setCustomerAddress] = useState("Apt 402, Heritage Residency, Connaught Place");
  const [paymentMethod, setPaymentMethod] = useState<"UpiOnline" | "PayOnDelivery">("UpiOnline");

  // Sample Cart
  const [cartItems, setCartItems] = useState([
    {
      menuItemId: "item-pbm",
      name: "Paneer Butter Masala",
      quantity: 1,
      unitPrice: 350,
      modifiers: [{ modifierOptionId: "opt-spice", optionName: "Medium Spicy", priceDelta: 0 }],
      cookingNotes: "Make it rich and creamy",
    },
    {
      menuItemId: "item-naan",
      name: "Butter Garlic Naan",
      quantity: 2,
      unitPrice: 75,
      modifiers: [{ modifierOptionId: "opt-extra-butter", optionName: "Extra Butter", priceDelta: 15 }],
    },
  ]);

  const [checkoutResult, setCheckoutResult] = useState<StorefrontCheckoutResponse | null>(null);

  const outletsQuery = useQuery({
    queryKey: ["storefront-outlets"],
    queryFn: () => storefrontApi.getOutlets(),
  });

  const availableOutlets = outletsQuery.data || [];
  const outlet = availableOutlets.find((o) => o.outletId === selectedOutletId) || availableOutlets[0] || {
    outletId: "22222222-0000-0000-0000-000000000001",
    restaurantId: "22222222-0000-0000-0000-000000000001",
    name: "Tablo Heritage Restaurant",
    address: "Block B, Connaught Place, New Delhi",
    phone: "+91 98765 43210",
    isOpenNow: true,
    operatingHours: "11:00 AM - 11:30 PM",
    leadTimeMinutes: 25,
    minOrderAmount: 200,
    deliveryBaseFee: 40,
    freeDeliveryAboveAmount: 799,
    isPublished: true,
    isTemporarilyClosed: false,
  };

  const subtotal = cartItems.reduce((s, item) => {
    const modTotal = (item.modifiers || []).reduce((ms, m) => ms + m.priceDelta, 0);
    return s + (item.unitPrice + modTotal) * item.quantity;
  }, 0);

  const tax = Math.round(subtotal * 0.05);
  const deliveryFee = channel === "DirectDelivery" ? (subtotal >= outlet.freeDeliveryAboveAmount ? 0 : outlet.deliveryBaseFee) : 0;
  const packagingFee = channel === "TakeawayPickup" || channel === "DirectDelivery" ? 15 : 0;
  const totalPayable = subtotal + tax + deliveryFee + packagingFee;
  const meetsMinOrder = subtotal >= outlet.minOrderAmount;
  const canPlaceImmediate = outlet.isOpenNow && !outlet.isTemporarilyClosed;

  const checkoutMutation = useMutation({
    mutationFn: (idempotencyKey: string) =>
      storefrontApi.checkout({
        outletId: outlet.outletId,
        orderChannel: channel,
        fulfillmentTiming: timing,
        scheduledForUtc: timing === "Scheduled" ? new Date(scheduledTime).toISOString() : undefined,
        customerName,
        customerPhone,
        deliveryAddress: channel === "DirectDelivery" ? customerAddress : undefined,
        items: cartItems,
        paymentMethod,
        idempotencyKey,
      }),
    onSuccess: (data) => {
      setCheckoutResult(data);
      if (data.isReplay) {
        toast.info(`Retrieved existing order #${data.orderNumber} (Idempotent response)`);
      } else {
        toast.success(`Storefront order #${data.orderNumber} placed successfully!`);
      }
    },
    onError: (err: any) => {
      toast.error(err.message || "Failed to place order.");
    },
  });

  const handlePlaceOrder = () => {
    if (!meetsMinOrder) {
      toast.error(`Minimum order amount is ${formatINR(outlet.minOrderAmount)}. Please add more items.`);
      return;
    }
    if (timing === "Immediate" && !canPlaceImmediate) {
      toast.error("This outlet is currently closed. Please select 'Schedule for Later' during operating hours.");
      return;
    }
    // Generate durable key for submission
    const key = `idem-storefront-${customerPhone}-${subtotal}-${cartItems.length}`;
    checkoutMutation.mutate(key);
  };

  if (!outletsQuery.isLoading && availableOutlets.length === 0) {
    return (
      <div className="mx-auto max-w-xl text-center py-16 px-4 space-y-4">
        <div className="h-16 w-16 rounded-full bg-muted flex items-center justify-center mx-auto text-muted-foreground">
          <UtensilsCrossed className="h-8 w-8" />
        </div>
        <h2 className="text-xl font-bold font-display">No Outlets Available</h2>
        <p className="text-sm text-muted-foreground">
          There are currently no published restaurant outlets accepting online orders in this location. Please check back later.
        </p>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-4xl space-y-6 p-4">
      {/* Brand Header */}
      <div className="rounded-3xl border border-border bg-card p-6 shadow-xs flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <div className="flex items-center gap-2 mb-2">
            <Badge variant="outline" className="bg-primary/10 text-primary">
              Official Digital Storefront
            </Badge>
            {outlet.isTemporarilyClosed ? (
              <Badge variant="destructive" className="text-xs">
                Temporarily Closed
              </Badge>
            ) : outlet.isOpenNow ? (
              <Badge className="bg-emerald-600 text-white text-xs">
                Open Now
              </Badge>
            ) : (
              <Badge variant="secondary" className="text-amber-700 bg-amber-500/10 text-xs">
                Closed Now
              </Badge>
            )}
          </div>
          <h2 className="text-2xl font-bold font-display">{outlet.name}</h2>
          <p className="text-xs text-muted-foreground mt-0.5 flex items-center gap-1.5">
            <MapPin className="h-3.5 w-3.5" /> {outlet.address} · {outlet.operatingHours}
          </p>
          {outlet.isTemporarilyClosed && outlet.closureReason && (
            <p className="text-xs text-destructive font-medium mt-1">
              Notice: {outlet.closureReason}
            </p>
          )}
        </div>

        {availableOutlets.length > 1 && (
          <div className="flex items-center gap-2">
            <select
              value={outlet.outletId}
              onChange={(e) => setSelectedOutletId(e.target.value)}
              className="rounded-full border border-border bg-background px-3 py-1.5 text-xs font-semibold"
            >
              {availableOutlets.map((o) => (
                <option key={o.outletId} value={o.outletId}>
                  {o.name} {o.isOpenNow ? "· Open" : "· Closed"}
                </option>
              ))}
            </select>
          </div>
        )}
      </div>

      {!checkoutResult ? (
        <div className="grid gap-6 md:grid-cols-3">
          {/* Channel and Fulfillment Settings */}
          <div className="md:col-span-2 space-y-4">
            <div className="rounded-3xl border border-border bg-card p-5 space-y-4 shadow-xs">
              <h3 className="font-bold text-sm">1. Delivery or Pickup Channel</h3>
              <div className="grid grid-cols-3 gap-2">
                {(
                  [
                    { id: "DirectDelivery", label: "Direct Delivery", icon: Truck },
                    { id: "TakeawayPickup", label: "Takeaway Pickup", icon: ShoppingBag },
                    { id: "DineInDirect", label: "Dine-In Order", icon: UtensilsCrossed },
                  ] as const
                ).map((c) => {
                  const Icon = c.icon;
                  const isSelected = channel === c.id;
                  return (
                    <button
                      key={c.id}
                      type="button"
                      onClick={() => setChannel(c.id)}
                      className={`flex flex-col items-center gap-2 p-3 rounded-2xl border text-xs font-semibold transition ${
                        isSelected ? "border-primary bg-primary/10 text-primary" : "border-border bg-secondary/20 hover:bg-secondary/40 text-muted-foreground"
                      }`}
                    >
                      <Icon className="h-4 w-4" />
                      <span>{c.label}</span>
                    </button>
                  );
                })}
              </div>

              <div className="pt-2 border-t">
                <h4 className="text-xs font-semibold mb-2">Fulfillment Window</h4>
                <div className="flex gap-2">
                  <button
                    type="button"
                    onClick={() => setTiming("Immediate")}
                    className={`flex-1 p-2 rounded-xl border text-xs font-medium ${
                      timing === "Immediate" ? "border-primary bg-primary/10 text-primary font-bold" : "border-border"
                    }`}
                  >
                    Immediate (~{outlet.leadTimeMinutes} mins)
                  </button>
                  <button
                    type="button"
                    onClick={() => setTiming("Scheduled")}
                    className={`flex-1 p-2 rounded-xl border text-xs font-medium ${
                      timing === "Scheduled" ? "border-primary bg-primary/10 text-primary font-bold" : "border-border"
                    }`}
                  >
                    Schedule for Later
                  </button>
                </div>
                {timing === "Scheduled" && (
                  <div className="mt-2">
                    <Input
                      type="datetime-local"
                      value={scheduledTime}
                      onChange={(e) => setScheduledTime(e.target.value)}
                      className="text-xs"
                    />
                    <p className="text-[11px] text-muted-foreground mt-1">
                      Operating hours: {outlet.operatingHours} (Min lead time: {outlet.leadTimeMinutes} mins)
                    </p>
                  </div>
                )}
                {timing === "Immediate" && !canPlaceImmediate && (
                  <div className="mt-2 flex items-center gap-1.5 p-2 rounded-xl bg-amber-500/10 text-amber-800 text-xs font-medium">
                    <AlertCircle className="h-4 w-4 shrink-0" />
                    <span>This outlet is currently closed. Please choose "Schedule for Later" during operating hours ({outlet.operatingHours}).</span>
                  </div>
                )}
              </div>
            </div>

            {/* Customer Details */}
            <div className="rounded-3xl border border-border bg-card p-5 space-y-3 shadow-xs">
              <h3 className="font-bold text-sm">2. Customer Details</h3>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-semibold mb-1">Full Name</label>
                  <Input value={customerName} onChange={(e) => setCustomerName(e.target.value)} className="text-xs" />
                </div>
                <div>
                  <label className="block text-xs font-semibold mb-1">Phone Number</label>
                  <Input value={customerPhone} onChange={(e) => setCustomerPhone(e.target.value)} className="text-xs" />
                </div>
              </div>
              {channel === "DirectDelivery" && (
                <div>
                  <label className="block text-xs font-semibold mb-1">Delivery Address</label>
                  <Input value={customerAddress} onChange={(e) => setCustomerAddress(e.target.value)} className="text-xs" />
                </div>
              )}
            </div>
          </div>

          {/* Cart & Checkout Summary */}
          <div className="rounded-3xl border border-border bg-card p-5 space-y-4 shadow-xs h-fit">
            <h3 className="font-bold text-sm">Order Summary</h3>
            <div className="space-y-2 text-xs divide-y divide-border">
              {cartItems.map((item, idx) => (
                <div key={idx} className="pt-2 first:pt-0">
                  <div className="flex justify-between font-semibold">
                    <span>{item.quantity}x {item.name}</span>
                    <span>{formatINR(item.unitPrice * item.quantity)}</span>
                  </div>
                  {item.modifiers?.map((m, mi) => (
                    <p key={mi} className="text-[11px] text-muted-foreground">
                      + {m.optionName} {m.priceDelta > 0 ? `(${formatINR(m.priceDelta)})` : ""}
                    </p>
                  ))}
                  {item.cookingNotes && (
                    <p className="text-[10px] text-amber-700 italic">"{item.cookingNotes}"</p>
                  )}
                </div>
              ))}
            </div>

            <div className="pt-3 border-t space-y-1.5 text-xs text-muted-foreground">
              <div className="flex justify-between">
                <span>Subtotal</span>
                <span className="font-medium text-foreground">{formatINR(subtotal)}</span>
              </div>
              <div className="flex justify-between">
                <span>GST (5%)</span>
                <span className="font-medium text-foreground">{formatINR(tax)}</span>
              </div>
              {packagingFee > 0 && (
                <div className="flex justify-between">
                  <span>Eco Packaging</span>
                  <span className="font-medium text-foreground">{formatINR(packagingFee)}</span>
                </div>
              )}
              {deliveryFee > 0 ? (
                <div className="flex justify-between">
                  <span>Delivery Fee</span>
                  <span className="font-medium text-foreground">{formatINR(deliveryFee)}</span>
                </div>
              ) : channel === "DirectDelivery" ? (
                <div className="flex justify-between text-emerald-600 font-medium">
                  <span>Delivery</span>
                  <span>FREE</span>
                </div>
              ) : null}
              <div className="flex justify-between pt-2 border-t text-sm font-bold text-foreground">
                <span>Total Payable</span>
                <span className="text-primary">{formatINR(totalPayable)}</span>
              </div>
            </div>

            {!meetsMinOrder && (
              <div className="p-2.5 rounded-xl bg-destructive/10 text-destructive text-xs font-semibold">
                Min. order is {formatINR(outlet.minOrderAmount)}. Add {formatINR(outlet.minOrderAmount - subtotal)} more to checkout.
              </div>
            )}

            <div className="pt-2">
              <label className="block text-xs font-semibold mb-1">Payment Method</label>
              <select
                value={paymentMethod}
                onChange={(e) => setPaymentMethod(e.target.value as any)}
                className="w-full rounded-xl border border-border bg-background p-2 text-xs font-semibold"
              >
                <option value="UpiOnline">UPI Instant Payment (GPay, PhonePe, Paytm)</option>
                <option value="PayOnDelivery">Pay on Delivery / Collection</option>
              </select>
            </div>

            <Button
              onClick={handlePlaceOrder}
              disabled={checkoutMutation.isPending || !meetsMinOrder || (timing === "Immediate" && !canPlaceImmediate)}
              className="w-full rounded-full gap-2 font-bold"
            >
              <span>Place Order · {formatINR(totalPayable)}</span>
              <ArrowRight className="h-4 w-4" />
            </Button>
          </div>
        </div>
      ) : (
        /* Order Placed Confirmation View with Order Tracking */
        <div className="rounded-3xl border border-border bg-card p-6 shadow-xs text-center space-y-4 max-w-md mx-auto">
          <div className="h-12 w-12 rounded-full bg-emerald-500/10 text-emerald-600 flex items-center justify-center mx-auto">
            <CheckCircle2 className="h-6 w-6" />
          </div>

          <div>
            <span className="text-xs font-bold text-muted-foreground uppercase tracking-wider">Order Confirmed</span>
            <h3 className="text-xl font-bold font-display mt-0.5">Order #{checkoutResult.orderNumber}</h3>
            <p className="text-xs text-muted-foreground">Token: <strong>{checkoutResult.token}</strong> · Status: {checkoutResult.status}</p>
          </div>

          {checkoutResult.upiUri && (
            <div className="p-4 rounded-2xl border border-dashed border-border bg-secondary/15 space-y-2">
              <p className="text-xs font-bold">Pay {formatINR(checkoutResult.totalPayable)} with UPI</p>
              <div className="flex justify-center py-1">
                <QRCodeSVG value={checkoutResult.upiUri} size={160} marginSize={2} />
              </div>
              <p className="text-[11px] text-muted-foreground">{checkoutResult.payeeVpa}</p>
              <a
                href={checkoutResult.upiUri}
                className="inline-block text-xs font-bold text-emerald-600 underline"
              >
                Open UPI App
              </a>
            </div>
          )}

          {checkoutResult.trackingToken && (
            <div className="p-3 rounded-2xl border border-border bg-muted/30 text-left space-y-1.5">
              <div className="flex items-center gap-1.5 text-xs font-bold text-foreground">
                <ShieldCheck className="h-4 w-4 text-primary" />
                <span>Private Tracking Link</span>
              </div>
              <p className="text-[11px] text-muted-foreground break-all">
                Order Token: <code className="bg-background px-1.5 py-0.5 rounded border">{checkoutResult.trackingToken}</code>
              </p>
              <button
                type="button"
                onClick={() => {
                  navigator.clipboard.writeText(`${window.location.origin}/order/track/${checkoutResult.trackingToken}`);
                  toast.success("Tracking link copied to clipboard!");
                }}
                className="inline-flex items-center gap-1 text-[11px] font-semibold text-primary hover:underline"
              >
                <Copy className="h-3 w-3" /> Copy Secure Order Tracking Link
              </button>
            </div>
          )}

          <div className="text-xs text-muted-foreground pt-2 border-t">
            <p>Estimated fulfillment: <strong>{new Date(checkoutResult.estimatedFulfillmentUtc).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}</strong></p>
          </div>

          <Button variant="outline" size="sm" onClick={() => setCheckoutResult(null)} className="rounded-full text-xs">
            Back to Menu
          </Button>
        </div>
      )}
    </div>
  );
}
