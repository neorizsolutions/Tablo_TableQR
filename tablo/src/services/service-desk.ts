import { apiClient, API_BASE_URL } from "./apiClient";

export interface ServiceTable {
  id: string;
  tableNumber: number;
  isActive: boolean;
  status?: "Available" | "Occupied" | "Bill Requested";
  activeOrdersCount?: number;
  activeDiningGroups?: DiningGroup[];
  openBillTotal?: number;
}
export interface BillItem {
  id: string;
  name: string;
  quantity: number;
  unitPrice: number;
  releaseState?: string;
  courseName?: string;
  isRefill?: boolean;
  cancelledQuantity?: number;
  isCancelled?: boolean;
  packingIntent?: "DineIn" | "Takeaway";
}
export interface BillOrder {
  id: string;
  orderNumber: number;
  status: string;
  totalAmount: number;
  items: BillItem[];
}
export interface TableBill {
  diningGroupId?: string | null;
  groupLabel?: string | null;
  parentBillId?: string | null;
  shareDescription?: string | null;
  paidAmount: number;
  remainingAmount: number;
  payments?: {
    id: string;
    method: string;
    amount: number;
    collectedAtUtc: string;
    actorEmployeeId: string;
  }[];
  restaurantName?: string | null;
  settledByEmployeeId?: string | null;
  id: string;
  tableNumber: number;
  status: "Open" | "Settled";
  revision: string;
  createdAtUtc: string;
  settledAtUtc: string | null;
  paymentMethod: string | null;
  subtotal: number;
  packagingCharge?: number;
  leftoverPackingCharge?: number;
  vehiclePlate?: string;
  parkingBay?: string;
  tax: number;
  discount: number;
  totalAmount: number;
  canSettle: boolean;
  orders: BillOrder[];
}
export interface BillReview {
  tableId: string;
  tableNumber: number;
  openBills: TableBill[];
  openBill: TableBill | null;
  unbilledActiveOrderCount: number;
  recentSettlements: TableBill[];
}
export interface StaffOrderRequest {
  diningGroupId?: string;
  operationId: string;
  tableId: string;
  items: {
    menuItemId: string;
    quantity: number;
    releaseState?: "Fired" | "Held";
    courseName?: string;
    packingIntent?: "DineIn" | "Takeaway";
    specialInstructions?: string;
    selectedModifiers?: { modifierGroupId: string; modifierOptionId: string }[];
  }[];
}
export interface SettlementRequest {
  operationId: string;
  expectedRevision: string;
  expectedTotal: number;
  paymentMethod: string;
}
export class ServiceDeskError extends Error {
  status: number;
  constructor(
    message: string,
    status: number,
  ) {
    super(message);
    this.name = "ServiceDeskError";
    this.status = status;
  }
}

export async function serviceDeskRequest<T>(
  restaurantId: string,
  path: string,
  body?: unknown,
): Promise<T> {
  const method = body === undefined ? "GET" : "POST";
  const options: RequestInit = {
    method,
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  };
  try {
    return await apiClient<T>(`/restaurants/${restaurantId}/service-desk/${path}`, options);
  } catch (err: any) {
    throw new ServiceDeskError(
      err.message || `Request failed (${err.status || 500}).`,
      err.status || 500,
    );
  }
}

export const getServiceTables = (restaurantId: string) =>
  serviceDeskRequest<ServiceTable[]>(restaurantId, "tables");
export const getBillReview = (restaurantId: string, tableId: string, groupId?: string) =>
  serviceDeskRequest<BillReview>(
    restaurantId,
    `tables/${tableId}/bill${groupId ? `?diningGroupId=${groupId}` : ""}`,
  );
export const openTableBill = (restaurantId: string, tableId: string) =>
  serviceDeskRequest<TableBill>(restaurantId, `tables/${tableId}/bill/open`, {});
export const submitStaffOrder = (restaurantId: string, body: StaffOrderRequest) =>
  serviceDeskRequest<{ orderId: string; orderNumber: number }>(restaurantId, "orders", body);
export const settleTableBill = (restaurantId: string, billId: string, body: SettlementRequest) =>
  serviceDeskRequest<TableBill>(restaurantId, `bills/${billId}/settle`, body);

export interface DiningGroup {
  id: string;
  tableId: string;
  label: string;
  createdAtUtc: string;
}
export const getDiningGroups = (restaurantId: string, tableId: string) =>
  serviceDeskRequest<DiningGroup[]>(restaurantId, `tables/${tableId}/groups`);

export const createDiningGroup = (restaurantId: string, tableId: string, label: string) =>
  serviceDeskRequest<DiningGroup>(restaurantId, `tables/${tableId}/groups`, { label });

export interface BillSplitItemOption {
  billItemId: string;
  quantity: number;
}
export interface BillSplitRequest {
  splitMode: "Item" | "Amount";
  targetDiningGroupId?: string;
  items?: BillSplitItemOption[];
  splitAmounts?: number[]; // In exact INR, converted to paise
}
export interface BillSplitPreview {
  originalBillTotal: number;
  newBillTotal: number;
  unallocatedBalance: number;
  canSplit: boolean;
  message?: string;
}
export const getBillSplitPreview = (restaurantId: string, billId: string, body: BillSplitRequest) =>
  serviceDeskRequest<BillSplitPreview>(restaurantId, `bills/${billId}/split-preview`, body);

export const splitTableBill = (restaurantId: string, billId: string, body: BillSplitRequest) =>
  serviceDeskRequest<{ originalBill: TableBill; newBills: TableBill[] }>(
    restaurantId,
    `bills/${billId}/split`,
    body,
  );

export const transferTableVisit = (
  restaurantId: string,
  body: {
    sourceTableId: string;
    targetTableId: string;
    sourceDiningGroupId?: string;
    targetDiningGroupId?: string;
    reason?: string;
  },
) => serviceDeskRequest<{ success: boolean; message: string }>(restaurantId, "transfer", body);
