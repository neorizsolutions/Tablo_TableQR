import { apiClient } from "./apiClient.ts";

export type SupportConversationStatus = "AwaitingSupport" | "AwaitingRestaurant" | "Resolved";
export type SupportSenderType = "Restaurant" | "SuperAdmin";
export type SupportConnectionState = "connecting" | "connected" | "reconnecting" | "offline" | "revoked";

export interface SupportMessageDto {
  id: string;
  conversationId: string;
  senderType: SupportSenderType;
  senderEmployeeId?: string | null;
  senderName: string;
  senderEmail: string;
  message: string;
  createdAtUtc: string;
  sequenceNumber: number;
  clientOperationId?: string | null;
  localStatus?: "sending" | "failed";
  localError?: string;
}

export interface SupportEscalationLogDto {
  id: string;
  waitingPeriodId: string;
  escalationTargetEmail: string;
  scheduledDeadlineUtc: string;
  dispatchedAtUtc?: string | null;
  attemptCount: number;
  lastAttemptAtUtc?: string | null;
  nextRetryAtUtc?: string | null;
  status: string;
  lastError?: string | null;
  providerMessageId?: string | null;
}

export interface SupportAuditLogDto {
  id: string;
  actorType: string;
  actorName: string;
  action: string;
  details?: string | null;
  timestampUtc: string;
}

export interface SupportConversationSummaryDto {
  id: string;
  restaurantId: string;
  restaurantName: string;
  subject: string;
  status: SupportConversationStatus;
  createdAtUtc: string;
  updatedAtUtc: string;
  lastActivityAtUtc: string;
  resolvedAtUtc?: string | null;
  resolvedBy?: string | null;
  awaitingSupportSinceUtc?: string | null;
  currentWaitingPeriodId?: string | null;
  escalationDeadlineUtc?: string | null;
  isEscalated: boolean;
  escalatedAtUtc?: string | null;
  restaurantUnreadCount: number;
  superAdminUnreadCount: number;
  lastMessagePreview?: string | null;
  lastMessageSenderType?: SupportSenderType | null;
  lastMessageId?: string | null;
  version: number;
  waitingDurationSeconds?: number | null;
}

export interface SupportConversationDetailDto {
  conversation: SupportConversationSummaryDto;
  messages: SupportMessageDto[];
  escalationLogs: SupportEscalationLogDto[];
  auditLogs: SupportAuditLogDto[];
}

export interface SupportSyncResponseDto {
  serverUtc: string;
  conversations: SupportConversationSummaryDto[];
  totalUnreadCount: number;
}

export interface SupportConversationMutationResultDto {
  conversation: SupportConversationSummaryDto;
  message?: SupportMessageDto | null;
  deduplicated: boolean;
}

export interface SupportRealtimeEventDto {
  eventId: string;
  eventType:
    | "ConversationCreated"
    | "MessageCreated"
    | "ConversationResolved"
    | "ConversationReopened"
    | "ConversationRead"
    | "ConversationEscalated"
    | string;
  occurredAtUtc: string;
  conversationId: string;
  restaurantId: string;
  conversationVersion: number;
  conversation?: SupportConversationSummaryDto | null;
  message?: SupportMessageDto | null;
  escalation?: SupportEscalationLogDto | null;
  readByRole?: SupportSenderType | null;
}

export function getRestaurantSupportConversations(sinceUtc?: string) {
  const qs = sinceUtc ? `?sinceUtc=${encodeURIComponent(sinceUtc)}` : "";
  return apiClient<SupportSyncResponseDto>(`/support/conversations${qs}`);
}

export function getRestaurantSupportConversation(id: string, afterSequence?: number) {
  const qs = typeof afterSequence === "number" ? `?afterSequence=${afterSequence}` : "";
  return apiClient<SupportConversationDetailDto>(`/support/conversations/${id}${qs}`);
}

export function createSupportConversation(payload: {
  subject: string;
  message: string;
  clientOperationId: string;
}) {
  return apiClient<SupportConversationMutationResultDto>("/support/conversations", {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

export function replySupportConversation(
  id: string,
  payload: { message: string; clientOperationId: string },
) {
  return apiClient<SupportConversationMutationResultDto>(`/support/conversations/${id}/messages`, {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

export function reopenSupportConversation(
  id: string,
  payload: { message?: string; clientOperationId?: string },
) {
  return apiClient<SupportConversationMutationResultDto>(`/support/conversations/${id}/reopen`, {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

export function resolveSupportConversation(id: string, note?: string) {
  return apiClient<SupportConversationSummaryDto>(`/support/conversations/${id}/resolve`, {
    method: "POST",
    body: JSON.stringify({ note }),
  });
}

export function markRestaurantSupportRead(id: string) {
  return apiClient<SupportConversationSummaryDto>(`/support/conversations/${id}/read`, {
    method: "POST",
  });
}

export function getSuperAdminSupportInbox(params?: {
  status?: string;
  search?: string;
  escalatedOnly?: boolean;
  sinceUtc?: string;
}) {
  const searchParams = new URLSearchParams();
  if (params?.status) searchParams.set("status", params.status);
  if (params?.search) searchParams.set("search", params.search);
  if (params?.escalatedOnly) searchParams.set("escalatedOnly", "true");
  if (params?.sinceUtc) searchParams.set("sinceUtc", params.sinceUtc);
  const qs = searchParams.toString();
  return apiClient<SupportSyncResponseDto>(`/sysadmin/support/conversations${qs ? `?${qs}` : ""}`);
}

export function getSuperAdminSupportConversation(id: string, afterSequence?: number) {
  const qs = typeof afterSequence === "number" ? `?afterSequence=${afterSequence}` : "";
  return apiClient<SupportConversationDetailDto>(`/sysadmin/support/conversations/${id}${qs}`);
}

export function replySuperAdminSupportConversation(
  id: string,
  payload: { message: string; clientOperationId: string },
) {
  return apiClient<SupportConversationMutationResultDto>(
    `/sysadmin/support/conversations/${id}/messages`,
    {
      method: "POST",
      body: JSON.stringify(payload),
    },
  );
}

export function resolveSuperAdminSupportConversation(id: string, note?: string) {
  return apiClient<SupportConversationSummaryDto>(
    `/sysadmin/support/conversations/${id}/resolve`,
    {
      method: "POST",
      body: JSON.stringify({ note }),
    },
  );
}

export function markSuperAdminSupportRead(id: string) {
  return apiClient<SupportConversationSummaryDto>(
    `/sysadmin/support/conversations/${id}/read`,
    {
      method: "POST",
    },
  );
}
