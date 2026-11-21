import { apiClient } from './apiClient';

export interface BillPaymentDto {
  id: string;
  method: string;
  amount: number;
  collectedAtUtc: string;
  actorEmployeeId?: string;
}

export interface BillDto {
  id: string;
  totalAmount: number;
  subtotal: number;
  tax: number;
  discount: number;
  packagingCharge?: number;
  leftoverPackingCharge?: number;
  vehiclePlate?: string;
  parkingBay?: string;
  paidAmount: number;
  remainingAmount: number;
  status: string;
  paymentMethod?: string;
  revision: string;
  canSettle: boolean;
  payments?: BillPaymentDto[];
  receiptJson?: string;
}

export interface OrderItemRequest {
  menuItemId: string;
  quantity: number;
  specialInstructions?: string;
  packingIntent?: 'DineIn' | 'Takeaway';
}

export interface CreateParcelOrderRequest {
  operationId: string;
  customerName?: string;
  customerPhone?: string;
  orderChannel?: 'ParcelTakeaway' | 'CarParking' | 'Delivery';
  deliveryProvider?: 'Swiggy' | 'Zomato' | 'Other' | string;
  externalOrderReference?: string;
  marketplaceCollected?: boolean;
  marketplaceEnteredTotal?: number;
  marketplaceEnteredTax?: number;
  marketplaceEnteredDiscount?: number;
  marketplaceEnteredPackaging?: number;
  orderSource?: 'Counter' | 'Phone' | 'WhatsApp' | 'CarSide' | 'MarketplaceApp';
  isScheduled?: boolean;
  scheduledPickupTimeLocal?: string;
  preparationLeadMinutes?: number;
  vehiclePlate?: string;
  vehicleModel?: string;
  parkingBay?: string;
  waivePackaging: boolean;
  packagingWaiverReason?: string;
  items: OrderItemRequest[];
  notes?: string;
}

export interface ParcelOrderItemDto {
  id: string;
  menuItemId: string;
  name: string;
  quantity: number;
  unitPrice: number;
  packingIntent?: 'DineIn' | 'Takeaway';
}

export interface ParcelOrderResponseDto {
  orderId: string;
  billId: string;
  orderNumber: number;
  dailyTokenNumber: number;
  businessDate: string;
  tokenFormatted: string;
  orderChannel: string;
  deliveryProvider?: string;
  externalOrderReference?: string;
  marketplaceCollected?: boolean;
  marketplaceEnteredTotal?: number;
  marketplaceEnteredTax?: number;
  marketplaceEnteredDiscount?: number;
  marketplaceEnteredPackaging?: number;
  status: string;
  customerName?: string;
  customerPhone?: string;
  subtotal: number;
  packagingCharge: number;
  packagingChargeMode?: string;
  packagingRate: number;
  isPackagingWaived: boolean;
  packagingWaiverReason?: string;
  tax: number;
  discount: number;
  totalAmount: number;
  createdAtUtc: string;
  createdAtIstFormatted: string;
  kotPrintStatus: string;
  kotPrintError?: string;
  orderSource?: string;
  isScheduled?: boolean;
  scheduledPickupTimeUtc?: string;
  scheduledPickupTimeLocal?: string;
  scheduledReleaseTimeUtc?: string;
  preparationLeadMinutes?: number;
  vehiclePlate?: string;
  vehicleModel?: string;
  parkingBay?: string;
  deliveredAtUtc?: string;
  deliveredByEmployeeId?: string;
  items: ParcelOrderItemDto[];
  bill: BillDto;
}

export interface SettleParcelBillRequest {
  operationId: string;
  expectedRevision: string;
  expectedTotal: number;
  paymentMethod: 'Cash' | 'UPI' | 'Card' | 'MarketplaceCollected' | 'Marketplace';
}

export interface RecordParcelPaymentRequest {
  operationId: string;
  method: 'Cash' | 'UPI' | 'Card' | 'MarketplaceCollected' | 'Marketplace';
  amount: number;
  expectedRevision?: string;
  expectedRemaining?: number;
}

export interface PaymentAcknowledgementDto {
  paymentId: string;
  billId: string;
  amount: number;
  method: string;
  remaining: number;
  collectedAtUtc: string;
}

export interface ParcelSettingsDto {
  chargeMode: 'None' | 'PerItem' | 'FlatPerOrder' | 'Percentage';
  flatRate: number;
  percentage: number;
  isTaxable: boolean;
  taxRate?: number;
}

export interface UpdateParcelSettingsRequest {
  chargeMode: 'None' | 'PerItem' | 'FlatPerOrder' | 'Percentage';
  flatRate: number;
  percentage: number;
  isTaxable: boolean;
  taxRate?: number;
}

export interface RescheduleOrderRequest {
  operationId: string;
  newPickupTimeLocal: string;
  preparationLeadMinutes?: number;
}

export interface DeliverCarOrderRequest {
  operationId: string;
}

export interface AddLeftoverPackingRequest {
  operationId: string;
  containerCharge?: number;
  containerCount: number;
  notes?: string;
}

export interface UpiQrResponseDto {
  billId: string;
  remainingBalance: number;
  amount: number;
  upiUri: string;
  payeeVpa: string;
  payeeName: string;
  reference: string;
  note?: string;
  isSettled: boolean;
}

export const parcelApi = {
  createOrder: (restaurantId: string, data: CreateParcelOrderRequest) =>
    apiClient<ParcelOrderResponseDto>(`/restaurants/${restaurantId}/parcel/orders`, {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  searchOrders: (restaurantId: string, params?: { businessDate?: string; token?: number; status?: string; customer?: string }) => {
    const q = new URLSearchParams();
    if (params?.businessDate) q.set('businessDate', params.businessDate);
    if (params?.token) q.set('token', params.token.toString());
    if (params?.status) q.set('status', params.status);
    if (params?.customer) q.set('customer', params.customer);
    const qs = q.toString();
    return apiClient<ParcelOrderResponseDto[]>(`/restaurants/${restaurantId}/parcel/orders${qs ? `?${qs}` : ''}`);
  },

  getOrder: (restaurantId: string, orderId: string) =>
    apiClient<ParcelOrderResponseDto>(`/restaurants/${restaurantId}/parcel/orders/${orderId}`),

  settleOrder: (restaurantId: string, orderId: string, data: SettleParcelBillRequest) =>
    apiClient<BillDto>(`/restaurants/${restaurantId}/parcel/orders/${orderId}/settle`, {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  recordPayment: (restaurantId: string, orderId: string, data: RecordParcelPaymentRequest) =>
    apiClient<PaymentAcknowledgementDto>(`/restaurants/${restaurantId}/parcel/orders/${orderId}/payments`, {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  releaseScheduledOrderNow: (restaurantId: string, orderId: string) =>
    apiClient<ParcelOrderResponseDto>(`/restaurants/${restaurantId}/parcel/orders/${orderId}/release-now`, {
      method: 'POST',
    }),

  rescheduleOrder: (restaurantId: string, orderId: string, data: RescheduleOrderRequest) =>
    apiClient<ParcelOrderResponseDto>(`/restaurants/${restaurantId}/parcel/orders/${orderId}/reschedule`, {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  deliverCarOrder: (restaurantId: string, orderId: string, data: DeliverCarOrderRequest) =>
    apiClient<ParcelOrderResponseDto>(`/restaurants/${restaurantId}/parcel/orders/${orderId}/deliver`, {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  addLeftoverPacking: (restaurantId: string, billId: string, data: AddLeftoverPackingRequest) =>
    apiClient<BillDto>(`/restaurants/${restaurantId}/parcel/bills/${billId}/leftover-packing`, {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  getBillUpiQr: (restaurantId: string, billId: string, amount?: number) => {
    const qs = amount ? `?amount=${amount}` : '';
    return apiClient<UpiQrResponseDto>(`/restaurants/${restaurantId}/parcel/bills/${billId}/upi-qr${qs}`);
  },

  cancelOrder: (restaurantId: string, orderId: string, reason: string) =>
    apiClient<ParcelOrderResponseDto>(`/restaurants/${restaurantId}/parcel/orders/${orderId}/cancel`, {
      method: 'POST',
      body: JSON.stringify({ reason }),
    }),

  printReceipt: (restaurantId: string, orderId: string, reason?: string) =>
    apiClient<{ id: string; readableText: string }>(`/restaurants/${restaurantId}/parcel/orders/${orderId}/print-receipt`, {
      method: 'POST',
      body: JSON.stringify({ reason: reason || 'Customer requested printed receipt' }),
    }),

  retryKot: (restaurantId: string, orderId: string) =>
    apiClient<{ message: string }>(`/restaurants/${restaurantId}/parcel/orders/${orderId}/retry-kot`, {
      method: 'POST',
    }),

  getSettings: (restaurantId: string) =>
    apiClient<ParcelSettingsDto>(`/restaurants/${restaurantId}/parcel/settings`),

  updateSettings: (restaurantId: string, data: UpdateParcelSettingsRequest) =>
    apiClient<ParcelSettingsDto>(`/restaurants/${restaurantId}/parcel/settings`, {
      method: 'PUT',
      body: JSON.stringify(data),
    }),

  recordPayout: (restaurantId: string, data: RecordMarketplacePayoutRequest) =>
    apiClient<MarketplacePayoutDto>(`/restaurants/${restaurantId}/parcel/marketplace-payouts`, {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  getPayouts: (restaurantId: string, deliveryProvider?: string) => {
    const qs = deliveryProvider ? `?deliveryProvider=${encodeURIComponent(deliveryProvider)}` : '';
    return apiClient<MarketplacePayoutDto[]>(`/restaurants/${restaurantId}/parcel/marketplace-payouts${qs}`);
  },

  getTransferShortageDryRun: (restaurantId: string) =>
    apiClient<TransferDiscrepancyReportDto>(`/restaurants/${restaurantId}/inventory/transfers/shortage-dry-run`),

  applyTransferShortageCorrection: (restaurantId: string) =>
    apiClient<ApplyTransferCorrectionResponseDto>(`/restaurants/${restaurantId}/inventory/transfers/shortage-correction`, {
      method: 'POST',
    }),
};

export interface RecordMarketplacePayoutRequest {
  payoutReference: string;
  deliveryProvider: string;
  payoutDateUtc?: string;
  grossAmount: number;
  commissionDeducted: number;
  tcsGstDeducted: number;
  otherAdjustments?: number;
  netAmount: number;
  linkedOrderIds?: string[];
  notes?: string;
}

export interface MarketplacePayoutDto {
  id: string;
  restaurantId: string;
  outletId?: string;
  deliveryProvider: string;
  payoutReference: string;
  payoutDateUtc: string;
  grossAmount: number;
  commissionDeducted: number;
  tcsGstDeducted: number;
  otherAdjustments: number;
  netAmount: number;
  linkedOrderIds: string[];
  notes?: string;
  createdAtUtc: string;
}

export interface TransferDiscrepancyDryRunDto {
  transferId: string;
  transferNumber: string;
  sourceOutletName: string;
  destinationOutletName: string;
  receivedAtUtc: string;
  stockItemId: string;
  stockItemName: string;
  unit: string;
  dispatchedQuantity: number;
  receivedQuantity: number;
  shortageQuantity: number;
  unitCostSnapshot: number;
  excessLossValuation: number;
  isCorrected: boolean;
}

export interface TransferDiscrepancyReportDto {
  restaurantId: string;
  totalAffectedTransfers: number;
  uncorrectedCount: number;
  totalShortageUnits: number;
  totalExcessLossValuation: number;
  items: TransferDiscrepancyDryRunDto[];
}

export interface ApplyTransferCorrectionResponseDto {
  restaurantId: string;
  correctedCount: number;
  skippedAlreadyCorrectedCount: number;
  totalUnitsRestored: number;
  totalValuationRestored: number;
  message: string;
}
