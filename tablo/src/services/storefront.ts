import { apiClient } from "./apiClient.ts";

export interface StorefrontOutletSummary {
  outletId: string;
  restaurantId: string;
  name: string;
  address: string;
  phone: string;
  isOpenNow: boolean;
  operatingHours: string;
  leadTimeMinutes: number;
  minOrderAmount: number;
  deliveryBaseFee: number;
  freeDeliveryAboveAmount: number;
  isPublished: boolean;
  isTemporarilyClosed: boolean;
  closureReason?: string;
}

export interface UpdateStorefrontOutletSettingsRequest {
  isOpen?: boolean;
  isPublished?: boolean;
  openingTime?: string;
  closingTime?: string;
  isOvernight?: boolean;
  leadTimeMinutes?: number;
  minOrderAmount?: number;
  deliveryBaseFee?: number;
  freeDeliveryAboveAmount?: number;
  phone?: string;
  isTemporarilyClosed?: boolean;
  closureReason?: string;
}

export interface StorefrontOrderItem {
  menuItemId: string;
  name: string;
  quantity: number;
  unitPrice: number;
  modifiers?: { modifierOptionId: string; optionName: string; priceDelta: number }[];
  cookingNotes?: string;
}

export interface StorefrontCheckoutRequest {
  outletId: string;
  orderChannel: "DirectDelivery" | "TakeawayPickup" | "DineInDirect";
  fulfillmentTiming: "Immediate" | "Scheduled";
  scheduledForUtc?: string;
  customerName: string;
  customerPhone: string;
  customerEmail?: string;
  deliveryAddress?: string;
  deliveryNotes?: string;
  items: StorefrontOrderItem[];
  paymentMethod: "UpiOnline" | "PayOnDelivery" | "PayAtCounter";
  idempotencyKey: string;
}

export interface StorefrontCheckoutResponse {
  orderId: string;
  orderNumber: string;
  token: string;
  status: "Placed" | "Preparing" | "Ready" | "Dispatched" | "Delivered";
  subtotal: number;
  tax: number;
  deliveryFee: number;
  packagingFee: number;
  totalPayable: number;
  paymentStatus: "Pending" | "Paid";
  upiUri?: string;
  payeeVpa?: string;
  reference?: string;
  estimatedFulfillmentUtc: string;
  isReplay: boolean;
  trackingToken: string;
}

export interface StorefrontTrackingItem {
  name: string;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
}

export interface StorefrontOrderTracking {
  orderNumber: string;
  token: string;
  status: string;
  channel: string;
  outletName: string;
  placedAtUtc: string;
  estimatedFulfillmentUtc: string;
  items: StorefrontTrackingItem[];
  subtotal: number;
  tax: number;
  deliveryFee: number;
  packagingFee: number;
  totalPayable: number;
  paymentStatus: string;
  upiUri?: string;
  payeeVpa?: string;
}

export const storefrontApi = {
  getOutlet: (outletId: string): Promise<StorefrontOutletSummary> =>
    apiClient<StorefrontOutletSummary>(`/storefront/outlets/${outletId}`, { method: "GET" }),

  getOutlets: (restaurantId?: string): Promise<StorefrontOutletSummary[]> =>
    apiClient<StorefrontOutletSummary[]>(
      restaurantId ? `/storefront/outlets?restaurantId=${restaurantId}` : `/storefront/outlets`,
      { method: "GET" }
    ),

  getMenu: (outletId: string): Promise<any[]> =>
    apiClient<any[]>(`/storefront/outlets/${outletId}/menu`, { method: "GET" }),

  checkout: (params: StorefrontCheckoutRequest): Promise<StorefrontCheckoutResponse> =>
    apiClient<StorefrontCheckoutResponse>(`/storefront/checkout`, {
      method: "POST",
      body: JSON.stringify(params),
    }),

  getOrderStatus: (orderId: string, token?: string): Promise<StorefrontCheckoutResponse> =>
    apiClient<StorefrontCheckoutResponse>(
      token ? `/storefront/orders/${orderId}?token=${encodeURIComponent(token)}` : `/storefront/orders/${orderId}`,
      { method: "GET" }
    ),

  trackOrderByToken: (trackingToken: string): Promise<StorefrontOrderTracking> =>
    apiClient<StorefrontOrderTracking>(`/storefront/orders/track/${encodeURIComponent(trackingToken)}`, {
      method: "GET",
    }),

  updateOutletSettings: (outletId: string, settings: UpdateStorefrontOutletSettingsRequest): Promise<StorefrontOutletSummary> =>
    apiClient<StorefrontOutletSummary>(`/storefront/admin/outlets/${outletId}`, {
      method: "PUT",
      body: JSON.stringify(settings),
    }),
};
