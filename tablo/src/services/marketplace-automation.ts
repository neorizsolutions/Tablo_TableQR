import { apiClient } from "./apiClient";

export interface MarketplaceStore {
  id: string;
  restaurantId: string;
  provider: "Swiggy" | "Zomato";
  externalStoreId: string;
  storeStatus: "Online" | "Paused";
  autoAccept: boolean;
  lastSyncUtc?: string;
}

export interface MarketplaceItem {
  id: string;
  restaurantId: string;
  provider: "Swiggy" | "Zomato";
  externalItemId: string;
  menuItemId: string;
  menuItemName: string;
  isAvailable: boolean;
  customPrice?: number;
  packagingCharge: number;
}

export interface MarketplaceOrderEvent {
  eventId: string;
  provider: "Swiggy" | "Zomato";
  eventType: "ORDER_PLACED" | "ORDER_CANCELLED" | "STORE_STATUS";
  externalOrderId: string;
  externalStoreId: string;
  totalAmount: number;
  customerName?: string;
  customerPhone?: string;
  deliveryAddress?: string;
  items: {
    externalItemId: string;
    name: string;
    quantity: number;
    price: number;
    cookingNotes?: string;
  }[];
}

export interface WebhookResult {
  status: "Processed" | "Duplicate" | "Failed";
  message: string;
  orderId?: string;
  orderNumber?: number;
  tokenNumber?: number;
  generatedKotTicketIds?: string[];
}

export interface OutboundUpdate {
  id: string;
  orderId: string;
  provider: "Swiggy" | "Zomato";
  externalOrderId: string;
  targetStatus: "CONFIRMED" | "PREPARING" | "READY_FOR_PICKUP" | "DISPATCHED";
  attempts: number;
  status: "Pending" | "Success" | "Failed";
  lastError?: string;
  lastAttemptUtc?: string;
}

export const marketplaceAutomationApi = {
  getStores: (restaurantId: string) =>
    apiClient<MarketplaceStore[]>(`/restaurants/${restaurantId}/marketplace/stores`),

  toggleStoreStatus: (restaurantId: string, provider: "Swiggy" | "Zomato", status: "Online" | "Paused") =>
    apiClient<MarketplaceStore>(`/restaurants/${restaurantId}/marketplace/stores/${provider}/toggle-status`, {
      method: "POST",
      body: JSON.stringify({ status }),
    }),

  toggleAutoAccept: (restaurantId: string, provider: "Swiggy" | "Zomato", autoAccept: boolean) =>
    apiClient<MarketplaceStore>(`/restaurants/${restaurantId}/marketplace/stores/${provider}/auto-accept`, {
      method: "POST",
      body: JSON.stringify({ autoAccept }),
    }),

  getItems: (restaurantId: string) =>
    apiClient<MarketplaceItem[]>(`/restaurants/${restaurantId}/marketplace/items`),

  toggleItemAvailability: (restaurantId: string, itemId: string, isAvailable: boolean) =>
    apiClient<MarketplaceItem>(`/restaurants/${restaurantId}/marketplace/items/${itemId}/toggle-availability`, {
      method: "POST",
      body: JSON.stringify({ isAvailable }),
    }),

  simulateInboundOrder: (restaurantId: string, payload: MarketplaceOrderEvent) =>
    apiClient<WebhookResult>(`/restaurants/${restaurantId}/marketplace/webhook/${payload.provider.toLowerCase()}`, {
      method: "POST",
      body: JSON.stringify(payload),
    }),

  getOutboundUpdates: (restaurantId: string) =>
    apiClient<OutboundUpdate[]>(`/restaurants/${restaurantId}/marketplace/outbound-updates`),

  retryOutboundUpdate: (restaurantId: string, updateId: string) =>
    apiClient<{ success: boolean; message: string }>(`/restaurants/${restaurantId}/marketplace/outbound-updates/${updateId}/retry`, {
      method: "POST",
    }),
};
