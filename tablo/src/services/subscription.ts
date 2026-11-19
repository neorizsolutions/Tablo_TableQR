import { apiClient } from "./apiClient";

export interface OutletSummary {
  id: string;
  name: string;
  isMain?: boolean;
}

export interface PlanSummary {
  code: string;
  name: string;
  description?: string;
  status: string;
  startDateUtc: string;
  endDateUtc?: string;
  monthlyPriceInr: number;
  autoRenew: boolean;
}

export interface PlanLimits {
  maxOutlets: number;
  maxTables: number;
}

export interface EffectiveAccess {
  restaurantId: string;
  restaurantName: string;
  outletId?: string;
  role: string;
  employeeName: string;
  employeeEmail: string;
  plan: PlanSummary;
  capabilities: string[];
  limits: PlanLimits;
  outlets?: OutletSummary[];
}

export interface PlanItem {
  id: string;
  code: string;
  name: string;
  description: string;
  monthlyPriceInr: number;
  maxOutlets: number;
  maxTables: number;
  features: string[];
  isActive: boolean;
  displayOrder: number;
}

export interface SubscriptionItem {
  id: string;
  restaurantId: string;
  restaurantName: string;
  planId: string;
  planCode: string;
  planName: string;
  status: string;
  startDateUtc: string;
  endDateUtc: string;
  autoRenew: boolean;
  notes?: string;
  enabledFeatures?: string[];
}

export interface SubscriptionAuditLogItem {
  id: string;
  restaurantId: string;
  restaurantName: string;
  actorEmail: string;
  action: string;
  fromPlanCode?: string;
  toPlanCode?: string;
  fromStatus?: string;
  toStatus?: string;
  reason: string;
  timestampUtc: string;
  detailsJson?: string;
}

export interface SuperAdminOverview {
  totalRestaurants: number;
  activeRestaurants: number;
  disabledRestaurants: number;
  totalSubscriptions: number;
  activeSubscriptions: number;
  trialSubscriptions: number;
  suspendedSubscriptions: number;
  expiredSubscriptions: number;
  expiringSoonCount: number;
  totalEmployees: number;
  subscriptionsByPlan: Record<string, number>;
}

export interface PlanChangePreview {
  fromPlanCode: string;
  toPlanCode: string;
  capabilitiesGained: string[];
  capabilitiesLost: string[];
  unchangedCapabilities: string[];
}

// User / Restaurant Endpoints
export async function getEffectiveAccess(outletId?: string): Promise<EffectiveAccess> {
  const query = outletId ? `?outletId=${outletId}` : "";
  return apiClient<EffectiveAccess>(`/subscriptions/effective-access${query}`);
}

export async function getCurrentSubscription(): Promise<SubscriptionItem> {
  return apiClient<SubscriptionItem>("/subscriptions/current");
}

export async function getPublicPlans(): Promise<PlanItem[]> {
  return apiClient<PlanItem[]>("/subscriptions/plans");
}

export async function previewPlanChange(targetPlanCode: string): Promise<PlanChangePreview> {
  return apiClient<PlanChangePreview>(`/subscriptions/preview-change?targetPlanCode=${targetPlanCode}`);
}

// Super Admin Endpoints
export async function getSysAdminOverview(): Promise<SuperAdminOverview> {
  return apiClient<SuperAdminOverview>("/sysadmin/overview");
}

export async function getSysAdminPlans(): Promise<PlanItem[]> {
  return apiClient<PlanItem[]>("/sysadmin/plans");
}

export async function updatePlanFeatures(
  planId: string,
  features: string[],
  reason: string
): Promise<PlanItem> {
  return apiClient<PlanItem>(`/sysadmin/plans/${planId}`, {
    method: "PUT",
    body: JSON.stringify({ features, reason })
  });
}

export async function getSysAdminSubscriptions(): Promise<SubscriptionItem[]> {
  return apiClient<SubscriptionItem[]>("/sysadmin/subscriptions");
}

export async function changeSubscription(
  restaurantId: string,
  data: {
    targetPlanCode?: string;
    newStatus?: string;
    endDateUtc?: string;
    autoRenew?: boolean;
    reason: string;
    notes?: string;
  }
): Promise<SubscriptionItem> {
  return apiClient<SubscriptionItem>(`/sysadmin/subscriptions/${restaurantId}/change-plan`, {
    method: "POST",
    body: JSON.stringify(data)
  });
}

export async function previewSysAdminPlanChange(
  restaurantId: string,
  targetPlanCode: string
): Promise<PlanChangePreview> {
  return apiClient<PlanChangePreview>(`/sysadmin/subscriptions/${restaurantId}/preview?targetPlanCode=${targetPlanCode}`);
}

export async function getSysAdminAuditLogs(
  restaurantId?: string,
  limit: number = 100
): Promise<SubscriptionAuditLogItem[]> {
  const query = restaurantId ? `?restaurantId=${restaurantId}&limit=${limit}` : `?limit=${limit}`;
  return apiClient<SubscriptionAuditLogItem[]>(`/sysadmin/audit-logs${query}`);
}
