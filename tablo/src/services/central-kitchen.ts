import { apiClient } from "./apiClient.ts";

export interface CentralProductionBatch {
  id: string;
  restaurantId: string;
  batchCode: string;
  recipeName: string;
  targetQuantity: number;
  unit: string;
  unitProductionCost: number;
  totalCost: number;
  status: "Draft" | "InProduction" | "Completed";
  createdAtUtc: string;
}

export interface OutletStockTransfer {
  transferId: string;
  transferCode: string;
  sourceKitchenId: string;
  destinationOutletId: string;
  itemName: string;
  quantityDispatched: number;
  quantityReceived: number;
  shortageQuantity: number;
  dispatchedCost: number;
  receivedCost: number;
  varianceLossCost: number;
  status: "Dispatched" | "PartiallyReceived" | "FullyReceived";
  discrepancyReason?: string;
  dispatchedAtUtc: string;
  receivedAtUtc?: string;
}

export interface MenuPublishItemOverride {
  menuItemId: string;
  itemName: string;
  centralPrice: number;
  outletPriceOverride?: number;
  isAvailableOverride?: boolean;
}

export interface MenuPublishPreview {
  centralRestaurantId: string;
  targetOutletIds: string[];
  itemsToAddCount: number;
  itemsToUpdateCount: number;
  overridesPreview: MenuPublishItemOverride[];
}

export interface MenuPublishResult {
  publishJobId: string;
  publishedAtUtc: string;
  outletsUpdatedCount: number;
  itemsUpdatedCount: number;
  success: boolean;
}

export const centralKitchenApi = {
  createProductionBatch: (params: {
    restaurantId: string;
    recipeName: string;
    targetQuantity: number;
    unit: string;
    unitProductionCost: number;
  }): Promise<CentralProductionBatch> =>
    apiClient<CentralProductionBatch>(`/central-kitchen/${params.restaurantId}/batches`, {
      method: "POST",
      body: JSON.stringify(params),
    }),

  getProductionBatches: (restaurantId: string): Promise<CentralProductionBatch[]> =>
    apiClient<CentralProductionBatch[]>(`/central-kitchen/${restaurantId}/batches`, {
      method: "GET",
    }),

  dispatchOutletTransfer: (params: {
    sourceKitchenId: string;
    destinationOutletId: string;
    ingredientId: string;
    itemName: string;
    quantityDispatched: number;
    unit: string;
    unitCost: number;
  }): Promise<OutletStockTransfer> =>
    apiClient<OutletStockTransfer>(`/central-kitchen/${params.sourceKitchenId}/transfers/dispatch`, {
      method: "POST",
      body: JSON.stringify(params),
    }),

  receiveOutletTransfer: (params: {
    transferId: string;
    quantityReceived: number;
    discrepancyReason?: string;
  }): Promise<OutletStockTransfer> =>
    apiClient<OutletStockTransfer>(`/central-kitchen/transfers/${params.transferId}/receive`, {
      method: "POST",
      body: JSON.stringify(params),
    }),

  getTransfers: (outletId: string): Promise<OutletStockTransfer[]> =>
    apiClient<OutletStockTransfer[]>(`/central-kitchen/transfers?outletId=${outletId}`, {
      method: "GET",
    }),

  previewMenuPublish: (params: {
    centralRestaurantId: string;
    targetOutletIds: string[];
  }): Promise<MenuPublishPreview> =>
    apiClient<MenuPublishPreview>(`/central-kitchen/${params.centralRestaurantId}/menu-publish/preview`, {
      method: "POST",
      body: JSON.stringify(params),
    }),

  executeMenuPublish: (params: {
    centralRestaurantId: string;
    targetOutletIds: string[];
    itemOverrides: MenuPublishItemOverride[];
    forceAvailabilitySync?: boolean;
  }): Promise<MenuPublishResult> =>
    apiClient<MenuPublishResult>(`/central-kitchen/${params.centralRestaurantId}/menu-publish/execute`, {
      method: "POST",
      body: JSON.stringify(params),
    }),
};
