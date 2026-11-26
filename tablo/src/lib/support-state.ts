import type {
  SupportConversationDetailDto,
  SupportConversationSummaryDto,
  SupportMessageDto,
  SupportRealtimeEventDto,
  SupportSyncResponseDto,
} from "../services/support.ts";

export function sortConversations(
  items: SupportConversationSummaryDto[],
  mode: "restaurant" | "superadmin",
): SupportConversationSummaryDto[] {
  return [...items].sort((a, b) => {
    if (mode === "superadmin") {
      const aEscalated = a.isEscalated && a.status === "AwaitingSupport" ? 1 : 0;
      const bEscalated = b.isEscalated && b.status === "AwaitingSupport" ? 1 : 0;
      if (aEscalated !== bEscalated) return bEscalated - aEscalated;

      const aResolved = a.status === "Resolved" ? 1 : 0;
      const bResolved = b.status === "Resolved" ? 1 : 0;
      if (aResolved !== bResolved) return aResolved - bResolved;
    }

    return new Date(b.lastActivityAtUtc).getTime() - new Date(a.lastActivityAtUtc).getTime();
  });
}

export function upsertConversationSummary(
  list: SupportConversationSummaryDto[],
  incoming: SupportConversationSummaryDto,
  mode: "restaurant" | "superadmin",
): SupportConversationSummaryDto[] {
  const index = list.findIndex((item) => item.id === incoming.id);
  if (index === -1) {
    return sortConversations([incoming, ...list], mode);
  }

  const current = list[index];
  if (incoming.version < current.version) {
    return list;
  }

  const next = [...list];
  next[index] = incoming;
  return sortConversations(next, mode);
}

export function mergeSyncResponse(
  current: SupportSyncResponseDto | undefined,
  delta: SupportSyncResponseDto,
  mode: "restaurant" | "superadmin",
  isFullReplace: boolean,
): SupportSyncResponseDto {
  if (isFullReplace || !current) {
    const sorted = sortConversations(delta.conversations, mode);
    const totalUnread = sorted.reduce(
      (sum, c) => sum + (mode === "superadmin" ? c.superAdminUnreadCount : c.restaurantUnreadCount),
      0,
    );
    return {
      serverUtc: delta.serverUtc,
      conversations: sorted,
      totalUnreadCount: totalUnread,
    };
  }

  let updatedList = [...current.conversations];
  for (const conv of delta.conversations) {
    updatedList = upsertConversationSummary(updatedList, conv, mode);
  }
  const totalUnread = updatedList.reduce(
    (sum, c) => sum + (mode === "superadmin" ? c.superAdminUnreadCount : c.restaurantUnreadCount),
    0,
  );
  return {
    serverUtc: delta.serverUtc,
    conversations: updatedList,
    totalUnreadCount: totalUnread,
  };
}

export function mergeMessages(
  existing: SupportMessageDto[],
  incoming: SupportMessageDto[],
): SupportMessageDto[] {
  const byId = new Map<string, SupportMessageDto>();
  const opToConfirmedId = new Map<string, string>();

  // First index confirmed messages
  for (const msg of [...existing, ...incoming]) {
    if (!msg.localStatus) {
      const prev = byId.get(msg.id);
      if (!prev || msg.sequenceNumber >= prev.sequenceNumber) {
        byId.set(msg.id, { ...msg, localStatus: undefined, localError: undefined });
      }
      if (msg.clientOperationId) {
        opToConfirmedId.set(msg.clientOperationId.toLowerCase(), msg.id);
      }
    }
  }

  // Keep remaining optimistic messages only if NOT yet confirmed by server id or clientOperationId
  const result: SupportMessageDto[] = Array.from(byId.values());
  const seenOptimisticOps = new Set<string>();

  for (const msg of [...existing, ...incoming]) {
    if (!msg.localStatus) continue;
    const op = msg.clientOperationId?.toLowerCase();
    if (op && opToConfirmedId.has(op)) continue;
    if (byId.has(msg.id)) continue;
    if (op && seenOptimisticOps.has(op)) continue;
    if (op) seenOptimisticOps.add(op);
    result.push(msg);
  }

  return result.sort((a, b) => {
    const aPending = a.localStatus ? 1 : 0;
    const bPending = b.localStatus ? 1 : 0;
    if (aPending !== bPending) return aPending - bPending;
    if (a.sequenceNumber !== b.sequenceNumber) return a.sequenceNumber - b.sequenceNumber;
    return new Date(a.createdAtUtc).getTime() - new Date(b.createdAtUtc).getTime();
  });
}

export function applyRealtimeEventToList(
  current: SupportSyncResponseDto | undefined,
  event: SupportRealtimeEventDto,
  mode: "restaurant" | "superadmin",
): SupportSyncResponseDto | undefined {
  if (!event.conversation) return current;
  const base = current ?? {
    serverUtc: event.occurredAtUtc,
    conversations: [],
    totalUnreadCount: 0,
  };
  const nextList = upsertConversationSummary(base.conversations, event.conversation, mode);
  const totalUnread = nextList.reduce(
    (sum, c) => sum + (mode === "superadmin" ? c.superAdminUnreadCount : c.restaurantUnreadCount),
    0,
  );
  return {
    serverUtc:
      new Date(event.occurredAtUtc).getTime() > new Date(base.serverUtc).getTime()
        ? event.occurredAtUtc
        : base.serverUtc,
    conversations: nextList,
    totalUnreadCount: totalUnread,
  };
}

export function applyRealtimeEventToDetail(
  current: SupportConversationDetailDto | undefined,
  event: SupportRealtimeEventDto,
): SupportConversationDetailDto | undefined {
  if (!current || current.conversation.id !== event.conversationId) {
    return current;
  }

  const nextConv =
    event.conversation && event.conversation.version >= current.conversation.version
      ? event.conversation
      : current.conversation;

  const nextMessages = event.message
    ? mergeMessages(current.messages, [event.message])
    : current.messages;

  let nextEscalations = current.escalationLogs;
  if (event.escalation) {
    const exists = current.escalationLogs.some((e) => e.id === event.escalation!.id);
    nextEscalations = exists
      ? current.escalationLogs.map((e) => (e.id === event.escalation!.id ? event.escalation! : e))
      : [event.escalation, ...current.escalationLogs];
  }

  return {
    ...current,
    conversation: nextConv,
    messages: nextMessages,
    escalationLogs: nextEscalations,
  };
}

export function formatWaitingDuration(sinceUtc?: string | null, nowMs: number = Date.now()): string {
  if (!sinceUtc) return "";
  const diffSec = Math.max(0, Math.floor((nowMs - new Date(sinceUtc).getTime()) / 1000));
  const hours = Math.floor(diffSec / 3600);
  const mins = Math.floor((diffSec % 3600) / 60);
  const secs = diffSec % 60;
  if (hours > 0) return `${hours}h ${mins}m`;
  if (mins > 0) return `${mins}m ${secs}s`;
  return `${secs}s`;
}
