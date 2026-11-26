import { confirmClose } from "@/lib/confirmation";
import { createFileRoute, Link } from "@tanstack/react-router";
import { useEffect, useMemo, useRef, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import {
  AlertTriangle,
  CheckCircle2,
  Clock,
  LifeBuoy,
  Loader2,
  MessageSquarePlus,
  RotateCcw,
  Search,
  Send,
  ShieldAlert,
} from "lucide-react";
import { toast } from "sonner";
import { AppLayout } from "@/components/app-layout";
import {
  formatSupportTimestamp,
  LiveWaitingTimer,
  SupportConnectionBadge,
  SupportStatusBadge,
} from "@/components/support/support-shared";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { useAuth } from "@/hooks/use-auth";
import {
  createClientOperationId,
  getSupportDetailQueryKey,
  getSupportListQueryKey,
  useSupportConversationDetail,
  useSupportDraft,
  useSupportInboxList,
  useSupportRealtime,
} from "@/hooks/use-support";
import { mergeMessages, upsertConversationSummary } from "@/lib/support-state";
import {
  createSupportConversation,
  markRestaurantSupportRead,
  reopenSupportConversation,
  replySupportConversation,
  resolveSupportConversation,
  type SupportConversationDetailDto,
  type SupportConversationStatus,
  type SupportMessageDto,
  type SupportSyncResponseDto,
} from "@/services/support";

export const Route = createFileRoute("/support")({
  validateSearch: (search: Record<string, unknown>) => ({
    conversationId: typeof search.conversationId === "string" ? search.conversationId : undefined,
  }),
  head: () => ({
    meta: [{ title: "Support — Tablo" }],
  }),
  component: SupportPage,
});

function SupportPage() {
  const { user, roles, loading } = useAuth();

  if (loading) {
    return (
      <div className="grid min-h-screen place-items-center bg-secondary/40">
        <Loader2 className="h-6 w-6 animate-spin text-primary" />
      </div>
    );
  }

  if (!user) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-secondary/40 px-4">
        <div className="max-w-md rounded-2xl border border-border bg-card p-8 text-center shadow-sm">
          <LifeBuoy className="mx-auto mb-3 h-10 w-10 text-primary" />
          <h1 className="text-xl font-bold">Sign in required</h1>
          <p className="mt-2 text-sm text-muted-foreground">
            Sign in with a Restaurant Admin or Owner account to contact Tablo Support.
          </p>
          <Link to="/auth" className="mt-5 inline-flex">
            <Button>Go to Sign In</Button>
          </Link>
        </div>
      </div>
    );
  }

  const allowed = roles.includes("Admin") || roles.includes("Owner");
  if (!allowed) {
    return (
      <AppLayout>
        <div className="mx-auto max-w-lg p-8">
          <div className="rounded-2xl border border-border bg-card p-8 text-center">
            <ShieldAlert className="mx-auto mb-3 h-10 w-10 text-amber-600" />
            <h1 className="text-xl font-bold">Restaurant Admin Required</h1>
            <p className="mt-2 text-sm text-muted-foreground">
              Support conversations are restricted to Restaurant Admin and Owner accounts.
            </p>
          </div>
        </div>
      </AppLayout>
    );
  }

  return (
    <AppLayout>
      <RestaurantSupportWorkspace />
    </AppLayout>
  );
}

function RestaurantSupportWorkspace() {
  const { user, signOut } = useAuth();
  const queryClient = useQueryClient();
  const searchParams = Route.useSearch();
  const tenantKey = user?.restaurantId;

  const [selectedId, setSelectedId] = useState<string | null>(searchParams.conversationId || null);
  const [statusFilter, setStatusFilter] = useState<"All" | SupportConversationStatus>("All");
  const [searchText, setSearchText] = useState("");
  const [newOpen, setNewOpen] = useState(false);

  const [newSubject, setNewSubject, clearNewSubject] = useSupportDraft("restaurant:new-subject");
  const [newMessage, setNewMessage, clearNewMessage] = useSupportDraft("restaurant:new-message");
  const [replyDraft, setReplyDraft, clearReplyDraft] = useSupportDraft(
    `restaurant:reply:${selectedId || "none"}`,
  );
  const [reopenDraft, setReopenDraft, clearReopenDraft] = useSupportDraft(
    `restaurant:reopen:${selectedId || "none"}`,
  );

  const [creatingBusy, setCreatingBusy] = useState(false);
  const [sendingBusy, setSendingBusy] = useState(false);
  const [actionBusy, setActionBusy] = useState(false);
  const [createOpId, setCreateOpId] = useState<string>(() => createClientOperationId());
  const [replyOpId, setReplyOpId] = useState<string>(() => createClientOperationId());
  const [optimisticByConv, setOptimisticByConv] = useState<Record<string, SupportMessageDto[]>>({});

  const markingReadRef = useRef<string | null>(null);
  const messagesEndRef = useRef<HTMLDivElement | null>(null);

  const { connectionState, revokedReason, reconnectAndSync } = useSupportRealtime("restaurant", {
    enabled: Boolean(user?.restaurantId),
    activeConversationId: selectedId,
  });

  const listQuery = useSupportInboxList("restaurant", {
    enabled: connectionState !== "revoked",
    connectionState,
  });

  const detailQuery = useSupportConversationDetail("restaurant", selectedId, {
    enabled: connectionState !== "revoked" && Boolean(selectedId),
    connectionState,
  });

  const conversations = useMemo(
    () => listQuery.data?.conversations ?? [],
    [listQuery.data?.conversations],
  );

  // Select first conversation automatically if none is selected yet
  useEffect(() => {
    if (!selectedId && conversations.length > 0) {
      setSelectedId(conversations[0].id);
    }
  }, [selectedId, conversations]);

  useEffect(() => {
    if (searchParams.conversationId) {
      setSelectedId(searchParams.conversationId);
    }
  }, [searchParams.conversationId]);

  const activeSummary = useMemo(
    () =>
      detailQuery.data?.conversation ??
      conversations.find((c) => c.id === selectedId) ??
      null,
    [detailQuery.data?.conversation, conversations, selectedId],
  );

  // Auto mark-as-read when viewing an unread conversation
  useEffect(() => {
    if (!activeSummary || activeSummary.restaurantUnreadCount <= 0 || connectionState === "revoked") {
      return;
    }
    const key = `${activeSummary.id}:${activeSummary.version}`;
    if (markingReadRef.current === key) return;
    markingReadRef.current = key;

    markRestaurantSupportRead(activeSummary.id)
      .then((updated) => {
        const listKey = getSupportListQueryKey("restaurant", tenantKey);
        queryClient.setQueryData<SupportSyncResponseDto>(listKey, (old) => {
          if (!old) return old;
          const next = upsertConversationSummary(old.conversations, updated, "restaurant");
          return {
            ...old,
            conversations: next,
            totalUnreadCount: next.reduce((acc, c) => acc + c.restaurantUnreadCount, 0),
          };
        });
        const detailKey = getSupportDetailQueryKey("restaurant", activeSummary.id);
        queryClient.setQueryData<SupportConversationDetailDto>(detailKey, (old) =>
          old ? { ...old, conversation: updated } : old,
        );
      })
      .catch(() => {
        markingReadRef.current = null;
      });
  }, [activeSummary, connectionState, queryClient, tenantKey]);

  const mergedMessages = useMemo(() => {
    const serverMessages = detailQuery.data?.messages ?? [];
    const localOpt = selectedId ? optimisticByConv[selectedId] ?? [] : [];
    return mergeMessages(serverMessages, localOpt);
  }, [detailQuery.data?.messages, optimisticByConv, selectedId]);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [mergedMessages.length, selectedId]);

  const filteredConversations = useMemo(() => {
    const q = searchText.trim().toLowerCase();
    return conversations.filter((c) => {
      if (statusFilter !== "All" && c.status !== statusFilter) return false;
      if (!q) return true;
      return (
        c.subject.toLowerCase().includes(q) ||
        (c.lastMessagePreview || "").toLowerCase().includes(q)
      );
    });
  }, [conversations, searchText, statusFilter]);

  const handleCreateConversation = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newSubject.trim() || !newMessage.trim()) {
      toast.error("Please enter both a subject and a message.");
      return;
    }

    setCreatingBusy(true);
    try {
      const result = await createSupportConversation({
        subject: newSubject.trim(),
        message: newMessage.trim(),
        clientOperationId: createOpId,
      });

      const listKey = getSupportListQueryKey("restaurant", tenantKey);
      queryClient.setQueryData<SupportSyncResponseDto>(listKey, (old) => {
        const list = old?.conversations ?? [];
        const next = upsertConversationSummary(list, result.conversation, "restaurant");
        return {
          serverUtc: result.conversation.updatedAtUtc,
          conversations: next,
          totalUnreadCount: next.reduce((acc, c) => acc + c.restaurantUnreadCount, 0),
        };
      });

      setSelectedId(result.conversation.id);
      clearNewSubject();
      clearNewMessage();
      setCreateOpId(createClientOperationId());
      setNewOpen(false);
      toast.success("Support conversation started.");
    } catch (err: any) {
      toast.error(err?.message || "Failed to send support request. Your draft has been preserved.");
    } finally {
      setCreatingBusy(false);
    }
  };

  const handleSendReply = async (e?: React.FormEvent, retryOpId?: string, retryText?: string) => {
    e?.preventDefault();
    if (!selectedId) return;
    const text = (retryText ?? replyDraft).trim();
    if (!text) return;

    const opId = retryOpId ?? replyOpId;
    const optimisticMsg: SupportMessageDto = {
      id: `opt-${opId}`,
      conversationId: selectedId,
      senderType: "Restaurant",
      senderEmployeeId: user?.id,
      senderName: user?.name || user?.email || "Restaurant Admin",
      senderEmail: user?.email || "",
      message: text,
      createdAtUtc: new Date().toISOString(),
      sequenceNumber: Number.MAX_SAFE_INTEGER,
      clientOperationId: opId,
      localStatus: "sending",
    };

    setOptimisticByConv((prev) => ({
      ...prev,
      [selectedId]: mergeMessages(prev[selectedId] ?? [], [optimisticMsg]),
    }));

    setSendingBusy(true);
    try {
      const result = await replySupportConversation(selectedId, {
        message: text,
        clientOperationId: opId,
      });

      setOptimisticByConv((prev) => ({
        ...prev,
        [selectedId]: (prev[selectedId] ?? []).filter(
          (m) => m.clientOperationId !== opId,
        ),
      }));

      const detailKey = getSupportDetailQueryKey("restaurant", selectedId);
      queryClient.setQueryData<SupportConversationDetailDto>(detailKey, (old) => {
        if (!old) return old;
        return {
          ...old,
          conversation: result.conversation,
          messages: result.message
            ? mergeMessages(old.messages, [result.message])
            : old.messages,
        };
      });

      const listKey = getSupportListQueryKey("restaurant", tenantKey);
      queryClient.setQueryData<SupportSyncResponseDto>(listKey, (old) => {
        if (!old) return old;
        const next = upsertConversationSummary(old.conversations, result.conversation, "restaurant");
        return {
          ...old,
          conversations: next,
          totalUnreadCount: next.reduce((acc, c) => acc + c.restaurantUnreadCount, 0),
        };
      });

      if (!retryText || replyDraft.trim() === text) {
        clearReplyDraft();
      }
      setReplyOpId(createClientOperationId());
    } catch (err: any) {
      setOptimisticByConv((prev) => ({
        ...prev,
        [selectedId]: (prev[selectedId] ?? []).map((m) =>
          m.clientOperationId === opId
            ? { ...m, localStatus: "failed", localError: err?.message || "Send failed" }
            : m,
        ),
      }));
      toast.error(err?.message || "Failed to send reply. Draft preserved for retry.");
    } finally {
      setSendingBusy(false);
    }
  };

  const handleResolve = async () => {
    if (!selectedId) return;
    setActionBusy(true);
    try {
      const updated = await resolveSupportConversation(selectedId);
      const listKey = getSupportListQueryKey("restaurant", tenantKey);
      queryClient.setQueryData<SupportSyncResponseDto>(listKey, (old) => {
        if (!old) return old;
        const next = upsertConversationSummary(old.conversations, updated, "restaurant");
        return {
          ...old,
          conversations: next,
          totalUnreadCount: next.reduce((acc, c) => acc + c.restaurantUnreadCount, 0),
        };
      });
      const detailKey = getSupportDetailQueryKey("restaurant", selectedId);
      queryClient.setQueryData<SupportConversationDetailDto>(detailKey, (old) =>
        old ? { ...old, conversation: updated } : old,
      );
      toast.success("Conversation marked as resolved.");
    } catch (err: any) {
      toast.error(err?.message || "Could not resolve conversation.");
    } finally {
      setActionBusy(false);
    }
  };

  const handleReopen = async (withMessage: boolean) => {
    if (!selectedId) return;
    const message = withMessage ? reopenDraft.trim() : undefined;
    if (withMessage && !message) {
      toast.error("Please enter a message to reopen the conversation.");
      return;
    }

    setActionBusy(true);
    const opId = withMessage ? replyOpId : undefined;
    try {
      const result = await reopenSupportConversation(selectedId, {
        message,
        clientOperationId: opId,
      });

      const detailKey = getSupportDetailQueryKey("restaurant", selectedId);
      queryClient.setQueryData<SupportConversationDetailDto>(detailKey, (old) => {
        if (!old) return old;
        return {
          ...old,
          conversation: result.conversation,
          messages: result.message
            ? mergeMessages(old.messages, [result.message])
            : old.messages,
        };
      });

      const listKey = getSupportListQueryKey("restaurant", tenantKey);
      queryClient.setQueryData<SupportSyncResponseDto>(listKey, (old) => {
        if (!old) return old;
        const next = upsertConversationSummary(old.conversations, result.conversation, "restaurant");
        return {
          ...old,
          conversations: next,
          totalUnreadCount: next.reduce((acc, c) => acc + c.restaurantUnreadCount, 0),
        };
      });

      if (withMessage) {
        clearReopenDraft();
        setReplyOpId(createClientOperationId());
      }
      toast.success("Conversation reopened.");
    } catch (err: any) {
      toast.error(err?.message || "Failed to reopen conversation.");
    } finally {
      setActionBusy(false);
    }
  };

  if (connectionState === "revoked") {
    return (
      <div className="mx-auto max-w-xl p-8">
        <div className="rounded-2xl border border-destructive/30 bg-destructive/5 p-6 text-center">
          <AlertTriangle className="mx-auto mb-3 h-10 w-10 text-destructive" />
          <h2 className="text-lg font-bold text-destructive">Support Access Revoked</h2>
          <p className="mt-2 text-sm text-muted-foreground">
            {revokedReason || "Your active session or support permissions have changed."}
          </p>
          <div className="mt-5 flex justify-center gap-3">
            <Button variant="outline" onClick={() => void signOut("/auth")}>
              Sign Out
            </Button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="flex h-full min-h-0 flex-col bg-secondary/20">
      <header className="flex flex-wrap items-center justify-between gap-3 border-b border-border bg-card px-4 py-3 sm:px-6">
        <div className="flex items-center gap-3">
          <span className="grid h-10 w-10 place-items-center rounded-xl bg-primary/10 text-primary">
            <LifeBuoy className="h-5 w-5" />
          </span>
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-lg font-bold tracking-tight">Restaurant Support</h1>
              {(listQuery.data?.totalUnreadCount ?? 0) > 0 && (
                <Badge variant="default" data-testid="restaurant-total-unread">
                  {listQuery.data?.totalUnreadCount} unread
                </Badge>
              )}
            </div>
            <p className="text-xs text-muted-foreground">
              Direct priority channel with the Tablo support team
            </p>
          </div>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <SupportConnectionBadge
            state={connectionState}
            onReconnect={() => {
              void reconnectAndSync();
              void listQuery.refetch();
              if (selectedId) void detailQuery.refetch();
            }}
          />
          <Button
            type="button"
            size="sm"
            onClick={() => {
              setCreateOpId(createClientOperationId());
              setNewOpen(true);
            }}
            data-testid="new-support-conversation-btn"
          >
            <MessageSquarePlus className="mr-1.5 h-4 w-4" />
            New Conversation
          </Button>
        </div>
      </header>

      <div className="grid min-h-0 flex-1 grid-cols-1 md:grid-cols-[340px_1fr]">
        {/* Left pane: Conversation List */}
        <aside className="flex min-h-0 flex-col border-b border-border bg-card md:border-b-0 md:border-r">
          <div className="space-y-2 border-b border-border p-3">
            <div className="relative">
              <Search className="pointer-events-none absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                value={searchText}
                onChange={(e) => setSearchText(e.target.value)}
                placeholder="Search subjects or messages..."
                className="h-9 pl-8 text-xs"
              />
            </div>
            <div className="flex flex-wrap gap-1">
              {(["All", "AwaitingSupport", "AwaitingRestaurant", "Resolved"] as const).map((tab) => (
                <button
                  key={tab}
                  type="button"
                  onClick={() => setStatusFilter(tab)}
                  className={`rounded-md px-2 py-1 text-[11px] font-medium transition ${
                    statusFilter === tab
                      ? "bg-primary text-primary-foreground"
                      : "bg-secondary text-muted-foreground hover:text-foreground"
                  }`}
                >
                  {tab === "All"
                    ? "All"
                    : tab === "AwaitingSupport"
                      ? "Awaiting Support"
                      : tab === "AwaitingRestaurant"
                        ? "Awaiting Reply"
                        : "Resolved"}
                </button>
              ))}
            </div>
          </div>

          <div className="min-h-0 flex-1 divide-y divide-border overflow-y-auto">
            {listQuery.isLoading ? (
              <div className="p-8 text-center">
                <Loader2 className="mx-auto h-5 w-5 animate-spin text-primary" />
              </div>
            ) : filteredConversations.length === 0 ? (
              <div className="p-6 text-center text-xs text-muted-foreground">
                No support conversations yet. Start a conversation to reach our team.
              </div>
            ) : (
              filteredConversations.map((conv) => {
                const isSelected = conv.id === selectedId;
                return (
                  <button
                    key={conv.id}
                    type="button"
                    data-testid={`support-conv-item-${conv.id}`}
                    onClick={() => setSelectedId(conv.id)}
                    className={`w-full space-y-1.5 p-3.5 text-left transition ${
                      isSelected ? "bg-primary/5 ring-1 ring-inset ring-primary/20" : "hover:bg-secondary/50"
                    }`}
                  >
                    <div className="flex items-start justify-between gap-2">
                      <span className="line-clamp-1 text-sm font-semibold text-foreground">
                        {conv.subject}
                      </span>
                      {conv.restaurantUnreadCount > 0 && (
                        <Badge
                          variant="default"
                          className="h-5 min-w-5 shrink-0 justify-center px-1.5 text-[10px]"
                          data-testid={`conv-unread-${conv.id}`}
                        >
                          {conv.restaurantUnreadCount}
                        </Badge>
                      )}
                    </div>

                    {conv.lastMessagePreview && (
                      <p className="line-clamp-1 text-xs text-muted-foreground">
                        {conv.lastMessageSenderType === "SuperAdmin" ? "Support: " : "You: "}
                        {conv.lastMessagePreview}
                      </p>
                    )}

                    <div className="flex flex-wrap items-center justify-between gap-1 pt-0.5">
                      <SupportStatusBadge status={conv.status} isEscalated={conv.isEscalated} />
                      <span className="text-[10px] text-muted-foreground">
                        {formatSupportTimestamp(conv.lastActivityAtUtc)}
                      </span>
                    </div>
                  </button>
                );
              })
            )}
          </div>
        </aside>

        {/* Right pane: Conversation Thread */}
        <section className="flex min-h-0 flex-col bg-background">
          {!selectedId || !activeSummary ? (
            <div className="flex flex-1 flex-col items-center justify-center p-8 text-center">
              <LifeBuoy className="mb-3 h-10 w-10 text-muted-foreground/50" />
              <h2 className="text-base font-semibold">Select or start a conversation</h2>
              <p className="mt-1 max-w-sm text-xs text-muted-foreground">
                Messages are delivered in real time. Any inquiry unanswered for 10 minutes automatically
                alerts our on-call team at support@neorizsolutions.com.
              </p>
            </div>
          ) : (
            <>
              <div className="flex flex-wrap items-center justify-between gap-3 border-b border-border bg-card px-5 py-3.5">
                <div className="space-y-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <h2 className="text-base font-bold" data-testid="active-conv-subject">
                      {activeSummary.subject}
                    </h2>
                    <SupportStatusBadge
                      status={activeSummary.status}
                      isEscalated={activeSummary.isEscalated}
                    />
                  </div>
                  <div className="flex flex-wrap items-center gap-3 text-xs text-muted-foreground">
                    {activeSummary.status === "AwaitingSupport" && activeSummary.awaitingSupportSinceUtc && (
                      <span className="inline-flex items-center gap-1 font-medium text-amber-700">
                        <Clock className="h-3.5 w-3.5" />
                        Waiting: <LiveWaitingTimer sinceUtc={activeSummary.awaitingSupportSinceUtc} />
                      </span>
                    )}
                    {activeSummary.isEscalated && activeSummary.escalatedAtUtc && (
                      <span className="inline-flex items-center gap-1 font-medium text-rose-700">
                        <AlertTriangle className="h-3.5 w-3.5" />
                        Urgent alert sent {formatSupportTimestamp(activeSummary.escalatedAtUtc)}
                      </span>
                    )}
                    {activeSummary.status === "Resolved" && activeSummary.resolvedAtUtc && (
                      <span className="inline-flex items-center gap-1 text-emerald-700">
                        <CheckCircle2 className="h-3.5 w-3.5" />
                        Resolved {formatSupportTimestamp(activeSummary.resolvedAtUtc)}
                        {activeSummary.resolvedBy ? ` by ${activeSummary.resolvedBy}` : ""}
                      </span>
                    )}
                  </div>
                </div>

                {activeSummary.status !== "Resolved" && (
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    disabled={actionBusy}
                    onClick={handleResolve}
                    data-testid="restaurant-resolve-btn"
                  >
                    <CheckCircle2 className="mr-1.5 h-4 w-4 text-emerald-600" />
                    Mark Resolved
                  </Button>
                )}
              </div>

              <div className="min-h-0 flex-1 space-y-3 overflow-y-auto p-5" data-testid="restaurant-message-thread">
                {detailQuery.isLoading ? (
                  <div className="p-8 text-center">
                    <Loader2 className="mx-auto h-5 w-5 animate-spin text-primary" />
                  </div>
                ) : (
                  mergedMessages.map((msg) => {
                    const isMine = msg.senderType === "Restaurant";
                    return (
                      <div
                        key={msg.localStatus ? `local-${msg.clientOperationId}` : msg.id}
                        className={`flex flex-col ${isMine ? "items-end" : "items-start"}`}
                        data-testid={`support-msg-${msg.id}`}
                      >
                        <div
                          className={`max-w-[85%] rounded-2xl px-4 py-2.5 text-sm shadow-xs sm:max-w-[72%] ${
                            isMine
                              ? "bg-primary text-primary-foreground"
                              : "border border-border bg-card text-foreground"
                          }`}
                        >
                          <div className="mb-1 flex items-center justify-between gap-4 text-[11px] opacity-80">
                            <span className="font-semibold">
                              {isMine ? msg.senderName : `Tablo Support (${msg.senderName})`}
                            </span>
                            <span>{formatSupportTimestamp(msg.createdAtUtc)}</span>
                          </div>
                          <p className="whitespace-pre-wrap break-words leading-relaxed">{msg.message}</p>
                          {msg.localStatus === "sending" && (
                            <div className="mt-1 text-[10px] opacity-80">Sending...</div>
                          )}
                          {msg.localStatus === "failed" && (
                            <div className="mt-1.5 flex items-center gap-2 text-[11px] text-amber-200">
                              <span>{msg.localError || "Failed to send"}</span>
                              <button
                                type="button"
                                className="underline"
                                onClick={() =>
                                  void handleSendReply(
                                    undefined,
                                    msg.clientOperationId || undefined,
                                    msg.message,
                                  )
                                }
                              >
                                Retry
                              </button>
                            </div>
                          )}
                        </div>
                      </div>
                    );
                  })
                )}
                <div ref={messagesEndRef} />
              </div>

              {activeSummary.status === "Resolved" ? (
                <div className="border-t border-border bg-card p-4" data-testid="restaurant-reopen-panel">
                  <div className="mb-2 flex items-center justify-between gap-2">
                    <span className="text-xs font-semibold text-foreground">
                      This conversation is resolved. Reopen with a new message:
                    </span>
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      disabled={actionBusy}
                      onClick={() => void handleReopen(false)}
                    >
                      <RotateCcw className="mr-1 h-3.5 w-3.5" />
                      Reopen Only
                    </Button>
                  </div>
                  <div className="flex flex-col gap-2 sm:flex-row">
                    <Textarea
                      value={reopenDraft}
                      onChange={(e) => setReopenDraft(e.target.value)}
                      placeholder="Describe why you are reopening this ticket..."
                      rows={2}
                      className="min-h-[60px] flex-1 resize-none text-sm"
                      data-testid="restaurant-reopen-input"
                    />
                    <Button
                      type="button"
                      disabled={actionBusy || !reopenDraft.trim()}
                      onClick={() => void handleReopen(true)}
                      className="shrink-0 self-end"
                      data-testid="restaurant-reopen-submit"
                    >
                      {actionBusy && <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />}
                      <RotateCcw className="mr-1.5 h-4 w-4" />
                      Reopen & Send
                    </Button>
                  </div>
                </div>
              ) : (
                <form
                  onSubmit={(e) => void handleSendReply(e)}
                  className="border-t border-border bg-card p-4"
                >
                  <div className="flex flex-col gap-2 sm:flex-row">
                    <Textarea
                      value={replyDraft}
                      onChange={(e) => setReplyDraft(e.target.value)}
                      placeholder="Write a reply to Tablo Support..."
                      rows={2}
                      className="min-h-[64px] flex-1 resize-none text-sm"
                      data-testid="restaurant-reply-input"
                    />
                    <Button
                      type="submit"
                      disabled={sendingBusy || !replyDraft.trim()}
                      className="shrink-0 self-end"
                      data-testid="restaurant-send-reply-btn"
                    >
                      {sendingBusy && <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />}
                      <Send className="mr-1.5 h-4 w-4" />
                      Send Reply
                    </Button>
                  </div>
                </form>
              )}
            </>
          )}
        </section>
      </div>

      {/* New Conversation Dialog */}
      <Dialog open={newOpen} onOpenChange={setNewOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>New Support Conversation</DialogTitle>
            <DialogDescription>
              Describe your issue or question. Unanswered requests automatically escalate after 10 minutes.
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={handleCreateConversation} className="space-y-4">
            <div>
              <label className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                Subject
              </label>
              <Input
                required
                maxLength={200}
                value={newSubject}
                onChange={(e) => setNewSubject(e.target.value)}
                placeholder="e.g. Thermal printer offline at Kitchen Station 2"
                className="mt-1"
                data-testid="new-conv-subject-input"
              />
            </div>
            <div>
              <label className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                Message
              </label>
              <Textarea
                required
                maxLength={4000}
                rows={4}
                value={newMessage}
                onChange={(e) => setNewMessage(e.target.value)}
                placeholder="Provide details so our team can assist quickly..."
                className="mt-1"
                data-testid="new-conv-message-input"
              />
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => void confirmClose(() => setNewOpen(false))}>
                Cancel
              </Button>
              <Button
                type="submit"
                disabled={creatingBusy || !newSubject.trim() || !newMessage.trim()}
                data-testid="new-conv-submit-btn"
              >
                {creatingBusy && <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />}
                Submit Ticket
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
