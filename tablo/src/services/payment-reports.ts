import { apiClient, apiDownloadBlob } from "./apiClient";
import type { TableBill } from "./service-desk";

export interface PaymentFilters {
  startDate?: string;
  endDate?: string;
  tableId?: string;
  paymentMethod?: string;
  page?: number;
  pageSize?: number;
}
export interface PaymentRow {
  billId: string;
  groupLabel?: string;
  id: string;
  tableId: string;
  tableNumber: number;
  settledAtUtc: string;
  settledByEmployeeId: string | null;
  paymentMethod: string;
  totalAmount: number;
}
export interface PaymentReport {
  settledBillCount:number;
  outstandingBalance:number;
  timeZone: string;
  totalCollected: number;
  cash: number;
  upi: number;
  card: number;
  totalCount: number;
  totalPages: number;
  page: number;
  pageSize: number;
  items: PaymentRow[];
}
const root = (id: string) => `/restaurants/${id}/payment-reports`;
const query = (filters: PaymentFilters) =>
  new URLSearchParams(
    Object.entries(filters)
      .filter(([, v]) => v !== undefined && v !== "")
      .map(([k, v]) => [k, String(v)]),
  ).toString();
export const getPaymentReport = (id: string, filters: PaymentFilters) =>
  apiClient<PaymentReport>(`${root(id)}?${query(filters)}`);
export const getPaymentReceipt = (id: string, bill: string) =>
  apiClient<TableBill>(`${root(id)}/${bill}/receipt`);
export async function exportPayments(id: string, filters: PaymentFilters) {
  const blob = await apiDownloadBlob(`${root(id)}/export?${query(filters)}`);
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = "Tablo_Payments_IST.xlsx";
  document.body.appendChild(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
