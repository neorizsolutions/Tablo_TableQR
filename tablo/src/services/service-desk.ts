import { API_BASE_URL } from "./apiClient";

export interface ServiceTable {
  id: string;
  tableNumber: number;
  isActive: boolean;
}
export interface BillItem {
  id: string;
  name: string;
  quantity: number;
  unitPrice: number;
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
  items: { menuItemId: string; quantity: number }[];
}
export interface SettlementRequest {
  operationId: string;
  expectedRevision: string;
  expectedTotal: number;
  paymentMethod: string;
}
export class ServiceDeskError extends Error {
  constructor(
    message: string,
    public status: number,
  ) {
    super(message);
  }
}

export async function serviceDeskRequest<T>(
  restaurantId: string,
  path: string,
  body?: unknown,
): Promise<T> {
  const response = await fetch(`${API_BASE_URL}/restaurants/${restaurantId}/service-desk/${path}`, {
    method: body === undefined ? "GET" : "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${localStorage.getItem("tablo_token") || ""}`,
    },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  });
  if (!response.ok) {
    const data = await response.json().catch(() => null);
    throw new ServiceDeskError(
      data?.message || `Request failed (${response.status}).`,
      response.status,
    );
  }
  return response.json() as Promise<T>;
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
