import React, { useState, useEffect } from "react";
import { QRCodeSVG } from "qrcode.react";
import type { UpiQrResponseDto } from "@/services/parcel";
import {
  Package,
  Search,
  Plus,
  Minus,
  CheckCircle2,
  Printer,
  RefreshCw,
  Clock,
  User,
  Phone,
  Settings,
  Receipt,
  FileText,
  AlertCircle,
  IndianRupee,
  ShieldAlert,
  X,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from "@/components/ui/dialog";
import {
  parcelApi,
  ParcelOrderResponseDto,
  ParcelSettingsDto,
} from "../services/parcel";
import { MenuItem } from "@/types";
import { getAdminMenu } from "@/services/api";
import {
  SavedParcelPaymentOperation,
  readSavedParcelPayment,
  getParcelPaymentStorageKey,
} from "@/hooks/use-parcel-payment";
import { ParcelRecovery } from "@/components/parcel-recovery";
import { ApiError } from "@/services/apiClient";

interface ParcelCounterProps {
  restaurantId: string;
  employeeId?: string;
  onClose?: () => void;
}

export function ParcelCounter({ restaurantId, employeeId, onClose }: ParcelCounterProps) {
  const effectiveEmployeeId = employeeId || "cashier";
  const [activeTab, setActiveTab] = useState<"new" | "orders" | "payouts" | "settings">("new");
  const [menuItems, setMenuItems] = useState<MenuItem[]>([]);
  const [selectedCategory, setSelectedCategory] = useState<string>("all");
  const [searchQuery, setSearchQuery] = useState("");
  const [cart, setCart] = useState<{ [itemId: string]: { item: MenuItem; quantity: number; notes?: string } }>({});
  
  // Customer details
  const [customerName, setCustomerName] = useState("");
  const [customerPhone, setCustomerPhone] = useState("");
  const [waivePackaging, setWaivePackaging] = useState(false);
  const [waiverReason, setWaiverReason] = useState("");
  const [generalNotes, setGeneralNotes] = useState("");

  // Channel, Scheduled & Car-Side & Marketplace Delivery state
  const [orderChannel, setOrderChannel] = useState<"ParcelTakeaway" | "CarParking" | "Delivery">("ParcelTakeaway");
  const [deliveryProvider, setDeliveryProvider] = useState<"Swiggy" | "Zomato" | "Other Delivery">("Swiggy");
  const [customProviderName, setCustomProviderName] = useState("");
  const [externalOrderReference, setExternalOrderReference] = useState("");
  const [marketplaceCollected, setMarketplaceCollected] = useState(true);
  const [marketplaceEnteredTotal, setMarketplaceEnteredTotal] = useState("");
  const [marketplaceEnteredTax, setMarketplaceEnteredTax] = useState("");
  const [marketplaceEnteredDiscount, setMarketplaceEnteredDiscount] = useState("");
  const [marketplaceEnteredPackaging, setMarketplaceEnteredPackaging] = useState("");

  // Payouts state
  const [payouts, setPayouts] = useState<any[]>([]);
  const [loadingPayouts, setLoadingPayouts] = useState(false);
  const [payoutModalOpen, setPayoutModalOpen] = useState(false);
  const [payoutProvider, setPayoutProvider] = useState<"Swiggy" | "Zomato" | "Other Delivery">("Swiggy");
  const [payoutRef, setPayoutRef] = useState("");
  const [payoutGross, setPayoutGross] = useState("");
  const [payoutCommission, setPayoutCommission] = useState("");
  const [payoutTcs, setPayoutTcs] = useState("");
  const [payoutAdjustments, setPayoutAdjustments] = useState("");
  const [payoutNet, setPayoutNet] = useState("");
  const [payoutNotes, setPayoutNotes] = useState("");
  const [savingPayout, setSavingPayout] = useState(false);

  const [isScheduled, setIsScheduled] = useState(false);
  const [scheduledPickupTimeLocal, setScheduledPickupTimeLocal] = useState("");
  const [preparationLeadMinutes, setPreparationLeadMinutes] = useState(15);
  const [orderSource, setOrderSource] = useState<"Counter" | "Phone" | "WhatsApp">("Counter");
  const [vehiclePlate, setVehiclePlate] = useState("");
  const [vehicleModel, setVehicleModel] = useState("");
  const [parkingBay, setParkingBay] = useState("");

  // Reschedule dialog state
  const [rescheduleOrderModal, setRescheduleOrderModal] = useState<ParcelOrderResponseDto | null>(null);
  const [newPickupTimeLocal, setNewPickupTimeLocal] = useState("");
  const [newLeadMinutes, setNewLeadMinutes] = useState(15);
  const [rescheduling, setRescheduling] = useState(false);

  // Dynamic UPI QR state
  const [upiRequest, setUpiQrData] = useState<UpiQrResponseDto | null>(null);
  const [fetchingUpiQr, setFetchingUpiQr] = useState(false);

  // Filter in Orders tab
  const [statusFilter, setStatusFilter] = useState<"all" | "active" | "scheduled" | "car" | "delivery" | "settled">("all");

  // Packaging settings
  const [settings, setSettings] = useState<ParcelSettingsDto | null>(null);

  // Orders history
  const [orders, setOrders] = useState<ParcelOrderResponseDto[]>([]);
  const [historySearch, setHistorySearch] = useState("");
  const [loadingOrders, setLoadingOrders] = useState(false);

  // Modals & States
  const [submitting, setSubmitting] = useState(false);
  const [createdOrder, setCreatedOrder] = useState<ParcelOrderResponseDto | null>(null);
  const [paymentModalOrder, setPaymentModalOrder] = useState<ParcelOrderResponseDto | null>(null);
  const [loadingPaymentOrder, setLoadingPaymentOrder] = useState(false);
  const [paymentMethod, setPaymentMethod] = useState<"Cash" | "UPI" | "Card" | "MarketplaceCollected" | "Marketplace">("Cash");
  const [paymentAmount, setPaymentAmount] = useState<string>("");
  const [paymentOpId, setPaymentOpId] = useState<string>("");
  const [recordingPayment, setRecordingPayment] = useState(false);
  const [paymentError, setPaymentError] = useState<string | null>(null);
  const [paymentSuccess, setPaymentSuccess] = useState<string | null>(null);
  const [settledReceiptText, setSettledReceiptText] = useState<string | null>(null);
  const [printingReceipt, setPrintingReceipt] = useState(false);
  const [reprintModalJob, setReprintModalJob] = useState<ParcelOrderResponseDto | null>(null);
  const [reprintReason, setReprintReason] = useState("");
  const [reprinting, setReprinting] = useState(false);
  const [savedPaymentOp, setSavedPaymentOp] = useState<SavedParcelPaymentOperation | null>(null);

  // Settings form
  const [editChargeMode, setEditChargeMode] = useState<"None" | "PerItem" | "FlatPerOrder" | "Percentage">("None");
  const [editFlatRate, setEditFlatRate] = useState<number>(0);
  const [editPercentage, setEditPercentage] = useState<number>(0);
  const [editIsTaxable, setEditIsTaxable] = useState<boolean>(false);
  const [editTaxRate, setEditTaxRate] = useState<number>(5);
  const [savingSettings, setSavingSettings] = useState(false);

  useEffect(() => {
    loadData();
  }, [restaurantId]);

  const loadData = async () => {
    try {
      const [menuRes, setts, ords] = await Promise.all([
        getAdminMenu(restaurantId),
        parcelApi.getSettings(restaurantId).catch(() => null),
        parcelApi.searchOrders(restaurantId).catch(() => []),
      ]);
      setMenuItems((menuRes?.flatMenu || []).filter((i: MenuItem) => i.available));
      if (setts) {
        setSettings(setts);
        setEditChargeMode(setts.chargeMode);
        setEditFlatRate(setts.flatRate);
        setEditPercentage(setts.percentage);
        setEditIsTaxable(setts.isTaxable);
        setEditTaxRate(setts.taxRate ?? 5);
      }
      setOrders(ords);
    } catch (err) {
      console.error("Failed to load parcel data:", err);
    }
  };

  const categories = Array.from(new Set(menuItems.map((m) => m.categoryName || "General")));

  const filteredItems = menuItems.filter((item) => {
    const matchesCategory = selectedCategory === "all" || item.categoryName === selectedCategory;
    const matchesSearch = item.name.toLowerCase().includes(searchQuery.toLowerCase());
    return matchesCategory && matchesSearch;
  });

  const updateQuantity = (item: MenuItem, delta: number) => {
    setCart((prev) => {
      const current = prev[item.id]?.quantity || 0;
      const next = current + delta;
      if (next <= 0) {
        const copy = { ...prev };
        delete copy[item.id];
        return copy;
      }
      return {
        ...prev,
        [item.id]: { item, quantity: next, notes: prev[item.id]?.notes || "" },
      };
    });
  };

  const setItemNotes = (itemId: string, notes: string) => {
    setCart((prev) => {
      if (!prev[itemId]) return prev;
      return {
        ...prev,
        [itemId]: { ...prev[itemId], notes },
      };
    });
  };

  // Calculations
  const cartItems = Object.values(cart);
  const foodSubtotal = cartItems.reduce((acc, curr) => acc + curr.item.price * curr.quantity, 0);

  let packagingCharge = 0;
  if (!waivePackaging && settings) {
    if (settings.chargeMode === "FlatPerOrder") {
      packagingCharge = settings.flatRate;
    } else if (settings.chargeMode === "Percentage") {
      packagingCharge = Math.round(foodSubtotal * (settings.percentage / 100) * 100) / 100;
    } else if (settings.chargeMode === "PerItem") {
      packagingCharge = cartItems.reduce((acc, curr) => acc + ((curr.item as any).packagingFee || 0) * curr.quantity, 0);
    }
  }

  const foodTax = Math.round(foodSubtotal * 0.05 * 100) / 100;
  let packagingTax = 0;
  if (!waivePackaging && packagingCharge > 0 && settings?.isTaxable) {
    packagingTax = Math.round(packagingCharge * ((settings.taxRate ?? 5) / 100) * 100) / 100;
  }
  const totalTax = foodTax + packagingTax;
  const grandTotal = Math.round((foodSubtotal + packagingCharge + totalTax) * 100) / 100;

  const handleCreateOrder = async () => {
    if (cartItems.length === 0) return;
    const isDelivery = orderChannel === "Delivery";
    if (isDelivery && !externalOrderReference.trim()) {
      alert("External order reference is required for marketplace delivery orders.");
      return;
    }
    if (isDelivery && deliveryProvider === "Other Delivery" && !customProviderName.trim()) {
      alert("Please specify the provider name for Other Delivery.");
      return;
    }

    setSubmitting(true);
    try {
      const opId = crypto.randomUUID();
      const finalProvider = isDelivery
        ? (deliveryProvider === "Other Delivery" ? (customProviderName.trim() || "Other") : deliveryProvider)
        : undefined;

      const res = await parcelApi.createOrder(restaurantId, {
        operationId: opId,
        customerName: customerName || undefined,
        customerPhone: customerPhone || undefined,
        orderChannel,
        deliveryProvider: finalProvider,
        externalOrderReference: isDelivery ? externalOrderReference.trim() : undefined,
        marketplaceCollected: isDelivery ? marketplaceCollected : false,
        marketplaceEnteredTotal: isDelivery && marketplaceEnteredTotal ? parseFloat(marketplaceEnteredTotal) : undefined,
        marketplaceEnteredTax: isDelivery && marketplaceEnteredTax ? parseFloat(marketplaceEnteredTax) : undefined,
        marketplaceEnteredDiscount: isDelivery && marketplaceEnteredDiscount ? parseFloat(marketplaceEnteredDiscount) : undefined,
        marketplaceEnteredPackaging: isDelivery && marketplaceEnteredPackaging ? parseFloat(marketplaceEnteredPackaging) : undefined,
        orderSource: isDelivery ? "MarketplaceApp" : (isScheduled ? orderSource : (orderChannel === "CarParking" ? "CarSide" : "Counter")),
        isScheduled,
        scheduledPickupTimeLocal: isScheduled ? scheduledPickupTimeLocal : undefined,
        preparationLeadMinutes: isScheduled ? preparationLeadMinutes : undefined,
        vehiclePlate: orderChannel === "CarParking" ? vehiclePlate : undefined,
        vehicleModel: orderChannel === "CarParking" ? vehicleModel : undefined,
        parkingBay: orderChannel === "CarParking" ? parkingBay : undefined,
        waivePackaging,
        packagingWaiverReason: waivePackaging ? waiverReason : undefined,
        items: cartItems.map((c) => ({
          menuItemId: c.item.id,
          quantity: c.quantity,
          specialInstructions: c.notes || undefined,
          packingIntent: "Takeaway",
        })),
        notes: generalNotes || undefined,
      });

      setCreatedOrder(res);
      setCart({});
      setCustomerName("");
      setCustomerPhone("");
      setVehiclePlate("");
      setVehicleModel("");
      setParkingBay("");
      setExternalOrderReference("");
      setMarketplaceEnteredTotal("");
      setMarketplaceEnteredTax("");
      setMarketplaceEnteredDiscount("");
      setMarketplaceEnteredPackaging("");
      setIsScheduled(false);
      setScheduledPickupTimeLocal("");
      setWaivePackaging(false);
      setWaiverReason("");
      setGeneralNotes("");
      loadOrders();
    } catch (err: any) {
      alert(err.message || "Failed to create parcel order.");
    } finally {
      setSubmitting(false);
    }
  };

  const loadOrders = async () => {
    setLoadingOrders(true);
    try {
      const ords = await parcelApi.searchOrders(restaurantId);
      setOrders(ords);
    } catch (err) {
      console.error(err);
    } finally {
      setLoadingOrders(false);
    }
  };

  const loadPayouts = async () => {
    setLoadingPayouts(true);
    try {
      const data = await parcelApi.getPayouts(restaurantId);
      setPayouts(data || []);
    } catch (e) {
      console.error("Failed to load payouts:", e);
    } finally {
      setLoadingPayouts(false);
    }
  };

  const handleRecordPayout = async () => {
    if (!payoutRef.trim()) {
      alert("Payout Reference is required.");
      return;
    }
    const gross = parseFloat(payoutGross) || 0;
    const comm = parseFloat(payoutCommission) || 0;
    const tcs = parseFloat(payoutTcs) || 0;
    const adj = parseFloat(payoutAdjustments) || 0;
    const calcNet = Math.round((gross - comm - tcs + adj) * 100) / 100;
    const net = payoutNet ? (parseFloat(payoutNet) || 0) : calcNet;

    setSavingPayout(true);
    try {
      await parcelApi.recordPayout(restaurantId, {
        deliveryProvider: payoutProvider,
        payoutReference: payoutRef.trim(),
        grossAmount: gross,
        commissionDeducted: comm,
        tcsGstDeducted: tcs,
        otherAdjustments: adj,
        netAmount: net,
        notes: payoutNotes.trim() || undefined,
      });
      alert("Marketplace payout recorded successfully.");
      setPayoutModalOpen(false);
      setPayoutRef("");
      setPayoutGross("");
      setPayoutCommission("");
      setPayoutTcs("");
      setPayoutAdjustments("");
      setPayoutNet("");
      setPayoutNotes("");
      loadPayouts();
    } catch (e: any) {
      alert(e.message || "Failed to record payout.");
    } finally {
      setSavingPayout(false);
    }
  };

  const handleReleaseScheduledOrder = async (orderId: string) => {
    try {
      await parcelApi.releaseScheduledOrderNow(restaurantId, orderId);
      loadOrders();
    } catch (err: any) {
      alert(err.message || "Failed to release scheduled order.");
    }
  };

  const handleDeliverCarOrder = async (orderId: string) => {
    try {
      await parcelApi.deliverCarOrder(restaurantId, orderId, { operationId: crypto.randomUUID() });
      loadOrders();
    } catch (err: any) {
      alert(err.message || "Failed to deliver car order.");
    }
  };

  const handleRescheduleOrder = async () => {
    if (!rescheduleOrderModal || !newPickupTimeLocal) return;
    setRescheduling(true);
    try {
      await parcelApi.rescheduleOrder(restaurantId, rescheduleOrderModal.orderId, {
        operationId: crypto.randomUUID(),
        newPickupTimeLocal,
        preparationLeadMinutes: newLeadMinutes,
      });
      setRescheduleOrderModal(null);
      loadOrders();
    } catch (err: any) {
      alert(err.message || "Failed to reschedule order.");
    } finally {
      setRescheduling(false);
    }
  };

  const handleFetchUpiQr = async (billId: string, requestedAmount?: number) => {
    setUpiQrData(null);
    setPaymentError(null);
    setFetchingUpiQr(true);
    try {
      const qr = await parcelApi.getBillUpiQr(restaurantId, billId, requestedAmount);
      setUpiQrData(qr);
    } catch (err: any) {
      setPaymentError(err.message || "Failed to generate dynamic UPI QR.");
    } finally {
      setFetchingUpiQr(false);
    }
  };

  const openPaymentModal = async (ord: ParcelOrderResponseDto) => {
    setPaymentModalOrder(ord);
    setPaymentError(null);
    setPaymentSuccess(null);
    setSettledReceiptText(null);

    // Check for durable unresolved payment in localStorage
    const saved = readSavedParcelPayment(effectiveEmployeeId, restaurantId, ord.orderId);
    setSavedPaymentOp(saved);
    if (saved) {
      setPaymentMethod(saved.method);
      setPaymentAmount(saved.amount.toFixed(2));
      setPaymentOpId(saved.operationId);
    } else {
      if (ord.deliveryProvider || ord.orderChannel === "Delivery" || ord.marketplaceCollected) {
        setPaymentMethod("MarketplaceCollected");
      } else {
        setPaymentMethod("Cash");
      }
      setPaymentOpId(crypto.randomUUID());
    }

    setLoadingPaymentOrder(true);
    try {
      const fresh = await parcelApi.getOrder(restaurantId, ord.orderId);
      setPaymentModalOrder(fresh);
      if (!saved) {
        const remaining = fresh.bill?.remainingAmount ?? (fresh.totalAmount - (fresh.bill?.paidAmount ?? 0));
        setPaymentAmount(remaining > 0 ? remaining.toFixed(2) : "0.00");
      }
    } catch (err: any) {
      console.warn("Could not fetch authoritative order state, using cached:", err);
      if (!saved) {
        const remaining = ord.bill?.remainingAmount ?? (ord.totalAmount - (ord.bill?.paidAmount ?? 0));
        setPaymentAmount(remaining > 0 ? remaining.toFixed(2) : "0.00");
      }
    } finally {
      setLoadingPaymentOrder(false);
    }
  };

  const handleRefreshPaymentOrder = async () => {
    if (!paymentModalOrder) return;
    setUpiQrData(null);
    setLoadingPaymentOrder(true);
    setPaymentError(null);
    const saved = readSavedParcelPayment(effectiveEmployeeId, restaurantId, paymentModalOrder.orderId);
    setSavedPaymentOp(saved);
    try {
      const fresh = await parcelApi.getOrder(restaurantId, paymentModalOrder.orderId);
      setPaymentModalOrder(fresh);
      if (!saved) {
        const remaining = fresh.bill?.remainingAmount ?? (fresh.totalAmount - (fresh.bill?.paidAmount ?? 0));
        setPaymentAmount(remaining > 0 ? remaining.toFixed(2) : "0.00");
      }
    } catch (err: any) {
      setPaymentError(err.message || "Failed to refresh order status.");
    } finally {
      setLoadingPaymentOrder(false);
    }
  };

  const handleRecordPayment = async () => {
    if (!paymentModalOrder) return;
    if (recordingPayment) return; // double-click prevention

    const storageKey = getParcelPaymentStorageKey(effectiveEmployeeId, restaurantId, paymentModalOrder.orderId);
    const saved = readSavedParcelPayment(effectiveEmployeeId, restaurantId, paymentModalOrder.orderId);

    let opToExecute: SavedParcelPaymentOperation;

    if (saved) {
      opToExecute = saved;
    } else {
      const amountNum = parseFloat(paymentAmount);
      if (isNaN(amountNum) || amountNum <= 0) {
        setPaymentError("Please enter a valid positive payment amount.");
        return;
      }

      const remaining = paymentModalOrder.bill?.remainingAmount ?? (paymentModalOrder.totalAmount - (paymentModalOrder.bill?.paidAmount ?? 0));
      if (amountNum > remaining + 0.001) {
        setPaymentError(`Payment amount ₹${amountNum.toFixed(2)} exceeds remaining balance of ₹${remaining.toFixed(2)}.`);
        return;
      }

      opToExecute = {
        restaurantId,
        orderId: paymentModalOrder.orderId,
        billId: paymentModalOrder.billId,
        actorEmployeeId: effectiveEmployeeId,
        operationId: paymentOpId || crypto.randomUUID(),
        method: paymentMethod,
        amount: Math.round(amountNum * 100) / 100,
        expectedRevision: paymentModalOrder.bill?.revision,
        expectedRemaining: remaining,
        createdAt: Date.now(),
      };

      // Persist BEFORE network dispatch: an unavailable store must NEVER proceed
      try {
        localStorage.setItem(storageKey, JSON.stringify(opToExecute));
      } catch {
        setPaymentError("Browser storage is unavailable. Enable storage before submitting this payment.");
        return;
      }
      setSavedPaymentOp(opToExecute);
    }

    setRecordingPayment(true);
    setPaymentError(null);
    setPaymentSuccess(null);

    try {
      const ack = await parcelApi.recordPayment(restaurantId, paymentModalOrder.orderId, {
        operationId: opToExecute.operationId,
        method: opToExecute.method,
        amount: opToExecute.amount,
        expectedRevision: opToExecute.expectedRevision,
        expectedRemaining: opToExecute.expectedRemaining,
      });

      // Definitive success: clear local storage
      localStorage.removeItem(storageKey);
      setSavedPaymentOp(null);

      // Refresh authoritative order state
      const refreshed = await parcelApi.getOrder(restaurantId, paymentModalOrder.orderId);
      setPaymentModalOrder(refreshed);
      const newRemaining = refreshed.bill?.remainingAmount ?? (refreshed.totalAmount - (refreshed.bill?.paidAmount ?? 0));
      setPaymentAmount(newRemaining > 0 ? newRemaining.toFixed(2) : "0.00");
      setPaymentOpId(crypto.randomUUID());
      setPaymentSuccess(`Recorded ${ack.method} payment of ₹${ack.amount.toFixed(2)}. Remaining balance: ₹${ack.remaining.toFixed(2)}.`);
      loadOrders();

      if (newRemaining <= 0.001) {
        // Automatically attempt receipt print on final settlement
        try {
          const receiptRes = await parcelApi.printReceipt(restaurantId, paymentModalOrder.orderId, "Settled takeaway bill");
          setSettledReceiptText(receiptRes.readableText);
        } catch (e: any) {
          console.warn("Auto receipt print notification:", e);
        }
      }
    } catch (err: any) {
      const status = err instanceof ApiError ? err.status : err?.status;
      if (status && [400, 404, 409].includes(status)) {
        // Definitive client/server refusal: clear saved operation so operator can correct details
        localStorage.removeItem(storageKey);
        setSavedPaymentOp(null);
      }
      // For transient network drops or server errors (500, 401, 403, disconnected):
      // The saved operation is preserved in localStorage for replay!
      setPaymentError(err.message || "Payment state is uncertain. Replay to verify or complete.");
    } finally {
      setRecordingPayment(false);
    }
  };

  const handlePrintSettledReceipt = async () => {
    if (!paymentModalOrder) return;
    setPrintingReceipt(true);
    try {
      const res = await parcelApi.printReceipt(restaurantId, paymentModalOrder.orderId, "Takeaway receipt");
      setSettledReceiptText(res.readableText);
      alert("Receipt print queued to counter printer.");
    } catch (err: any) {
      alert(err.message || "Failed to print receipt.");
    } finally {
      setPrintingReceipt(false);
    }
  };

  const handleReprintReceipt = async () => {
    if (!reprintModalJob) return;
    setReprinting(true);
    try {
      await parcelApi.printReceipt(
        restaurantId,
        reprintModalJob.orderId,
        reprintReason || "Customer reprint"
      );
      alert("Receipt print queued to counter printer.");
      setReprintModalJob(null);
      setReprintReason("");
    } catch (err: any) {
      alert(err.message || "Print request failed.");
    } finally {
      setReprinting(false);
    }
  };

  const handleRetryKot = async (orderId: string) => {
    try {
      const res = await parcelApi.retryKot(restaurantId, orderId);
      alert(res.message || "KOT retry queued.");
      loadOrders();
    } catch (err: any) {
      alert(err.message || "KOT retry failed.");
    }
  };

  const handleSaveSettings = async () => {
    setSavingSettings(true);
    try {
      const updated = await parcelApi.updateSettings(restaurantId, {
        chargeMode: editChargeMode,
        flatRate: editFlatRate,
        percentage: editPercentage,
        isTaxable: editIsTaxable,
        taxRate: editTaxRate,
      });
      setSettings(updated);
      alert("Packaging charge settings saved.");
    } catch (err: any) {
      alert(err.message || "Failed to update packaging settings.");
    } finally {
      setSavingSettings(false);
    }
  };

  return (
    <div className="space-y-6">
      {/* Top Header & Navigation */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 border-b pb-4">
        <div>
          <div className="flex items-center gap-2">
            <Package className="h-6 w-6 text-primary" />
            <h1 className="text-2xl font-bold tracking-tight">Parcel Counter & Takeaway</h1>
            <Badge variant="outline" className="ml-2 font-mono text-xs">
              IST Date: {new Date().toLocaleDateString("en-IN", { timeZone: "Asia/Kolkata" })}
            </Badge>
          </div>
          <p className="text-sm text-muted-foreground mt-1">
            Fast takeaway counter ordering with daily tokens, automatic packaging charges, and multi-station KOTs.
          </p>
        </div>

        <div className="flex gap-2">
          <Button
            variant={activeTab === "new" ? "default" : "outline"}
            size="sm"
            onClick={() => setActiveTab("new")}
          >
            <Plus className="h-4 w-4 mr-1.5" />
            New Parcel Order
          </Button>
          <Button
            variant={activeTab === "orders" ? "default" : "outline"}
            size="sm"
            onClick={() => {
              setActiveTab("orders");
              loadOrders();
            }}
          >
            <Receipt className="h-4 w-4 mr-1.5" />
            Today's Tokens ({orders.length})
          </Button>
          <Button
            variant={activeTab === "payouts" ? "default" : "outline"}
            size="sm"
            onClick={() => {
              setActiveTab("payouts");
              loadPayouts();
            }}
          >
            <IndianRupee className="h-4 w-4 mr-1.5" />
            Marketplace Payouts
          </Button>
          <Button
            variant={activeTab === "settings" ? "default" : "outline"}
            size="sm"
            onClick={() => setActiveTab("settings")}
          >
            <Settings className="h-4 w-4 mr-1.5" />
            Packaging Rules
          </Button>
          {onClose && (
            <Button
              variant="ghost"
              size="sm"
              onClick={onClose}
              className="ml-2 text-muted-foreground hover:text-foreground"
            >
              <X className="h-4 w-4 mr-1" />
              Close
            </Button>
          )}
        </div>
      </div>

      <ParcelRecovery restaurantId={restaurantId} employeeId={effectiveEmployeeId} onResolved={loadOrders} />

      {/* TAB 1: NEW PARCEL ORDER */}
      {activeTab === "new" && (
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
          {/* Menu Catalog (8 Cols) */}
          <div className="lg:col-span-7 xl:col-span-8 space-y-4">
            {/* Search and Category Filter */}
            <div className="flex flex-col sm:flex-row gap-3">
              <div className="relative flex-1">
                <Search className="absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" />
                <Input
                  placeholder="Search dishes or drinks..."
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  className="pl-9"
                />
              </div>
              <div className="flex gap-1 overflow-x-auto pb-1 max-w-full">
                <Button
                  variant={selectedCategory === "all" ? "default" : "outline"}
                  size="sm"
                  onClick={() => setSelectedCategory("all")}
                >
                  All
                </Button>
                {categories.map((cat) => (
                  <Button
                    key={cat}
                    variant={selectedCategory === cat ? "default" : "outline"}
                    size="sm"
                    onClick={() => setSelectedCategory(cat)}
                  >
                    {cat}
                  </Button>
                ))}
              </div>
            </div>

            {/* Menu Items Grid */}
            <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 gap-3">
              {filteredItems.map((item) => {
                const qty = cart[item.id]?.quantity || 0;
                return (
                  <Card key={item.id} className="relative overflow-hidden hover:border-primary/50 transition">
                    <CardHeader className="p-3.5 pb-2">
                      <div className="flex justify-between items-start gap-2">
                        <CardTitle className="text-sm font-semibold line-clamp-1">{item.name}</CardTitle>
                        <span className="font-bold text-sm">₹{item.price.toFixed(2)}</span>
                      </div>
                      <CardDescription className="text-xs line-clamp-1">
                        {item.categoryName || "General"}
                      </CardDescription>
                    </CardHeader>
                    <CardContent className="p-3.5 pt-0 flex justify-between items-center">
                      <span className="text-xs text-muted-foreground">
                        {(item as any).kitchenStationName ? `Station: ${(item as any).kitchenStationName}` : "Auto Station"}
                      </span>
                      {qty === 0 ? (
                        <Button size="sm" variant="outline" className="h-8 px-3" onClick={() => updateQuantity(item, 1)}>
                          <Plus className="h-3.5 w-3.5 mr-1" /> Add
                        </Button>
                      ) : (
                        <div className="flex items-center gap-1.5">
                          <Button size="icon" variant="outline" className="h-7 w-7" onClick={() => updateQuantity(item, -1)}>
                            <Minus className="h-3.5 w-3.5" />
                          </Button>
                          <span className="font-bold text-sm w-5 text-center">{qty}</span>
                          <Button size="icon" variant="outline" className="h-7 w-7" onClick={() => updateQuantity(item, 1)}>
                            <Plus className="h-3.5 w-3.5" />
                          </Button>
                        </div>
                      )}
                    </CardContent>
                  </Card>
                );
              })}
            </div>
          </div>

          {/* Cart & Billing Sidebar (4-5 Cols) */}
          <div className="lg:col-span-5 xl:col-span-4">
            <Card className="sticky top-6 border-2 border-primary/20 shadow-md">
              <CardHeader className="bg-muted/40 pb-3">
                <div className="flex justify-between items-center">
                  <CardTitle className="text-base font-bold flex items-center gap-2">
                    <Receipt className="h-5 w-5 text-primary" />
                    Parcel Order Summary
                  </CardTitle>
                  <Badge variant="secondary" className="font-mono text-xs">
                    Takeaway
                  </Badge>
                </div>
              </CardHeader>

              <CardContent className="p-4 space-y-4">
                {/* Order Channel Selector */}
                <div className="flex rounded-lg bg-muted p-1 text-xs">
                  <button
                    type="button"
                    className={`flex-1 py-1.5 font-semibold rounded-md transition ${orderChannel === "ParcelTakeaway" ? "bg-background shadow text-foreground" : "text-muted-foreground hover:text-foreground"}`}
                    onClick={() => setOrderChannel("ParcelTakeaway")}
                  >
                    Takeaway Counter
                  </button>
                  <button
                    type="button"
                    className={`flex-1 py-1.5 font-semibold rounded-md transition ${orderChannel === "CarParking" ? "bg-background shadow text-foreground" : "text-muted-foreground hover:text-foreground"}`}
                    onClick={() => setOrderChannel("CarParking")}
                  >
                    🚗 Car-Side Delivery
                  </button>
                  <button
                    type="button"
                    className={`flex-1 py-1.5 font-semibold rounded-md transition ${orderChannel === "Delivery" ? "bg-background shadow text-foreground" : "text-muted-foreground hover:text-foreground"}`}
                    onClick={() => setOrderChannel("Delivery")}
                  >
                    🛵 Swiggy / Zomato
                  </button>
                </div>

                {/* Marketplace Delivery Specific Inputs */}
                {orderChannel === "Delivery" && (
                  <div className="p-3 rounded-lg border border-orange-200 bg-orange-50/40 dark:bg-orange-950/20 space-y-2.5 text-xs">
                    <div className="font-bold text-orange-900 dark:text-orange-300 flex items-center justify-between">
                      <span className="flex items-center gap-1.5">🛵 Marketplace Delivery Order</span>
                      <Badge variant="outline" className="border-orange-300 text-orange-700 bg-orange-100/60 text-[10px]">
                        Tableless KOT Routing
                      </Badge>
                    </div>

                    <div className="grid grid-cols-3 gap-2">
                      <div>
                        <label className="font-medium text-muted-foreground block mb-0.5">Provider *</label>
                        <select
                          value={deliveryProvider}
                          onChange={(e) => setDeliveryProvider(e.target.value as any)}
                          className="w-full h-8 border rounded-md px-2 bg-background text-xs font-semibold"
                        >
                          <option value="Swiggy">Swiggy</option>
                          <option value="Zomato">Zomato</option>
                          <option value="Other Delivery">Other Delivery</option>
                        </select>
                      </div>

                      {deliveryProvider === "Other Delivery" ? (
                        <div>
                          <label className="font-medium text-muted-foreground block mb-0.5">Provider Name *</label>
                          <Input
                            placeholder="e.g. Dunzo / Shadowfax"
                            value={customProviderName}
                            onChange={(e) => setCustomProviderName(e.target.value)}
                            className="h-8 text-xs"
                          />
                        </div>
                      ) : null}

                      <div className={deliveryProvider === "Other Delivery" ? "col-span-1" : "col-span-2"}>
                        <label className="font-medium text-muted-foreground block mb-0.5">External Order Ref / ID *</label>
                        <Input
                          placeholder="e.g. SWIGGY-8921 / 449201"
                          value={externalOrderReference}
                          onChange={(e) => setExternalOrderReference(e.target.value)}
                          className="h-8 text-xs font-mono font-bold"
                        />
                      </div>
                    </div>

                    <div className="pt-2 border-t border-orange-200/60 dark:border-orange-900/40 space-y-2">
                      <label className="flex items-center gap-2 cursor-pointer font-semibold text-orange-950 dark:text-orange-200">
                        <input
                          type="checkbox"
                          checked={marketplaceCollected}
                          onChange={(e) => setMarketplaceCollected(e.target.checked)}
                          className="rounded text-orange-600 focus:ring-orange-500"
                        />
                        <span>Marketplace Collected Payment (Prepaid Order)</span>
                      </label>

                      <div className="grid grid-cols-2 sm:grid-cols-4 gap-2 pt-1">
                        <div>
                          <label className="text-[10px] text-muted-foreground block mb-0.5">Entered Total (₹)</label>
                          <Input
                            type="number"
                            step="0.01"
                            placeholder={grandTotal > 0 ? grandTotal.toFixed(2) : "0.00"}
                            value={marketplaceEnteredTotal}
                            onChange={(e) => setMarketplaceEnteredTotal(e.target.value)}
                            className="h-7 text-xs font-mono"
                          />
                        </div>
                        <div>
                          <label className="text-[10px] text-muted-foreground block mb-0.5">Entered Tax (₹)</label>
                          <Input
                            type="number"
                            step="0.01"
                            placeholder={totalTax > 0 ? totalTax.toFixed(2) : "0.00"}
                            value={marketplaceEnteredTax}
                            onChange={(e) => setMarketplaceEnteredTax(e.target.value)}
                            className="h-7 text-xs font-mono"
                          />
                        </div>
                        <div>
                          <label className="text-[10px] text-muted-foreground block mb-0.5">Entered Packaging (₹)</label>
                          <Input
                            type="number"
                            step="0.01"
                            placeholder={packagingCharge > 0 ? packagingCharge.toFixed(2) : "0.00"}
                            value={marketplaceEnteredPackaging}
                            onChange={(e) => setMarketplaceEnteredPackaging(e.target.value)}
                            className="h-7 text-xs font-mono"
                          />
                        </div>
                        <div>
                          <label className="text-[10px] text-muted-foreground block mb-0.5">Entered Discount (₹)</label>
                          <Input
                            type="number"
                            step="0.01"
                            placeholder="0.00"
                            value={marketplaceEnteredDiscount}
                            onChange={(e) => setMarketplaceEnteredDiscount(e.target.value)}
                            className="h-7 text-xs font-mono"
                          />
                        </div>
                      </div>

                      {marketplaceEnteredTotal && Number(marketplaceEnteredTotal) > 0 && grandTotal > 0 && (
                        <div className="flex items-center justify-between text-[11px] p-1.5 rounded bg-orange-100/60 dark:bg-orange-950/40">
                          <span>TABLO Total: <strong>₹{grandTotal.toFixed(2)}</strong></span>
                          <span>Partner Total: <strong>₹{Number(marketplaceEnteredTotal).toFixed(2)}</strong></span>
                          {Math.abs(grandTotal - Number(marketplaceEnteredTotal)) > 0.01 ? (
                            <span className="font-semibold text-amber-700 dark:text-amber-400">
                              Diff: ₹{Math.abs(grandTotal - Number(marketplaceEnteredTotal)).toFixed(2)}
                            </span>
                          ) : (
                            <span className="font-semibold text-emerald-700 dark:text-emerald-400">
                              ✓ Matched
                            </span>
                          )}
                        </div>
                      )}
                    </div>
                  </div>
                )}

                {/* Car-Side Specific Inputs */}
                {orderChannel === "CarParking" && (
                  <div className="p-2.5 rounded-lg border border-purple-200 bg-purple-50/40 dark:bg-purple-950/20 space-y-2 text-xs">
                    <div className="font-bold text-purple-900 dark:text-purple-300 flex items-center gap-1">
                      🚗 Car Delivery Details
                    </div>
                    <div className="grid grid-cols-2 gap-2">
                      <div>
                        <label className="font-medium text-muted-foreground block mb-0.5">Vehicle Plate *</label>
                        <Input
                          placeholder="e.g. MH12AB1234"
                          value={vehiclePlate}
                          onChange={(e) => setVehiclePlate(e.target.value.toUpperCase())}
                          className="h-8 text-xs font-mono font-bold"
                        />
                      </div>
                      <div>
                        <label className="font-medium text-muted-foreground block mb-0.5">Parking Bay</label>
                        <Input
                          placeholder="e.g. Bay 4 / Pillar C"
                          value={parkingBay}
                          onChange={(e) => setParkingBay(e.target.value)}
                          className="h-8 text-xs"
                        />
                      </div>
                    </div>
                    <div>
                      <label className="font-medium text-muted-foreground block mb-0.5">Car Model / Color</label>
                      <Input
                        placeholder="e.g. White Swift / Black Creta"
                        value={vehicleModel}
                        onChange={(e) => setVehicleModel(e.target.value)}
                        className="h-8 text-xs"
                      />
                    </div>
                  </div>
                )}

                {/* Scheduled Advance Pickup Toggle */}
                <div className="p-2.5 rounded-lg border space-y-2 text-xs">
                  <label className="flex items-center gap-2 cursor-pointer font-semibold">
                    <input
                      type="checkbox"
                      checked={isScheduled}
                      onChange={(e) => setIsScheduled(e.target.checked)}
                      className="rounded"
                    />
                    <span>⏱️ Advance Scheduled Pickup (Phone / WhatsApp)</span>
                  </label>

                  {isScheduled && (
                    <div className="space-y-2 pt-1 border-t">
                      <div className="grid grid-cols-2 gap-2">
                        <div>
                          <label className="font-medium text-muted-foreground block mb-0.5">Order Source</label>
                          <select
                            value={orderSource}
                            onChange={(e) => setOrderSource(e.target.value as any)}
                            className="w-full h-8 border rounded-md px-2 bg-background text-xs"
                          >
                            <option value="Phone">Phone Call</option>
                            <option value="WhatsApp">WhatsApp</option>
                            <option value="Counter">Counter Desk</option>
                          </select>
                        </div>
                        <div>
                          <label className="font-medium text-muted-foreground block mb-0.5">Kitchen Lead (Mins)</label>
                          <Input
                            type="number"
                            min="5"
                            max="120"
                            value={preparationLeadMinutes}
                            onChange={(e) => setPreparationLeadMinutes(parseInt(e.target.value) || 15)}
                            className="h-8 text-xs"
                          />
                        </div>
                      </div>
                      <div>
                        <label className="font-medium text-muted-foreground block mb-0.5">
                          Scheduled Pickup Time (Asia/Kolkata) *
                        </label>
                        <Input
                          type="datetime-local"
                          value={scheduledPickupTimeLocal}
                          onChange={(e) => setScheduledPickupTimeLocal(e.target.value)}
                          className="h-8 text-xs"
                        />
                        <span className="text-[10px] text-muted-foreground">
                          Kitchen KOT will automatically fire {preparationLeadMinutes} mins before pickup.
                        </span>
                      </div>
                    </div>
                  )}
                </div>

                {/* Customer Details */}
                <div className="grid grid-cols-2 gap-2 text-xs">
                  <div>
                    <label className="font-medium text-muted-foreground flex items-center gap-1 mb-1">
                      <User className="h-3.5 w-3.5" /> Customer Name
                    </label>
                    <Input
                      placeholder="Optional"
                      value={customerName}
                      onChange={(e) => setCustomerName(e.target.value)}
                      className="h-8 text-xs"
                    />
                  </div>
                  <div>
                    <label className="font-medium text-muted-foreground flex items-center gap-1 mb-1">
                      <Phone className="h-3.5 w-3.5" /> Phone Number
                    </label>
                    <Input
                      placeholder="Optional"
                      value={customerPhone}
                      onChange={(e) => setCustomerPhone(e.target.value)}
                      className="h-8 text-xs"
                    />
                  </div>
                </div>

                {/* Items in Cart */}
                <div className="border rounded-md divide-y max-h-56 overflow-y-auto">
                  {cartItems.length === 0 ? (
                    <div className="p-6 text-center text-xs text-muted-foreground">
                      No items selected. Click + on the menu to add takeaway dishes.
                    </div>
                  ) : (
                    cartItems.map(({ item, quantity, notes }) => (
                      <div key={item.id} className="p-2.5 text-xs flex flex-col gap-1.5">
                        <div className="flex justify-between items-center">
                          <div className="flex-1 font-medium">
                            <span className="font-bold text-primary mr-1.5">{quantity}x</span>
                            {item.name}
                          </div>
                          <span className="font-semibold">₹{(item.price * quantity).toFixed(2)}</span>
                        </div>
                        <Input
                          placeholder="Special cooking instructions..."
                          value={notes || ""}
                          onChange={(e) => setItemNotes(item.id, e.target.value)}
                          className="h-6 text-[11px] bg-background/50"
                        />
                      </div>
                    ))
                  )}
                </div>

                {/* Packaging Charge Section */}
                <div className="bg-secondary/40 p-3 rounded-lg space-y-2 text-xs">
                  <div className="flex justify-between items-center font-medium">
                    <span>
                      Packaging ({settings?.chargeMode || "None"})
                      {!waivePackaging && settings?.chargeMode === "Percentage" && ` [${settings.percentage}%]`}
                      {!waivePackaging && settings?.chargeMode === "FlatPerOrder" && ` [₹${settings.flatRate}]`}
                    </span>
                    <span className={waivePackaging ? "line-through text-muted-foreground" : "font-bold"}>
                      ₹{packagingCharge.toFixed(2)}
                    </span>
                  </div>

                  <label className="flex items-center gap-2 cursor-pointer font-normal text-muted-foreground pt-1">
                    <input
                      type="checkbox"
                      checked={waivePackaging}
                      onChange={(e) => setWaivePackaging(e.target.checked)}
                      className="rounded border-gray-300"
                    />
                    <span>Waive packaging charge (Staff authorization)</span>
                  </label>

                  {waivePackaging && (
                    <Input
                      placeholder="Enter waiver reason (e.g. Regular guest / BYO container)"
                      value={waiverReason}
                      onChange={(e) => setWaiverReason(e.target.value)}
                      className="h-7 text-xs bg-background"
                    />
                  )}
                </div>

                {/* Totals Breakdown */}
                <div className="space-y-1.5 text-xs pt-1 border-t">
                  <div className="flex justify-between">
                    <span className="text-muted-foreground">Food Subtotal</span>
                    <span>₹{foodSubtotal.toFixed(2)}</span>
                  </div>
                  {packagingCharge > 0 && (
                    <div className="flex justify-between">
                      <span className="text-muted-foreground">Packaging Fee</span>
                      <span>₹{packagingCharge.toFixed(2)}</span>
                    </div>
                  )}
                  <div className="flex justify-between">
                    <span className="text-muted-foreground">GST & Taxes</span>
                    <span>₹{totalTax.toFixed(2)}</span>
                  </div>
                  <div className="flex justify-between font-bold text-sm pt-2 border-t text-primary">
                    <span>Grand Total</span>
                    <span>₹{grandTotal.toFixed(2)}</span>
                  </div>
                </div>

                {/* Action Buttons */}
                <div className="space-y-2 pt-2">
                  <Button
                    className="w-full font-bold"
                    size="lg"
                    disabled={cartItems.length === 0 || submitting}
                    onClick={handleCreateOrder}
                  >
                    {submitting ? (
                      <RefreshCw className="h-4 w-4 animate-spin mr-2" />
                    ) : (
                      <Printer className="h-4 w-4 mr-2" />
                    )}
                    Generate Token & Print KOT
                  </Button>
                </div>
              </CardContent>
            </Card>
          </div>
        </div>
      )}

      {/* TAB 2: TODAY'S PARCEL TOKENS */}
      {activeTab === "orders" && (
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-3">
            <div>
              <CardTitle className="text-base font-bold">Today's Takeaway Tokens</CardTitle>
              <CardDescription className="text-xs">
                Tokens reset sequentially each business date. Station KOTs and Counter receipts remain auditable.
              </CardDescription>
            </div>
            <Button size="sm" variant="outline" onClick={loadOrders} disabled={loadingOrders}>
              <RefreshCw className={`h-3.5 w-3.5 mr-1.5 ${loadingOrders ? "animate-spin" : ""}`} />
              Refresh
            </Button>
          </CardHeader>
          <CardContent className="space-y-3">
            {/* Filter Tabs */}
            <div className="flex gap-1.5 text-xs">
              {(["all", "active", "scheduled", "car", "delivery", "settled"] as const).map((filter) => (
                <button
                  key={filter}
                  type="button"
                  className={`px-3 py-1 rounded-full font-semibold capitalize transition ${statusFilter === filter ? "bg-primary text-primary-foreground" : "bg-muted text-muted-foreground hover:text-foreground"}`}
                  onClick={() => setStatusFilter(filter)}
                >
                  {filter === "all" ? "All Orders" : filter === "car" ? "🚗 Car Orders" : filter === "delivery" ? "🛵 Delivery" : filter === "scheduled" ? "⏱️ Scheduled" : filter}
                </button>
              ))}
            </div>

            <div className="border rounded-md overflow-hidden">
              <table className="w-full text-xs">
                <thead className="bg-muted/50 border-b">
                  <tr className="text-left">
                    <th className="p-3 font-semibold">Token</th>
                    <th className="p-3 font-semibold">Order</th>
                    <th className="p-3 font-semibold">Customer / Details</th>
                    <th className="p-3 font-semibold">Items</th>
                    <th className="p-3 font-semibold">Packaging</th>
                    <th className="p-3 font-semibold">Total</th>
                    <th className="p-3 font-semibold">Status</th>
                    <th className="p-3 font-semibold">KOT</th>
                    <th className="p-3 font-semibold text-right">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {orders.length === 0 ? (
                    <tr>
                      <td colSpan={9} className="p-8 text-center text-muted-foreground">
                        No parcel orders recorded for today yet.
                      </td>
                    </tr>
                  ) : (
                    orders
                      .filter((ord) => {
                        if (historySearch) {
                          const q = historySearch.toLowerCase();
                          const matches =
                            ord.dailyTokenNumber.toString().includes(q) ||
                            ord.orderNumber.toString().includes(q) ||
                            (ord.customerName && ord.customerName.toLowerCase().includes(q)) ||
                            (ord.externalOrderReference && ord.externalOrderReference.toLowerCase().includes(q)) ||
                            (ord.deliveryProvider && ord.deliveryProvider.toLowerCase().includes(q));
                          if (!matches) return false;
                        }
                        if (statusFilter === "scheduled") return ord.status === "Scheduled";
                        if (statusFilter === "car") return ord.orderChannel === "CarParking";
                        if (statusFilter === "delivery") return ord.orderChannel === "Delivery" || !!ord.deliveryProvider;
                        if (statusFilter === "settled") return ord.status === "Served" || (ord.bill && ord.bill.remainingAmount <= 0.001);
                        if (statusFilter === "active") return ord.status !== "Served" && ord.status !== "Cancelled";
                        return true;
                      })
                      .map((ord) => (
                      <tr key={ord.orderId} className="hover:bg-muted/20">
                        <td className="p-3 font-mono font-bold text-sm text-primary">
                          #{ord.dailyTokenNumber.toString().padStart(3, "0")}
                        </td>
                        <td className="p-3 font-mono text-muted-foreground">
                          <div>ORD-{ord.orderNumber.toString().padStart(4, "0")}</div>
                          {ord.orderChannel === "CarParking" && (
                            <Badge variant="outline" className="text-[9px] bg-purple-50 text-purple-700 border-purple-300">
                              🚗 Car: {ord.vehiclePlate || "Plate N/A"}
                            </Badge>
                          )}
                          {ord.deliveryProvider && (
                            <Badge
                              variant="outline"
                              className={`text-[9px] ${
                                ord.deliveryProvider.toLowerCase() === "swiggy"
                                  ? "bg-orange-50 text-orange-700 border-orange-300"
                                  : ord.deliveryProvider.toLowerCase() === "zomato"
                                  ? "bg-red-50 text-red-700 border-red-300"
                                  : "bg-blue-50 text-blue-700 border-blue-300"
                              }`}
                            >
                              🛵 {ord.deliveryProvider}
                            </Badge>
                          )}
                          {ord.externalOrderReference && (
                            <div className="text-[10px] font-mono font-bold text-foreground">
                              Ext: {ord.externalOrderReference}
                            </div>
                          )}
                          {ord.status === "Scheduled" && (
                            <Badge variant="outline" className="text-[9px] bg-amber-50 text-amber-700 border-amber-300">
                              ⏱️ Pickup: {ord.scheduledPickupTimeLocal || "Advance"}
                            </Badge>
                          )}
                        </td>
                        <td className="p-3">
                          <div className="font-medium">{ord.customerName || "Walk-in Guest"}</div>
                          {ord.customerPhone && (
                            <div className="text-[11px] text-muted-foreground">{ord.customerPhone}</div>
                          )}
                          {ord.orderChannel === "CarParking" && ord.parkingBay && (
                            <div className="text-[10px] text-purple-600 font-medium">Bay: {ord.parkingBay}</div>
                          )}
                        </td>
                        <td className="p-3">
                          <span className="font-semibold">{ord.items.length} items</span>
                          <span className="text-muted-foreground ml-1">
                            ({ord.items.reduce((a, b) => a + b.quantity, 0)} qty)
                          </span>
                        </td>
                        <td className="p-3">
                          {ord.isPackagingWaived ? (
                            <Badge variant="outline" className="text-[10px] text-amber-600 border-amber-300">
                              Waived
                            </Badge>
                          ) : (
                            <span>₹{ord.packagingCharge.toFixed(2)}</span>
                          )}
                        </td>
                        <td className="p-3">
                          <div className="font-bold">₹{ord.totalAmount.toFixed(2)}</div>
                          {ord.bill && ord.bill.paidAmount > 0 && ord.bill.remainingAmount > 0 && (
                            <span className="text-[10px] text-amber-600 block font-medium">
                              Paid: ₹{ord.bill.paidAmount.toFixed(2)} (Due: ₹{ord.bill.remainingAmount.toFixed(2)})
                            </span>
                          )}
                          {(ord.status === "Served" || (ord.bill && ord.bill.remainingAmount === 0)) && (
                            <span className="text-[10px] text-emerald-600 block font-medium">
                              Fully Settled
                            </span>
                          )}
                        </td>
                        <td className="p-3">
                          <Badge
                            variant={
                              ord.status === "Served"
                                ? "default"
                                : ord.status === "Cancelled"
                                ? "destructive"
                                : ord.status === "Scheduled"
                                ? "outline"
                                : "secondary"
                            }
                            className={ord.status === "Scheduled" ? "bg-amber-100 text-amber-800 border-amber-300 font-bold" : ""}
                          >
                            {ord.status}
                          </Badge>
                        </td>
                        <td className="p-3">
                          <Badge
                            variant={
                              ord.kotPrintStatus === "Queued" || ord.kotPrintStatus === "Printed"
                                ? "outline"
                                : ord.kotPrintStatus === "Failed"
                                ? "destructive"
                                : "secondary"
                            }
                            className="text-[10px]"
                          >
                            {ord.kotPrintStatus}
                          </Badge>
                        </td>
                        <td className="p-3 text-right space-x-1">
                          {ord.status === "Scheduled" && (
                            <>
                              <Button
                                size="sm"
                                variant="default"
                                className="h-7 text-xs px-2 bg-emerald-600 hover:bg-emerald-700"
                                onClick={() => handleReleaseScheduledOrder(ord.orderId)}
                              >
                                Release Now 🔥
                              </Button>
                              <Button
                                size="sm"
                                variant="outline"
                                className="h-7 text-xs px-2"
                                onClick={() => {
                                  setRescheduleOrderModal(ord);
                                  setNewPickupTimeLocal(ord.scheduledPickupTimeLocal || "");
                                  setNewLeadMinutes(ord.preparationLeadMinutes || 15);
                                }}
                              >
                                Reschedule 📅
                              </Button>
                            </>
                          )}
                          {ord.orderChannel === "CarParking" && ord.status !== "Served" && ord.status !== "Cancelled" && (
                            <Button
                              size="sm"
                              variant="default"
                              className="h-7 text-xs px-2 bg-purple-600 hover:bg-purple-700"
                              onClick={() => handleDeliverCarOrder(ord.orderId)}
                            >
                              Deliver 🚗
                            </Button>
                          )}
                          {ord.status !== "Cancelled" && (
                            <Button
                              size="sm"
                              variant={ord.status === "Served" ? "outline" : "default"}
                              className="h-7 text-xs px-2"
                              onClick={() => openPaymentModal(ord)}
                            >
                              {ord.status === "Served" ? "View Payments" : "Collect Payment"}
                            </Button>
                          )}
                          <Button
                            size="sm"
                            variant="outline"
                            className="h-7 text-xs px-2"
                            onClick={() => setReprintModalJob(ord)}
                          >
                            Print Receipt
                          </Button>
                          {ord.kotPrintStatus === "Failed" && (
                            <Button
                              size="sm"
                              variant="destructive"
                              className="h-7 text-xs px-2"
                              onClick={() => handleRetryKot(ord.orderId)}
                            >
                              Retry KOT
                            </Button>
                          )}
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </CardContent>
        </Card>
      )}

      {/* TAB: MARKETPLACE PAYOUTS & RECONCILIATION */}
      {activeTab === "payouts" && (
        <Card>
          <CardHeader>
            <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-3">
              <div>
                <CardTitle className="text-base font-bold flex items-center gap-2">
                  <IndianRupee className="h-5 w-5 text-primary" />
                  Marketplace Payouts & Commission Reconciliation
                </CardTitle>
                <CardDescription className="text-xs">
                  Record settlements received from Swiggy, Zomato, and delivery partners. Trace gross GMV, commissions deducted, TCS/GST, and net bank deposits.
                </CardDescription>
              </div>
              <Button size="sm" onClick={() => setPayoutModalOpen(true)}>
                <Plus className="h-4 w-4 mr-1.5" />
                Record Marketplace Payout
              </Button>
            </div>
          </CardHeader>
          <CardContent className="space-y-4">
            {/* Summary Metrics */}
            <div className="grid grid-cols-1 sm:grid-cols-4 gap-3 text-xs">
              <div className="p-3 bg-muted/40 rounded-xl border">
                <span className="text-[11px] text-muted-foreground block font-medium">Total Payouts Recorded</span>
                <span className="text-lg font-bold">{payouts.length} batch(es)</span>
              </div>
              <div className="p-3 bg-muted/40 rounded-xl border">
                <span className="text-[11px] text-muted-foreground block font-medium">Gross GMV Reconciled</span>
                <span className="text-lg font-bold font-mono">
                  ₹{payouts.reduce((acc, p) => acc + (p.grossAmount || 0), 0).toFixed(2)}
                </span>
              </div>
              <div className="p-3 bg-red-50/40 dark:bg-red-950/20 border border-red-200 dark:border-red-900 rounded-xl">
                <span className="text-[11px] text-red-700 dark:text-red-400 block font-medium">Total Commission & Tax</span>
                <span className="text-lg font-bold font-mono text-red-600 dark:text-red-400">
                  -₹{payouts.reduce((acc, p) => acc + (p.commissionDeducted || 0) + (p.tcsGstDeducted || 0), 0).toFixed(2)}
                </span>
              </div>
              <div className="p-3 bg-emerald-50/40 dark:bg-emerald-950/20 border border-emerald-200 dark:border-emerald-900 rounded-xl">
                <span className="text-[11px] text-emerald-700 dark:text-emerald-400 block font-medium">Net Payout Received</span>
                <span className="text-lg font-bold font-mono text-emerald-600 dark:text-emerald-400">
                  ₹{payouts.reduce((acc, p) => acc + (p.netAmount || 0), 0).toFixed(2)}
                </span>
              </div>
            </div>

            {/* Payouts Table */}
            <div className="border rounded-md overflow-hidden">
              <table className="w-full text-xs">
                <thead className="bg-muted/50 border-b">
                  <tr className="text-left">
                    <th className="p-3 font-semibold">Date</th>
                    <th className="p-3 font-semibold">Provider</th>
                    <th className="p-3 font-semibold">Payout Reference</th>
                    <th className="p-3 font-semibold text-right">Gross GMV</th>
                    <th className="p-3 font-semibold text-right">Commission</th>
                    <th className="p-3 font-semibold text-right">TCS / GST</th>
                    <th className="p-3 font-semibold text-right">Adjustments</th>
                    <th className="p-3 font-semibold text-right">Net Amount</th>
                    <th className="p-3 font-semibold">Notes</th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {payouts.length === 0 ? (
                    <tr>
                      <td colSpan={9} className="p-8 text-center text-muted-foreground">
                        {loadingPayouts ? "Loading payouts..." : "No marketplace payouts recorded yet. Click 'Record Marketplace Payout' above."}
                      </td>
                    </tr>
                  ) : (
                    payouts.map((p) => (
                      <tr key={p.id} className="hover:bg-muted/20">
                        <td className="p-3 text-muted-foreground whitespace-nowrap">
                          {new Date(p.payoutDateUtc || p.createdAtUtc).toLocaleDateString("en-IN", { timeZone: "Asia/Kolkata" })}
                        </td>
                        <td className="p-3 font-semibold">
                          <Badge
                            variant="outline"
                            className={`text-[9px] ${
                              p.deliveryProvider.toLowerCase() === "swiggy"
                                ? "bg-orange-50 text-orange-700 border-orange-300"
                                : p.deliveryProvider.toLowerCase() === "zomato"
                                ? "bg-red-50 text-red-700 border-red-300"
                                : "bg-blue-50 text-blue-700 border-blue-300"
                            }`}
                          >
                            {p.deliveryProvider}
                          </Badge>
                        </td>
                        <td className="p-3 font-mono font-bold">{p.payoutReference}</td>
                        <td className="p-3 text-right font-mono">₹{p.grossAmount.toFixed(2)}</td>
                        <td className="p-3 text-right font-mono text-red-600">-₹{p.commissionDeducted.toFixed(2)}</td>
                        <td className="p-3 text-right font-mono text-red-600">-₹{p.tcsGstDeducted.toFixed(2)}</td>
                        <td className="p-3 text-right font-mono">
                          {p.otherAdjustments >= 0 ? `+₹${p.otherAdjustments.toFixed(2)}` : `-₹${Math.abs(p.otherAdjustments).toFixed(2)}`}
                        </td>
                        <td className="p-3 text-right font-mono font-bold text-emerald-600">₹{p.netAmount.toFixed(2)}</td>
                        <td className="p-3 text-muted-foreground text-[11px] max-w-xs truncate">{p.notes || "—"}</td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </CardContent>
        </Card>
      )}

      {/* MODAL: RECORD MARKETPLACE PAYOUT */}
      <Dialog open={payoutModalOpen} onOpenChange={setPayoutModalOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle className="font-bold flex items-center gap-2">
              <IndianRupee className="h-5 w-5 text-primary" />
              Record Marketplace Payout
            </DialogTitle>
            <DialogDescription className="text-xs">
              Log settlement statements received from food aggregators to reconcile bank credits against gross orders.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-3 py-2 text-xs">
            <div className="grid grid-cols-2 gap-2">
              <div>
                <label className="font-semibold block mb-1">Provider *</label>
                <select
                  value={payoutProvider}
                  onChange={(e) => setPayoutProvider(e.target.value as any)}
                  className="w-full h-8 border rounded-md px-2 bg-background text-xs"
                >
                  <option value="Swiggy">Swiggy</option>
                  <option value="Zomato">Zomato</option>
                  <option value="Other Delivery">Other Delivery</option>
                </select>
              </div>
              <div>
                <label className="font-semibold block mb-1">Payout Statement Ref *</label>
                <Input
                  placeholder="e.g. SWIGGY-PAY-20261121"
                  value={payoutRef}
                  onChange={(e) => setPayoutRef(e.target.value)}
                  className="h-8 text-xs font-mono font-bold"
                />
              </div>
            </div>

            <div className="grid grid-cols-2 gap-2">
              <div>
                <label className="font-semibold block mb-1">Gross Order Total (₹) *</label>
                <Input
                  type="number"
                  step="0.01"
                  placeholder="0.00"
                  value={payoutGross}
                  onChange={(e) => setPayoutGross(e.target.value)}
                  className="h-8 text-xs font-mono"
                />
              </div>
              <div>
                <label className="font-semibold block mb-1">Commission Deducted (₹)</label>
                <Input
                  type="number"
                  step="0.01"
                  placeholder="0.00"
                  value={payoutCommission}
                  onChange={(e) => setPayoutCommission(e.target.value)}
                  className="h-8 text-xs font-mono"
                />
              </div>
            </div>

            <div className="grid grid-cols-2 gap-2">
              <div>
                <label className="font-semibold block mb-1">TCS & GST Deducted (₹)</label>
                <Input
                  type="number"
                  step="0.01"
                  placeholder="0.00"
                  value={payoutTcs}
                  onChange={(e) => setPayoutTcs(e.target.value)}
                  className="h-8 text-xs font-mono"
                />
              </div>
              <div>
                <label className="font-semibold block mb-1">Other Adjustments (₹)</label>
                <Input
                  type="number"
                  step="0.01"
                  placeholder="0.00"
                  value={payoutAdjustments}
                  onChange={(e) => setPayoutAdjustments(e.target.value)}
                  className="h-8 text-xs font-mono"
                />
              </div>
            </div>

            <div>
              <label className="font-semibold block mb-1">Net Deposited Amount (₹)</label>
              <Input
                type="number"
                step="0.01"
                placeholder="Auto-calculated from Gross - Deductions"
                value={payoutNet}
                onChange={(e) => setPayoutNet(e.target.value)}
                className="h-8 text-xs font-mono font-bold text-emerald-600"
              />
              <span className="text-[10px] text-muted-foreground block mt-0.5">
                Expected Net: ₹{(
                  (parseFloat(payoutGross) || 0) -
                  (parseFloat(payoutCommission) || 0) -
                  (parseFloat(payoutTcs) || 0) +
                  (parseFloat(payoutAdjustments) || 0)
                ).toFixed(2)}
              </span>
            </div>

            <div>
              <label className="font-semibold block mb-1">Notes / UTR Reference</label>
              <Input
                placeholder="Bank UTR number, reconciliation notes"
                value={payoutNotes}
                onChange={(e) => setPayoutNotes(e.target.value)}
                className="h-8 text-xs"
              />
            </div>
          </div>

          <DialogFooter className="flex sm:justify-between gap-2">
            <Button variant="outline" size="sm" onClick={() => setPayoutModalOpen(false)}>
              Cancel
            </Button>
            <Button size="sm" disabled={savingPayout || !payoutRef} onClick={handleRecordPayout}>
              {savingPayout ? <RefreshCw className="h-4 w-4 animate-spin mr-1.5" /> : null}
              Save & Reconcile Payout
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* TAB 3: PACKAGING SETTINGS */}
      {activeTab === "settings" && (
        <Card className="max-w-2xl">
          <CardHeader>
            <CardTitle className="text-base font-bold flex items-center gap-2">
              <Settings className="h-5 w-5 text-primary" />
              Packaging Charge Rules
            </CardTitle>
            <CardDescription className="text-xs">
              Configure how parcel and takeaway packaging is calculated. Multiple methods are never combined accidentally.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4 text-xs">
            <div>
              <label className="font-semibold block mb-1">Packaging Calculation Mode</label>
              <select
                value={editChargeMode}
                onChange={(e) => setEditChargeMode(e.target.value as any)}
                className="w-full h-9 border rounded-md px-3 bg-background text-xs"
              >
                <option value="None">None (No packaging charge)</option>
                <option value="FlatPerOrder">Flat Fee Per Order</option>
                <option value="Percentage">Percentage of Food Subtotal</option>
                <option value="PerItem">Per-Item Rate (Configured on Menu Dishes)</option>
              </select>
            </div>

            {editChargeMode === "FlatPerOrder" && (
              <div>
                <label className="font-semibold block mb-1">Flat Rate Per Order (INR)</label>
                <Input
                  type="number"
                  step="0.01"
                  value={editFlatRate}
                  onChange={(e) => setEditFlatRate(parseFloat(e.target.value) || 0)}
                  className="h-9 text-xs"
                />
              </div>
            )}

            {editChargeMode === "Percentage" && (
              <div>
                <label className="font-semibold block mb-1">Percentage of Food Subtotal (%)</label>
                <Input
                  type="number"
                  step="0.1"
                  value={editPercentage}
                  onChange={(e) => setEditPercentage(parseFloat(e.target.value) || 0)}
                  className="h-9 text-xs"
                />
              </div>
            )}

            <div className="pt-2 border-t space-y-2">
              <label className="flex items-center gap-2 cursor-pointer">
                <input
                  type="checkbox"
                  checked={editIsTaxable}
                  onChange={(e) => setEditIsTaxable(e.target.checked)}
                  className="rounded"
                />
                <span className="font-semibold">Apply GST / Tax on Packaging Charges</span>
              </label>

              {editIsTaxable && (
                <div>
                  <label className="font-semibold block mb-1">Packaging GST Rate (%)</label>
                  <Input
                    type="number"
                    step="0.1"
                    value={editTaxRate}
                    onChange={(e) => setEditTaxRate(parseFloat(e.target.value) || 0)}
                    className="h-9 text-xs"
                  />
                </div>
              )}
            </div>

            <div className="pt-3">
              <Button onClick={handleSaveSettings} disabled={savingSettings} className="font-semibold">
                {savingSettings && <RefreshCw className="h-4 w-4 animate-spin mr-2" />}
                Save Packaging Rules
              </Button>
            </div>
          </CardContent>
        </Card>
      )}

      {/* MODAL: ORDER CREATED SUCCESS DIALOG */}
      <Dialog open={!!createdOrder} onOpenChange={() => setCreatedOrder(null)}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2 text-primary font-bold">
              <CheckCircle2 className="h-5 w-5 text-emerald-500" />
              Takeaway Token #{createdOrder?.dailyTokenNumber.toString().padStart(3, "0")}
            </DialogTitle>
            <DialogDescription className="text-xs">
              Parcel order created and multi-station KOT print jobs dispatched.
            </DialogDescription>
          </DialogHeader>

          {createdOrder && (
            <div className="space-y-3 py-2 text-xs">
              <div className="p-4 bg-muted/60 rounded-xl text-center">
                <div className="text-xs text-muted-foreground uppercase tracking-widest font-bold">Token Number</div>
                <div className="text-4xl font-extrabold text-primary font-mono my-1">
                  #{createdOrder.dailyTokenNumber.toString().padStart(3, "0")}
                </div>
                <div className="text-xs text-muted-foreground">Order ORD-{createdOrder.orderNumber.toString().padStart(4, "0")}</div>
              </div>

              <div className="divide-y border rounded-lg p-2.5 space-y-1">
                <div className="flex justify-between">
                  <span className="text-muted-foreground">Customer</span>
                  <span className="font-medium">{createdOrder.customerName || "Walk-in Guest"}</span>
                </div>
                <div className="flex justify-between pt-1">
                  <span className="text-muted-foreground">Food Items</span>
                  <span>{createdOrder.items.length} items</span>
                </div>
                {createdOrder.packagingCharge > 0 && (
                  <div className="flex justify-between pt-1">
                    <span className="text-muted-foreground">Packaging</span>
                    <span>₹{createdOrder.packagingCharge.toFixed(2)}</span>
                  </div>
                )}
                <div className="flex justify-between font-bold pt-1 text-sm text-primary">
                  <span>Grand Total</span>
                  <span>₹{createdOrder.totalAmount.toFixed(2)}</span>
                </div>
              </div>
            </div>
          )}

          <DialogFooter className="flex sm:justify-between gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={() => {
                if (createdOrder) openPaymentModal(createdOrder);
                setCreatedOrder(null);
              }}
            >
              Collect Payment Now
            </Button>
            <Button size="sm" onClick={() => setCreatedOrder(null)}>
              Done
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* MODAL: PARCEL PARTIAL-PAYMENT & SETTLEMENT */}
      <Dialog open={!!paymentModalOrder} onOpenChange={() => setPaymentModalOrder(null)}>
        <DialogContent className="sm:max-w-lg max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <div className="flex justify-between items-start">
              <div>
                <DialogTitle className="font-bold flex items-center gap-2">
                  <Receipt className="h-5 w-5 text-primary" />
                  Token #{paymentModalOrder?.dailyTokenNumber.toString().padStart(3, "0")} — Payments
                </DialogTitle>
                <DialogDescription className="text-xs">
                  Record Cash, UPI, or Card payments. Final settlement occurs when remaining balance reaches zero.
                </DialogDescription>
              </div>
              <Button
                size="sm"
                variant="ghost"
                className="h-8 w-8 p-0"
                onClick={handleRefreshPaymentOrder}
                disabled={loadingPaymentOrder}
                title="Refresh authoritative server balance"
              >
                <RefreshCw className={`h-4 w-4 ${loadingPaymentOrder ? "animate-spin" : ""}`} />
              </Button>
            </div>
          </DialogHeader>

          {paymentModalOrder && (() => {
            const bill = paymentModalOrder.bill;
            const total = paymentModalOrder.totalAmount;
            const paid = bill?.paidAmount ?? 0;
            const remaining = bill?.remainingAmount ?? (total - paid);
            const upiAmount = Number(paymentAmount || remaining);
            const canGenerateUpiQr = Number.isFinite(upiAmount) && upiAmount > 0 &&
              upiAmount <= remaining && (!paymentAmount || /^\d+(\.\d{1,2})?$/.test(paymentAmount));
            const upiQrData = canGenerateUpiQr && !savedPaymentOp && !loadingPaymentOrder &&
              upiRequest?.billId === bill?.id && upiRequest?.amount === upiAmount &&
              upiRequest.remainingBalance === remaining ? upiRequest : null;
            const isFullySettled = remaining <= 0.001 || paymentModalOrder.status === "Served";

            return (
              <div className="space-y-4 py-2 text-xs">
                {/* Balance Metrics Grid */}
                <div className="grid grid-cols-3 gap-2 text-center">
                  <div className="p-2.5 bg-muted/50 rounded-lg border">
                    <span className="text-[10px] text-muted-foreground block font-medium">Total Bill</span>
                    <span className="text-sm font-bold">₹{total.toFixed(2)}</span>
                  </div>
                  <div className="p-2.5 bg-muted/50 rounded-lg border">
                    <span className="text-[10px] text-muted-foreground block font-medium">Total Collected</span>
                    <span className="text-sm font-bold text-emerald-600">₹{paid.toFixed(2)}</span>
                  </div>
                  <div className={`p-2.5 rounded-lg border ${remaining > 0 ? "bg-amber-500/10 border-amber-300 text-amber-700 dark:text-amber-400" : "bg-emerald-500/10 border-emerald-300 text-emerald-700 dark:text-emerald-400"}`}>
                    <span className="text-[10px] block font-medium">Balance Due</span>
                    <span className="text-sm font-extrabold">₹{Math.max(0, remaining).toFixed(2)}</span>
                  </div>
                </div>

                {/* Payment History List */}
                <div className="space-y-1.5">
                  <div className="font-semibold text-muted-foreground flex justify-between items-center text-[11px]">
                    <span>Payment History</span>
                    <span>{bill?.payments?.length || 0} transaction(s)</span>
                  </div>
                  <div className="border rounded-md divide-y max-h-36 overflow-y-auto">
                    {!bill?.payments || bill.payments.length === 0 ? (
                      <div className="p-3 text-center text-muted-foreground text-[11px]">
                        No payments recorded yet. Collect payment below.
                      </div>
                    ) : (
                      bill.payments.map((p, idx) => (
                        <div key={p.id || idx} className="p-2 flex justify-between items-center text-[11px]">
                          <div className="flex items-center gap-2">
                            <Badge variant="outline" className="text-[10px] font-mono">
                              {p.method}
                            </Badge>
                            <span className="text-muted-foreground">
                              {new Date(p.collectedAtUtc).toLocaleTimeString("en-IN", { timeZone: "Asia/Kolkata" })}
                            </span>
                          </div>
                          <span className="font-bold text-emerald-600">₹{p.amount.toFixed(2)}</span>
                        </div>
                      ))
                    )}
                  </div>
                </div>

                {/* Form to Record Next Payment (Only if balance remains) */}
                {!isFullySettled ? (
                  <div className="space-y-3 pt-2 border-t">
                    {savedPaymentOp && (
                      <div className="p-3 bg-amber-500/10 border border-amber-500/30 rounded-lg text-amber-800 dark:text-amber-300 text-xs space-y-1.5">
                        <div className="flex items-center justify-between">
                          <div className="flex items-center gap-1.5 font-bold">
                            <AlertCircle className="h-4 w-4 text-amber-600 shrink-0" />
                            Unresolved Payment In-Flight
                          </div>
                          <Badge variant="outline" className="border-amber-400 text-amber-600 text-[10px]">
                            Pending Confirmation
                          </Badge>
                        </div>
                        <p className="text-[11px] leading-relaxed">
                          A {savedPaymentOp.method} payment of ₹{savedPaymentOp.amount.toFixed(2)} was sent but unconfirmed due to a network interruption. Replaying will retrieve or finalize the payment safely without duplicate collection.
                        </p>
                      </div>
                    )}

                    <div>
                      <label className="font-semibold block mb-1 text-[11px]">Select Payment Method</label>
                      <div className="grid grid-cols-2 sm:grid-cols-4 gap-2">
                        {((paymentModalOrder.deliveryProvider || paymentModalOrder.orderChannel === "Delivery" || paymentModalOrder.marketplaceCollected)
                          ? (["MarketplaceCollected", "Cash", "UPI", "Card"] as const)
                          : (["Cash", "UPI", "Card"] as const)
                        ).map((m) => (
                          <Button
                            key={m}
                            type="button"
                            variant={paymentMethod === m ? "default" : "outline"}
                            className={`h-8 text-xs font-bold ${
                              m === "MarketplaceCollected"
                                ? paymentMethod === m
                                  ? "bg-orange-600 hover:bg-orange-700 text-white"
                                  : "border-orange-300 text-orange-700 hover:bg-orange-50 dark:hover:bg-orange-950"
                                : ""
                            }`}
                            disabled={!!savedPaymentOp}
                            onClick={() => {
                              setPaymentMethod(m as any);
                              setPaymentOpId(crypto.randomUUID());
                              setUpiQrData(null);
                            }}
                          >
                            {m === "MarketplaceCollected" ? "🛵 Marketplace" : m}
                          </Button>
                        ))}
                      </div>
                    </div>

                    {paymentMethod === "MarketplaceCollected" && (
                      <div className="p-3 bg-orange-50/60 dark:bg-orange-950/20 border border-orange-200 dark:border-orange-800 rounded-lg space-y-1 text-[11px] text-orange-900 dark:text-orange-300">
                        <div className="font-bold flex items-center gap-1.5">
                          <span>🛵 Prepaid Marketplace Settlement</span>
                          <Badge variant="outline" className="border-orange-400 text-orange-700 text-[9px]">
                            Cash / UPI Uninflated
                          </Badge>
                        </div>
                        <p className="text-[10px] leading-relaxed">
                          Marks this bill settled as prepaid via {paymentModalOrder.deliveryProvider || "Marketplace"}. Deducts physical stock items once and records an immutable receipt without inflating local cash registers.
                        </p>
                      </div>
                    )}

                    {paymentMethod === "UPI" && (
                      <div className="p-3 bg-purple-50/60 dark:bg-purple-950/20 border border-purple-200 dark:border-purple-800 rounded-lg space-y-2">
                        <div className="flex justify-between items-center">
                          <span className="font-bold text-xs text-purple-900 dark:text-purple-300">
                            Dynamic UPI QR Intent
                          </span>
                          <Button
                            size="sm"
                            variant="outline"
                            className="h-6 text-[10px] px-2 text-purple-700 border-purple-300"
                            disabled={fetchingUpiQr || loadingPaymentOrder || !!savedPaymentOp || !canGenerateUpiQr}
                            onClick={() => handleFetchUpiQr(bill.id, upiAmount)}
                          >
                            {fetchingUpiQr ? "Generating..." : upiQrData ? "Refresh QR" : "Generate UPI QR"}
                          </Button>
                        </div>
                        {upiQrData && (
                          <div className="space-y-1.5 text-[11px]">
                            <div className="flex justify-between">
                              <span className="text-muted-foreground">Payee:</span>
                              <span className="font-medium">{upiQrData.payeeName} ({upiQrData.payeeVpa})</span>
                            </div>
                            <div className="flex justify-between">
                              <span className="text-muted-foreground">Amount:</span>
                              <span className="font-bold text-primary">₹{upiQrData.amount.toFixed(2)}</span>
                            </div>
                            <div className="flex justify-between">
                              <span className="text-muted-foreground">Reference:</span>
                              <span className="font-mono">{upiQrData.reference}</span>
                            </div>
                            <div className="w-fit rounded-lg border bg-white">
                              <QRCodeSVG value={upiQrData.upiUri} size={160} marginSize={4} title="Scan to pay with UPI" />
                            </div>
                            <div className="pt-1 flex gap-2">
                              <a
                                href={upiQrData.upiUri}
                                target="_blank"
                                rel="noreferrer"
                                className="inline-flex items-center justify-center rounded-md bg-purple-600 text-white px-3 py-1 text-xs font-semibold hover:bg-purple-700"
                              >
                                Open UPI App
                              </a>
                            </div>
                          </div>
                        )}
                      </div>
                    )}

                    <div>
                      <div className="flex justify-between items-center mb-1">
                        <label className="font-semibold text-[11px]">Payment Amount (₹)</label>
                        <div className="flex gap-1">
                          <button
                            type="button"
                            disabled={!!savedPaymentOp}
                            className="text-[10px] text-primary hover:underline font-medium disabled:opacity-50"
                            onClick={() => setPaymentAmount(remaining.toFixed(2))}
                          >
                            Full (₹{remaining.toFixed(2)})
                          </button>
                          {remaining > 50 && (
                            <>
                              <span className="text-muted-foreground text-[10px]">|</span>
                              <button
                                type="button"
                                disabled={!!savedPaymentOp}
                                className="text-[10px] text-primary hover:underline font-medium disabled:opacity-50"
                                onClick={() => setPaymentAmount("50.00")}
                              >
                                ₹50
                              </button>
                            </>
                          )}
                          {remaining > 100 && (
                            <>
                              <span className="text-muted-foreground text-[10px]">|</span>
                              <button
                                type="button"
                                disabled={!!savedPaymentOp}
                                className="text-[10px] text-primary hover:underline font-medium disabled:opacity-50"
                                onClick={() => setPaymentAmount("100.00")}
                              >
                                ₹100
                              </button>
                            </>
                          )}
                        </div>
                      </div>
                      <div className="relative">
                        <Input
                          type="number"
                          step="0.01"
                          min="0.01"
                          max={remaining}
                          value={paymentAmount}
                          readOnly={!!savedPaymentOp}
                          onChange={(e) => {
                            if (!savedPaymentOp) {
                              setPaymentAmount(e.target.value);
                              setPaymentError(null);
                            }
                          }}
                          placeholder="0.00"
                          className="h-9 font-mono text-sm"
                        />
                      </div>
                      <div className="text-[10px] text-muted-foreground mt-1 flex justify-between">
                        <span>Max allowable: ₹{remaining.toFixed(2)}</span>
                        <span className="font-mono text-[9px] opacity-75">OpId: {paymentOpId.slice(0, 8)}...</span>
                      </div>
                    </div>

                    {paymentError && (
                      <div className="p-2.5 bg-destructive/10 border border-destructive/20 rounded-md text-destructive text-[11px] flex items-start gap-1.5">
                        <AlertCircle className="h-4 w-4 shrink-0 mt-0.5" />
                        <div>{paymentError}</div>
                      </div>
                    )}

                    {paymentSuccess && (
                      <div className="p-2.5 bg-emerald-500/10 border border-emerald-500/20 rounded-md text-emerald-600 text-[11px] flex items-start gap-1.5">
                        <CheckCircle2 className="h-4 w-4 shrink-0 mt-0.5" />
                        <div>{paymentSuccess}</div>
                      </div>
                    )}

                    <Button
                      type="button"
                      className="w-full font-bold h-9"
                      onClick={handleRecordPayment}
                      disabled={recordingPayment || loadingPaymentOrder || (!savedPaymentOp && (!paymentAmount || parseFloat(paymentAmount) <= 0))}
                    >
                      {recordingPayment ? (
                        <RefreshCw className="h-4 w-4 animate-spin mr-2" />
                      ) : savedPaymentOp ? (
                        <RefreshCw className="h-4 w-4 mr-1.5" />
                      ) : (
                        <IndianRupee className="h-4 w-4 mr-1.5" />
                      )}
                      {savedPaymentOp
                        ? `Replay Unresolved ${savedPaymentOp.method} Payment (₹${savedPaymentOp.amount.toFixed(2)})`
                        : `Record ${paymentMethod} Payment`}
                    </Button>

                    {savedPaymentOp && (
                      <div className="text-center pt-1">
                        <button
                          type="button"
                          className="text-[10px] text-muted-foreground hover:text-destructive underline"
                          onClick={() => {
                            const storageKey = getParcelPaymentStorageKey(effectiveEmployeeId, restaurantId, paymentModalOrder.orderId);
                            localStorage.removeItem(storageKey);
                            setSavedPaymentOp(null);
                            setPaymentOpId(crypto.randomUUID());
                          }}
                        >
                          Discard saved draft (Requires cashier verification)
                        </button>
                      </div>
                    )}
                  </div>
                ) : (
                  /* Fully Settled Section */
                  <div className="space-y-3 pt-2 border-t">
                    <div className="p-3 bg-emerald-500/10 border border-emerald-500/30 rounded-lg text-emerald-700 dark:text-emerald-400 text-center space-y-1">
                      <div className="flex items-center justify-center gap-1.5 font-bold text-sm">
                        <CheckCircle2 className="h-4 w-4 text-emerald-600" />
                        Bill Fully Settled
                      </div>
                      <p className="text-[11px] opacity-90">
                        Total collected: ₹{paid.toFixed(2)}. Remaining balance is ₹0.00. Order status is Served.
                      </p>
                    </div>

                    <div className="flex gap-2">
                      <Button
                        type="button"
                        variant="default"
                        className="flex-1 font-bold h-9"
                        onClick={handlePrintSettledReceipt}
                        disabled={printingReceipt}
                      >
                        {printingReceipt ? (
                          <RefreshCw className="h-4 w-4 animate-spin mr-2" />
                        ) : (
                          <Printer className="h-4 w-4 mr-1.5" />
                        )}
                        Print Settled Receipt
                      </Button>
                    </div>

                    {settledReceiptText && (
                      <div className="mt-2 p-2.5 bg-muted/60 rounded-md border text-[10px] font-mono whitespace-pre overflow-x-auto max-h-48">
                        {settledReceiptText}
                      </div>
                    )}
                  </div>
                )}
              </div>
            );
          })()}

          <DialogFooter>
            <Button variant="outline" size="sm" onClick={() => setPaymentModalOrder(null)}>
              Close
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* MODAL: REPRINT RECEIPT */}
      <Dialog open={!!reprintModalJob} onOpenChange={() => setReprintModalJob(null)}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle className="font-bold flex items-center gap-2">
              <Printer className="h-5 w-5 text-primary" />
              Reprint Takeaway Receipt
            </DialogTitle>
            <DialogDescription className="text-xs">
              Audited reprints record staff identity and reprint reason.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-3 py-2 text-xs">
            <div>
              <label className="font-semibold block mb-1">Reprint Reason</label>
              <Input
                placeholder="e.g. Guest requested additional physical copy"
                value={reprintReason}
                onChange={(e) => setReprintReason(e.target.value)}
                className="h-8 text-xs"
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" size="sm" onClick={() => setReprintModalJob(null)}>
              Cancel
            </Button>
            <Button size="sm" onClick={handleReprintReceipt} disabled={reprinting} className="font-bold">
              {reprinting && <RefreshCw className="h-4 w-4 animate-spin mr-2" />}
              Queue Reprint
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* MODAL: RESCHEDULE ADVANCE ORDER */}
      <Dialog open={!!rescheduleOrderModal} onOpenChange={() => setRescheduleOrderModal(null)}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2 font-bold">
              <Clock className="h-5 w-5 text-primary" />
              Reschedule Token #{rescheduleOrderModal?.dailyTokenNumber.toString().padStart(3, "0")}
            </DialogTitle>
            <DialogDescription className="text-xs">
              Change pickup time or kitchen preparation lead time. Token will safely reallocate if pickup date changes.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-3 py-2 text-xs">
            <div>
              <label className="font-semibold block mb-1">New Pickup Time (Asia/Kolkata) *</label>
              <Input
                type="datetime-local"
                value={newPickupTimeLocal}
                onChange={(e) => setNewPickupTimeLocal(e.target.value)}
                className="h-9 text-xs"
              />
            </div>
            <div>
              <label className="font-semibold block mb-1">Kitchen Lead Time (Minutes)</label>
              <Input
                type="number"
                min="5"
                max="120"
                value={newLeadMinutes}
                onChange={(e) => setNewLeadMinutes(parseInt(e.target.value) || 15)}
                className="h-9 text-xs"
              />
            </div>
          </div>
          <DialogFooter className="flex justify-end gap-2">
            <Button variant="outline" size="sm" onClick={() => setRescheduleOrderModal(null)}>
              Cancel
            </Button>
            <Button
              size="sm"
              disabled={rescheduling || !newPickupTimeLocal}
              onClick={handleRescheduleOrder}
            >
              {rescheduling && <RefreshCw className="h-4 w-4 animate-spin mr-1.5" />}
              Save & Reschedule
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
