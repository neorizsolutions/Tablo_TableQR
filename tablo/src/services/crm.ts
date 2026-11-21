import { apiClient } from "./apiClient";

export interface CustomerProfile {
  id: string;
  restaurantId: string;
  phone: string;
  name: string;
  email?: string;
  notes?: string;
  tags: string[];
  loyaltyPointsBalance: number;
  totalSpend: number;
  totalVisits: number;
  firstVisitUtc: string;
  lastVisitUtc: string;
  consentGiven: boolean;
}

export interface LoyaltyLedgerEntry {
  id: string;
  customerId: string;
  billId?: string;
  orderId?: string;
  transactionType: "Earn" | "Redeem" | "Reversal" | "Adjustment";
  points: number;
  balanceAfter: number;
  eligibleSalesAmount: number;
  idempotencyKey: string;
  description: string;
  createdAtUtc: string;
}

export interface CustomerFeedback {
  id: string;
  restaurantId: string;
  billId?: string;
  orderId?: string;
  customerId?: string;
  customerName?: string;
  customerPhone?: string;
  channel?: string;
  tableNumber?: number;
  overallRating: number;
  foodRating: number;
  serviceRating: number;
  ambianceRating: number;
  comments?: string;
  status: "New" | "Reviewed" | "Resolved";
  staffNotes?: string;
  createdAtUtc: string;
}

export const crmApi = {
  getCustomers: (restaurantId: string, search?: string) =>
    apiClient<CustomerProfile[]>(`/restaurants/${restaurantId}/crm/customers${search ? `?search=${encodeURIComponent(search)}` : ""}`),

  getCustomerById: (restaurantId: string, customerId: string) =>
    apiClient<{ customer: CustomerProfile; ledger: LoyaltyLedgerEntry[] }>(`/restaurants/${restaurantId}/crm/customers/${customerId}`),

  createCustomer: (restaurantId: string, data: Partial<CustomerProfile>) =>
    apiClient<CustomerProfile>(`/restaurants/${restaurantId}/crm/customers`, {
      method: "POST",
      body: JSON.stringify(data),
    }),

  updateCustomer: (restaurantId: string, customerId: string, data: Partial<CustomerProfile>) =>
    apiClient<CustomerProfile>(`/restaurants/${restaurantId}/crm/customers/${customerId}`, {
      method: "PUT",
      body: JSON.stringify(data),
    }),

  earnPoints: (restaurantId: string, data: { customerId: string; billId: string; eligibleAmount: number; idempotencyKey?: string }) =>
    apiClient<{ pointsEarned: number; newBalance: number; entry: LoyaltyLedgerEntry }>(`/restaurants/${restaurantId}/crm/loyalty/earn`, {
      method: "POST",
      body: JSON.stringify(data),
    }),

  redeemPoints: (restaurantId: string, data: { customerId: string; billId: string; pointsToRedeem: number; idempotencyKey?: string }) =>
    apiClient<{ pointsRedeemed: number; discountAmountInr: number; newBalance: number; entry: LoyaltyLedgerEntry }>(`/restaurants/${restaurantId}/crm/loyalty/redeem`, {
      method: "POST",
      body: JSON.stringify(data),
    }),

  getFeedback: (restaurantId: string) =>
    apiClient<CustomerFeedback[]>(`/restaurants/${restaurantId}/crm/feedback`),

  submitFeedback: (restaurantId: string, data: Partial<CustomerFeedback>) =>
    apiClient<CustomerFeedback>(`/restaurants/${restaurantId}/crm/feedback`, {
      method: "POST",
      body: JSON.stringify(data),
    }),

  updateFeedbackStatus: (restaurantId: string, feedbackId: string, status: string, staffNotes?: string) =>
    apiClient<CustomerFeedback>(`/restaurants/${restaurantId}/crm/feedback/${feedbackId}`, {
      method: "PATCH",
      body: JSON.stringify({ status, staffNotes }),
    }),
};
