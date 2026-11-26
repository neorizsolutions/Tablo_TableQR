import { useCallback, useEffect, useRef, useState } from "react";
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from "@microsoft/signalr";
import { useQuery, useQueryClient, type QueryClient } from "@tanstack/react-query";
import { API_BASE_URL, ApiError } from "@/services/apiClient";
import {
  getRestaurantSupportConversation,
  getRestaurantSupportConversations,
  getSuperAdminSupportConversation,
  getSuperAdminSupportInbox,
  type SupportConnectionState,
  type SupportConversationDetailDto,
  type SupportRealtimeEventDto,
  type SupportSyncResponseDto,
} from "@/services/support";
import {
  applyRealtimeEventToDetail,
  applyRealtimeEventToList,
  mergeMessages,
  mergeSyncResponse,
} from "@/lib/support-state";
import { useAuth } from "./use-auth";

type SupportMode = "restaurant" | "superadmin";

export function createClientOperationId(): string {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return crypto.randomUUID();
  }
  return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    const v = c === "x" ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

export function getSupportListQueryKey(mode: SupportMode, tenantKey?: string) {
  return ["support", mode, "list", tenantKey || "platform"] as const;
}

export function getSupportDetailQueryKey(mode: SupportMode, conversationId: string | null) {
  return ["support", mode, "detail", conversationId] as const;
}

interface ManagedRealtimeSession {
  mode: SupportMode;
  token: string;
  tenantKey?: string;
  connection: HubConnection;
  state: SupportConnectionState;
  revokedReason: string | null;
  lastServerUtc: string | null;
  activeConversationId: string | null;
  seenEvents: Set<string>;
  listeners: Set<() => void>;
  queryClients: Set<QueryClient>;
  refCount: number;
  stopped: boolean;
  startPromise: Promise<void> | null;
}

const sessions = new Map<string, ManagedRealtimeSession>();

function getSessionKey(mode: SupportMode, token: string) {
  return `${mode}:${token}`;
}

function notifySessionListeners(session: ManagedRealtimeSession) {
  for (const listener of session.listeners) {
    listener();
  }
}

async function reconcileSessionAfterSubscribe(session: ManagedRealtimeSession) {
  if (session.stopped) return;
  const listKey = getSupportListQueryKey(session.mode, session.tenantKey);

  try {
    const delta =
      session.mode === "superadmin"
        ? await getSuperAdminSupportInbox({ sinceUtc: session.lastServerUtc || undefined })
        : await getRestaurantSupportConversations(session.lastServerUtc || undefined);

    const isFullReplace = !session.lastServerUtc;
    session.lastServerUtc = delta.serverUtc;

    for (const qc of session.queryClients) {
      qc.setQueryData<SupportSyncResponseDto>(listKey, (old) =>
        mergeSyncResponse(old, delta, session.mode, isFullReplace),
      );
    }

    if (session.activeConversationId) {
      const convId = session.activeConversationId;
      const detailKey = getSupportDetailQueryKey(session.mode, convId);
      for (const qc of session.queryClients) {
        const existing = qc.getQueryData<SupportConversationDetailDto>(detailKey);
        const maxSeq = existing?.messages
          .filter((m) => !m.localStatus)
          .reduce((max, m) => Math.max(max, m.sequenceNumber), 0);

        const freshDetail =
          session.mode === "superadmin"
            ? await getSuperAdminSupportConversation(convId, existing ? maxSeq : undefined)
            : await getRestaurantSupportConversation(convId, existing ? maxSeq : undefined);

        qc.setQueryData<SupportConversationDetailDto>(detailKey, (curr) => {
          if (!curr || !existing) return freshDetail;
          return {
            conversation:
              freshDetail.conversation.version >= curr.conversation.version
                ? freshDetail.conversation
                : curr.conversation,
            messages: mergeMessages(curr.messages, freshDetail.messages),
            escalationLogs:
              freshDetail.escalationLogs.length > 0
                ? freshDetail.escalationLogs
                : curr.escalationLogs,
            auditLogs:
              freshDetail.auditLogs.length > 0 ? freshDetail.auditLogs : curr.auditLogs,
          };
        });
      }
    }
  } catch (err) {
    if (err instanceof ApiError && (err.status === 401 || err.status === 403)) {
      session.state = "revoked";
      session.revokedReason = err.message || "Your support access has been revoked.";
      void disposeSession(session);
      notifySessionListeners(session);
    }
  }
}

async function joinAuthorizedGroups(session: ManagedRealtimeSession) {
  if (session.stopped || session.connection.state !== HubConnectionState.Connected) return;
  if (session.mode === "superadmin") {
    await session.connection.invoke("JoinSuperAdminInbox");
  } else {
    await session.connection.invoke("JoinRestaurantSupport", null);
  }
  if (session.activeConversationId) {
    await session.connection.invoke("JoinConversation", session.activeConversationId);
  }
}

async function disposeSession(session: ManagedRealtimeSession) {
  session.stopped = true;
  session.connection.off("SupportEvent");
  session.connection.off("AccessRevoked");
  try {
    await session.connection.stop();
  } catch {
    // Connection may already be closed.
  }
}

function getOrCreateSession(
  mode: SupportMode,
  token: string,
  tenantKey: string | undefined,
  queryClient: QueryClient,
): ManagedRealtimeSession {
  const key = getSessionKey(mode, token);

  // Tear down any stale sessions for other tokens/modes before opening a new one.
  for (const [existingKey, existingSession] of sessions.entries()) {
    if (existingKey !== key && existingSession.refCount <= 0) {
      sessions.delete(existingKey);
      void disposeSession(existingSession);
    }
  }

  let session = sessions.get(key);
  if (session && !session.stopped && session.state !== "revoked") {
    session.queryClients.add(queryClient);
    return session;
  }

  const hubUrl = API_BASE_URL.replace(/\/api\/?$/, "/hubs/support");
  const connection = new HubConnectionBuilder()
    .withUrl(hubUrl, {
      accessTokenFactory: () => localStorage.getItem("tablo_token") || token || "",
    })
    .withAutomaticReconnect([0, 1500, 3000, 5000, 10000])
    .configureLogging(LogLevel.Warning)
    .build();

  session = {
    mode,
    token,
    tenantKey,
    connection,
    state: "connecting",
    revokedReason: null,
    lastServerUtc: null,
    activeConversationId: null,
    seenEvents: new Set<string>(),
    listeners: new Set(),
    queryClients: new Set([queryClient]),
    refCount: 0,
    stopped: false,
    startPromise: null,
  };

  const currentSession = session;

  // Register event handlers BEFORE calling connection.start() to eliminate missed-message windows.
  connection.on("SupportEvent", (evt: SupportRealtimeEventDto) => {
    if (currentSession.stopped || !evt || !evt.eventId) return;
    if (currentSession.seenEvents.has(evt.eventId)) return;

    currentSession.seenEvents.add(evt.eventId);
    if (currentSession.seenEvents.size > 500) {
      const oldest = currentSession.seenEvents.values().next().value;
      if (oldest) currentSession.seenEvents.delete(oldest);
    }

    const listKey = getSupportListQueryKey(currentSession.mode, currentSession.tenantKey);
    const detailKey = getSupportDetailQueryKey(currentSession.mode, evt.conversationId);

    for (const qc of currentSession.queryClients) {
      qc.setQueryData<SupportSyncResponseDto>(listKey, (old) =>
        applyRealtimeEventToList(old, evt, currentSession.mode),
      );
      qc.setQueryData<SupportConversationDetailDto>(detailKey, (old) =>
        applyRealtimeEventToDetail(old, evt),
      );
    }
    notifySessionListeners(currentSession);
  });

  connection.on("AccessRevoked", (reason: string) => {
    currentSession.state = "revoked";
    currentSession.revokedReason = reason || "Support access has been revoked.";
    void disposeSession(currentSession);
    notifySessionListeners(currentSession);
  });

  connection.onreconnecting(() => {
    if (currentSession.stopped || currentSession.state === "revoked") return;
    currentSession.state = "reconnecting";
    notifySessionListeners(currentSession);
  });

  connection.onreconnected(async () => {
    if (currentSession.stopped || currentSession.state === "revoked") return;
    currentSession.state = "connected";
    notifySessionListeners(currentSession);
    try {
      await joinAuthorizedGroups(currentSession);
      await reconcileSessionAfterSubscribe(currentSession);
    } catch {
      currentSession.state = "offline";
      notifySessionListeners(currentSession);
    }
  });

  connection.onclose((err) => {
    if (currentSession.state === "revoked") return;
    if (!currentSession.stopped) {
      currentSession.state = err?.message?.includes("401") || err?.message?.includes("403")
        ? "revoked"
        : "offline";
      if (currentSession.state === "revoked") {
        currentSession.revokedReason = "Session is no longer authorized.";
      }
      notifySessionListeners(currentSession);
    }
  });

  sessions.set(key, currentSession);
  void startSession(currentSession);
  return currentSession;
}

async function startSession(session: ManagedRealtimeSession) {
  if (session.stopped || session.state === "revoked") return;
  if (session.connection.state === HubConnectionState.Connected) return;
  if (session.startPromise) return session.startPromise;

  session.state = "connecting";
  notifySessionListeners(session);

  session.startPromise = (async () => {
    try {
      await session.connection.start();
      if (session.stopped) return;
      session.state = "connected";
      notifySessionListeners(session);
      await joinAuthorizedGroups(session);
      await reconcileSessionAfterSubscribe(session);
    } catch (err: any) {
      if (session.state !== "revoked") {
        const msg = String(err?.message || "");
        if (msg.includes("401") || msg.includes("403") || msg.includes("Access denied")) {
          session.state = "revoked";
          session.revokedReason = "Your session cannot access Support.";
        } else {
          session.state = "offline";
        }
        notifySessionListeners(session);
      }
    } finally {
      session.startPromise = null;
    }
  })();

  return session.startPromise;
}

export function useSupportRealtime(
  mode: SupportMode,
  options?: {
    enabled?: boolean;
    activeConversationId?: string | null;
  },
) {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const token = typeof localStorage !== "undefined" ? localStorage.getItem("tablo_token") || "" : "";
  const enabled = (options?.enabled ?? true) && Boolean(user && token);
  const activeConversationId = options?.activeConversationId ?? null;
  const tenantKey = mode === "restaurant" ? user?.restaurantId : "platform";

  const [connectionState, setConnectionState] = useState<SupportConnectionState>("connecting");
  const [revokedReason, setRevokedReason] = useState<string | null>(null);
  const sessionRef = useRef<ManagedRealtimeSession | null>(null);
  const prevConvRef = useRef<string | null>(null);

  useEffect(() => {
    if (!enabled) {
      setConnectionState("offline");
      return;
    }

    const session = getOrCreateSession(mode, token, tenantKey, queryClient);
    sessionRef.current = session;
    session.refCount += 1;

    const syncUiState = () => {
      setConnectionState(session.state);
      setRevokedReason(session.revokedReason);
    };

    session.listeners.add(syncUiState);
    syncUiState();

    return () => {
      session.listeners.delete(syncUiState);
      session.refCount = Math.max(0, session.refCount - 1);
      if (session.refCount === 0) {
        sessions.delete(getSessionKey(mode, token));
        void disposeSession(session);
      }
      sessionRef.current = null;
    };
  }, [enabled, mode, token, tenantKey, queryClient]);

  // Synchronize conversation group subscription when user selects a different conversation.
  useEffect(() => {
    const session = sessionRef.current;
    if (!session || !enabled) return;

    const prevId = prevConvRef.current;
    prevConvRef.current = activeConversationId;
    session.activeConversationId = activeConversationId;

    if (session.connection.state !== HubConnectionState.Connected) return;

    const updateGroups = async () => {
      try {
        if (prevId && prevId !== activeConversationId) {
          await session.connection.invoke("LeaveConversation", prevId).catch(() => undefined);
        }
        if (activeConversationId) {
          await session.connection.invoke("JoinConversation", activeConversationId);
        }
      } catch (err: any) {
        const msg = String(err?.message || "");
        if (msg.includes("revoked") || msg.includes("Access denied")) {
          session.state = "revoked";
          session.revokedReason = msg;
          notifySessionListeners(session);
        }
      }
    };

    void updateGroups();
  }, [activeConversationId, enabled, connectionState]);

  const reconnectAndSync = useCallback(async () => {
    const session = sessionRef.current;
    if (!session) return;
    if (session.state === "revoked") return;

    if (session.connection.state !== HubConnectionState.Connected) {
      await startSession(session);
    } else {
      await joinAuthorizedGroups(session).catch(() => undefined);
      await reconcileSessionAfterSubscribe(session);
    }
  }, []);

  return {
    connectionState,
    revokedReason,
    reconnectAndSync,
  };
}

export function useSupportInboxList(
  mode: SupportMode,
  options?: {
    enabled?: boolean;
    connectionState?: SupportConnectionState;
  },
) {
  const { user } = useAuth();
  const tenantKey = mode === "restaurant" ? user?.restaurantId : "platform";
  const enabled = (options?.enabled ?? true) && Boolean(user);
  const isOffline = options?.connectionState === "offline";

  return useQuery({
    queryKey: getSupportListQueryKey(mode, tenantKey),
    queryFn: async () => {
      return mode === "superadmin"
        ? await getSuperAdminSupportInbox()
        : await getRestaurantSupportConversations();
    },
    enabled,
    staleTime: 1000 * 30,
    // When SignalR is offline/unavailable, enable a fallback sync interval as recovery path.
    refetchInterval: isOffline ? 8000 : false,
  });
}

export function useSupportConversationDetail(
  mode: SupportMode,
  conversationId: string | null,
  options?: {
    enabled?: boolean;
    connectionState?: SupportConnectionState;
  },
) {
  const enabled = (options?.enabled ?? true) && Boolean(conversationId);
  const isOffline = options?.connectionState === "offline";

  return useQuery({
    queryKey: getSupportDetailQueryKey(mode, conversationId),
    queryFn: async () => {
      if (!conversationId) throw new Error("Conversation ID required");
      return mode === "superadmin"
        ? await getSuperAdminSupportConversation(conversationId)
        : await getRestaurantSupportConversation(conversationId);
    },
    enabled,
    staleTime: 1000 * 15,
    refetchInterval: isOffline ? 6000 : false,
  });
}

export function useSupportUnreadCount(mode: SupportMode, enabled: boolean) {
  const { connectionState } = useSupportRealtime(mode, { enabled });
  const listQuery = useSupportInboxList(mode, { enabled, connectionState });
  return listQuery.data?.totalUnreadCount ?? 0;
}

export function useSupportDraft(storageKey: string) {
  const fullKey = `tablo_support_draft:${storageKey}`;
  const [draft, setDraftState] = useState<string>(() => {
    try {
      return localStorage.getItem(fullKey) || "";
    } catch {
      return "";
    }
  });

  useEffect(() => {
    try {
      setDraftState(localStorage.getItem(fullKey) || "");
    } catch {
      setDraftState("");
    }
  }, [fullKey]);

  const setDraft = useCallback(
    (value: string) => {
      setDraftState(value);
      try {
        if (value) {
          localStorage.setItem(fullKey, value);
        } else {
          localStorage.removeItem(fullKey);
        }
      } catch {
        // Storage unavailable; keep in memory state cleanly.
      }
    },
    [fullKey],
  );

  const clearDraft = useCallback(() => {
    setDraftState("");
    try {
      localStorage.removeItem(fullKey);
    } catch {
      // Ignore storage error.
    }
  }, [fullKey]);

  return [draft, setDraft, clearDraft] as const;
}
