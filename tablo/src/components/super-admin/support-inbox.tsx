import { useEffect, useMemo, useRef, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import {
  AlertTriangle,
  Building2,
  CheckCircle2,
  Clock,
  Inbox,
  Loader2,
  Mail,
  Search,
  Send,
} from "lucide-react";
import { toast } from "sonner";
import {
  formatSupportTimestamp,
  LiveWaitingTimer,
  SupportConnectionBadge,
  SupportStatusBadge,
} from "@/components/support/support-shared";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
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
  markSuperAdminSupportRead,
  replySuperAdminSupportConversation,
  resolveSuperAdminSupportConversation,
  type SupportConversationDetailDto,
  type SupportMessageDto,
  type SupportSyncResponseDto,
} from "@/services/support";

type InboxFilter = "All" | "AwaitingSupport" | "Escalated" | "AwaitingRestaurant" | "Resolved";

export function SupportInboxPane({ initialConversationId }: { initialConversationId?: string }) {
  const { user } = useAuth();
  const queryClient = useQueryClient();

  const [selectedId, setSelectedId] = useState<string | null>(initialConversationId || null);
  const [filter, setFilter] = useState<InboxFilter>("All");
  const [searchText, setSearchText] = useState("");
  const [replyDraft, setReplyDraft, clearReplyDraft] = useSupportDraft(
    `superadmin:reply:${selectedId || "none"}`,
  );

  const [sendingBusy, setSendingBusy] = useState(false);
  const [resolveBusy, setResolveBusy] = useState(false);
  const [replyOpId, setReplyOpId] = useState<string>(() => createClientOperationId());
  const [optimisticByConv, setOptimisticByConv] = useState<Record<string, SupportMessageDto[]>>({});

  const markingReadRef = useRef<string | null>(null);
  const messagesEndRef = useRef<HTMLDivElement | null>(null);

  const { connectionState, reconnectAndSync } = useSupportRealtime("superadmin", {
    enabled: user?.role === "SuperAdmin",
    activeConversationId: selectedId,
  });

  const listQuery = useSupportInboxList("superadmin", {
    enabled: user?.role === "SuperAdmin",
    connectionState,
  });

  const detailQuery = useSupportConversationDetail("superadmin", selectedId, {
    enabled: user?.role === "SuperAdmin" && Boolean(selectedId),
    connectionState,
  });

  const conversations = useMemo(
    () => listQuery.data?.conversations ?? [],
    [listQuery.data?.conversations],
  );

  useEffect(() => {
    if (initialConversationId) {
      setSelectedId(initialConversationId);
    } else if (!selectedId && conversations.length > 0) {
      setSelectedId(conversations[0].id);
    }
  }, [initialConversationId, selectedId, conversations]);

  const activeSummary = useMemo(
    () =>
      detailQuery.data?.conversation ??
      conversations.find((c) => c.id === selectedId) ??
      null,
    [detailQuery.data?.conversation, conversations, selectedId],
  );

  // Auto mark-as-read when Super Admin views a conversation with unread restaurant messages
  useEffect(() => {
    if (!activeSummary || activeSummary.superAdminUnreadCount <= 0) {
      return;
    }
    const key = `${activeSummary.id}:${activeSummary.version}`;
    if (markingReadRef.current === key) return;
    markingReadRef.current = key;

    markSuperAdminSupportRead(activeSummary.id)
      .then((updated) => {
        const listKey = getSupportListQueryKey("superadmin", "platform");
        queryClient.setQueryData<SupportSyncResponseDto>(listKey, (old) => {
          if (!old) return old;
          const next = upsertConversationSummary(old.conversations, updated, "superadmin");
          return {
            ...old,
            conversations: next,
            totalUnreadCount: next.reduce((acc, c) => acc + c.superAdminUnreadCount, 0),
          };
        });
        const detailKey = getSupportDetailQueryKey("superadmin", activeSummary.id);
        queryClient.setQueryData<SupportConversationDetailDto>(detailKey, (old) =>
          old ? { ...old, conversation: updated } : old,
        );
      })
      .catch(() => {
        markingReadRef.current = null;
      });
  }, [activeSummary, queryClient]);

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
      if (filter === "Escalated") {
        if (!(c.isEscalated && c.status === "AwaitingSupport")) return false;
      } else if (filter !== "All" && c.status !== filter) {
        return false;
      }
      if (!q) return true;
      return (
        c.restaurantName.toLowerCase().includes(q) ||
        c.subject.toLowerCase().includes(q) ||
        (c.lastMessagePreview || "").toLowerCase().includes(q)
      );
    });
  }, [conversations, filter, searchText]);

  const escalatedCount = useMemo(
    () => conversations.filter((c) => c.isEscalated && c.status === "AwaitingSupport").length,
    [conversations],
  );
  const awaitingCount = useMemo(
    () => conversations.filter((c) => c.status === "AwaitingSupport").length,
    [conversations],
  );

  const handleSendReply = async (e?: React.FormEvent, retryOpId?: string, retryText?: string) => {
    e?.preventDefault();
    if (!selectedId) return;
    const text = (retryText ?? replyDraft).trim();
    if (!text) return;

    const opId = retryOpId ?? replyOpId;
    const optimisticMsg: SupportMessageDto = {
      id: `opt-${opId}`,
      conversationId: selectedId,
      senderType: "SuperAdmin",
      senderName: "Tablo Support",
      senderEmail: user?.email || "superadmin@tablo.local",
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
      const result = await replySuperAdminSupportConversation(selectedId, {
        message: text,
        clientOperationId: opId,
      });

      setOptimisticByConv((prev) => ({
        ...prev,
        [selectedId]: (prev[selectedId] ?? []).filter((m) => m.clientOperationId !== opId),
      }));

      const detailKey = getSupportDetailQueryKey("superadmin", selectedId);
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

      const listKey = getSupportListQueryKey("superadmin", "platform");
      queryClient.setQueryData<SupportSyncResponseDto>(listKey, (old) => {
        if (!old) return old;
        const next = upsertConversationSummary(old.conversations, result.conversation, "superadmin");
        return {
          ...old,
          conversations: next,
          totalUnreadCount: next.reduce((acc, c) => acc + c.superAdminUnreadCount, 0),
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
      toast.error(err?.message || "Failed to send support reply.");
    } finally {
      setSendingBusy(false);
    }
  };

  const handleResolve = async () => {
    if (!selectedId) return;
    setResolveBusy(true);
    try {
      const updated = await resolveSuperAdminSupportConversation(selectedId);
      const listKey = getSupportListQueryKey("superadmin", "platform");
      queryClient.setQueryData<SupportSyncResponseDto>(listKey, (old) => {
        if (!old) return old;
        const next = upsertConversationSummary(old.conversations, updated, "superadmin");
        return {
          ...old,
          conversations: next,
          totalUnreadCount: next.reduce((acc, c) => acc + c.superAdminUnreadCount, 0),
        };
      });
      const detailKey = getSupportDetailQueryKey("superadmin", selectedId);
      queryClient.setQueryData<SupportConversationDetailDto>(detailKey, (old) =>
        old ? { ...old, conversation: updated } : old,
      );
      toast.success("Ticket resolved.");
    } catch (err: any) {
      toast.error(err?.message || "Could not resolve conversation.");
    } finally {
      setResolveBusy(false);
    }
  };

  return (
    <div className="flex h-[calc(100dvh-3rem)] min-h-[540px] flex-col overflow-hidden rounded-2xl border border-border bg-card shadow-xs">
      <header className="flex flex-wrap items-center justify-between gap-3 border-b border-border bg-card px-5 py-3.5">
        <div className="flex items-center gap-3">
          <span className="grid h-9 w-9 place-items-center rounded-xl bg-primary/10 text-primary">
            <Inbox className="h-5 w-5" />
          </span>
          <div>
            <div className="flex flex-wrap items-center gap-2">
              <h2 className="text-lg font-bold tracking-tight">Support Inbox</h2>
              {(listQuery.data?.totalUnreadCount ?? 0) > 0 && (
                <Badge variant="default" data-testid="sa-total-unread">
                  {listQuery.data?.totalUnreadCount} unread
                </Badge>
              )}
              {awaitingCount > 0 && (
                <Badge variant="outline" className="border-amber-300 bg-amber-50 text-amber-800">
                  {awaitingCount} awaiting reply
                </Badge>
              )}
              {escalatedCount > 0 && (
                <Badge variant="outline" className="border-rose-300 bg-rose-50 text-rose-700 font-bold">
                  {escalatedCount} escalated
                </Badge>
              )}
            </div>
            <p className="text-xs text-muted-foreground">
              Cross-tenant restaurant support queue with 10-minute emergency escalation tracking
            </p>
          </div>
        </div>

        <SupportConnectionBadge
          state={connectionState}
          onReconnect={() => {
            void reconnectAndSync();
            void listQuery.refetch();
            if (selectedId) void detailQuery.refetch();
          }}
        />
      </header>

      <div className="grid min-h-0 flex-1 grid-cols-1 lg:grid-cols-[360px_1fr]">
        {/* Inbox Queue List */}
        <aside className="flex min-h-0 flex-col border-b border-border lg:border-b-0 lg:border-r">
          <div className="space-y-2 border-b border-border p-3">
            <div className="relative">
              <Search className="pointer-events-none absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                value={searchText}
                onChange={(e) => setSearchText(e.target.value)}
                placeholder="Search restaurant, subject, message..."
                className="h-9 pl-8 text-xs"
                data-testid="sa-inbox-search"
              />
            </div>
            <div className="flex flex-wrap gap-1">
              {(
                [
                  ["All", "All"],
                  ["AwaitingSupport", "Awaiting Support"],
                  ["Escalated", "Escalated"],
                  ["AwaitingRestaurant", "Awaiting Restaurant"],
                  ["Resolved", "Resolved"],
                ] as const
              ).map(([key, label]) => (
                <button
                  key={key}
                  type="button"
                  data-testid={`sa-filter-${key}`}
                  onClick={() => setFilter(key)}
                  className={`rounded-md px-2 py-1 text-[11px] font-medium transition ${
                    filter === key
                      ? "bg-primary text-primary-foreground"
                      : "bg-secondary text-muted-foreground hover:text-foreground"
                  }`}
                >
                  {label}
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
                No conversations match the current filter.
              </div>
            ) : (
              filteredConversations.map((conv) => {
                const isSelected = conv.id === selectedId;
                return (
                  <button
                    key={conv.id}
                    type="button"
                    data-testid={`sa-conv-item-${conv.id}`}
                    onClick={() => setSelectedId(conv.id)}
                    className={`w-full space-y-1.5 p-3.5 text-left transition ${
                      isSelected ? "bg-primary/5 ring-1 ring-inset ring-primary/20" : "hover:bg-secondary/50"
                    }`}
                  >
                    <div className="flex items-center justify-between gap-2 text-[11px] font-semibold text-primary">
                      <span className="inline-flex items-center gap-1 truncate">
                        <Building2 className="h-3 w-3 shrink-0" />
                        {conv.restaurantName}
                      </span>
                      {conv.superAdminUnreadCount > 0 && (
                        <Badge
                          variant="default"
                          className="h-5 min-w-5 shrink-0 justify-center px-1.5 text-[10px]"
                          data-testid={`sa-conv-unread-${conv.id}`}
                        >
                          {conv.superAdminUnreadCount}
                        </Badge>
                      )}
                    </div>

                    <div className="line-clamp-1 text-sm font-semibold text-foreground">
                      {conv.subject}
                    </div>

                    {conv.lastMessagePreview && (
                      <p className="line-clamp-1 text-xs text-muted-foreground">
                        {conv.lastMessagePreview}
                      </p>
                    )}

                    <div className="flex flex-wrap items-center justify-between gap-1 pt-0.5">
                      <SupportStatusBadge status={conv.status} isEscalated={conv.isEscalated} />
                      <div className="flex items-center gap-2 text-[10px] text-muted-foreground">
                        {conv.status === "AwaitingSupport" && conv.awaitingSupportSinceUtc && (
                          <span className="font-semibold text-amber-700">
                            <LiveWaitingTimer sinceUtc={conv.awaitingSupportSinceUtc} />
                          </span>
                        )}
                        <span>{formatSupportTimestamp(conv.lastActivityAtUtc)}</span>
                      </div>
                    </div>
                  </button>
                );
              })
            )}
          </div>
        </aside>

        {/* Active Conversation Thread */}
        <section className="flex min-h-0 flex-col bg-background">
          {!selectedId || !activeSummary ? (
            <div className="flex flex-1 flex-col items-center justify-center p-8 text-center">
              <Inbox className="mb-3 h-10 w-10 text-muted-foreground/40" />
              <h3 className="text-base font-semibold">Select a conversation</h3>
              <p className="mt-1 max-w-sm text-xs text-muted-foreground">
                Pick a restaurant support thread from the left to view full history, respond live, or resolve.
              </p>
            </div>
          ) : (
            <>
              <div className="flex flex-wrap items-center justify-between gap-3 border-b border-border bg-card px-5 py-3.5">
                <div className="space-y-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <Badge variant="outline" className="gap-1 text-xs font-semibold">
                      <Building2 className="h-3 w-3 text-primary" />
                      {activeSummary.restaurantName}
                    </Badge>
                    <h3 className="text-base font-bold" data-testid="sa-active-conv-subject">
                      {activeSummary.subject}
                    </h3>
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
                        Escalated to support@neorizsolutions.com at{" "}
                        {formatSupportTimestamp(activeSummary.escalatedAtUtc)}
                      </span>
                    )}
                    {(detailQuery.data?.escalationLogs?.length ?? 0) > 0 && (
                      <span className="inline-flex items-center gap-1">
                        <Mail className="h-3.5 w-3.5" />
                        Escalation status: {detailQuery.data!.escalationLogs[0].status}
                      </span>
                    )}
                  </div>
                </div>

                {activeSummary.status !== "Resolved" && (
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    disabled={resolveBusy}
                    onClick={handleResolve}
                    data-testid="sa-resolve-btn"
                  >
                    <CheckCircle2 className="mr-1.5 h-4 w-4 text-emerald-600" />
                    Resolve Ticket
                  </Button>
                )}
              </div>

              <div className="min-h-0 flex-1 space-y-3 overflow-y-auto p-5" data-testid="sa-message-thread">
                {detailQuery.isLoading ? (
                  <div className="p-8 text-center">
                    <Loader2 className="mx-auto h-5 w-5 animate-spin text-primary" />
                  </div>
                ) : (
                  mergedMessages.map((msg) => {
                    const isMine = msg.senderType === "SuperAdmin";
                    return (
                      <div
                        key={msg.localStatus ? `local-${msg.clientOperationId}` : msg.id}
                        className={`flex flex-col ${isMine ? "items-end" : "items-start"}`}
                        data-testid={`sa-msg-${msg.id}`}
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
                              {isMine
                                ? `Tablo Support (${msg.senderName})`
                                : `${activeSummary.restaurantName} · ${msg.senderName}`}
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

              <form onSubmit={(e) => void handleSendReply(e)} className="border-t border-border bg-card p-4">
                <div className="flex flex-col gap-2 sm:flex-row">
                  <Textarea
                    value={replyDraft}
                    onChange={(e) => setReplyDraft(e.target.value)}
                    placeholder={`Reply to ${activeSummary.restaurantName}... (ends any active escalation countdown)`}
                    rows={2}
                    className="min-h-[64px] flex-1 resize-none text-sm"
                    data-testid="sa-reply-input"
                  />
                  <Button
                    type="submit"
                    disabled={sendingBusy || !replyDraft.trim()}
                    className="shrink-0 self-end"
                    data-testid="sa-send-reply-btn"
                  >
                    {sendingBusy && <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />}
                    <Send className="mr-1.5 h-4 w-4" />
                    Send Reply
                  </Button>
                </div>
              </form>
            </>
          )}
        </section>
      </div>
    </div>
  );
}
