import { useEffect, useState } from "react";
import { AlertTriangle, CheckCircle2, Clock, RefreshCw, Wifi, WifiOff } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { formatWaitingDuration } from "@/lib/support-state";
import type {
  SupportConnectionState,
  SupportConversationStatus,
} from "@/services/support";

export function SupportConnectionBadge({
  state,
  onReconnect,
}: {
  state: SupportConnectionState;
  onReconnect?: () => void;
}) {
  const meta = {
    connecting: {
      label: "Connecting",
      className: "border-amber-200 bg-amber-50 text-amber-800",
      icon: RefreshCw,
      spin: true,
    },
    connected: {
      label: "Live",
      className: "border-emerald-200 bg-emerald-50 text-emerald-700",
      icon: Wifi,
      spin: false,
    },
    reconnecting: {
      label: "Reconnecting",
      className: "border-amber-200 bg-amber-50 text-amber-800",
      icon: RefreshCw,
      spin: true,
    },
    offline: {
      label: "Offline (Fallback sync)",
      className: "border-rose-200 bg-rose-50 text-rose-700",
      icon: WifiOff,
      spin: false,
    },
    revoked: {
      label: "Access Revoked",
      className: "border-destructive/40 bg-destructive/10 text-destructive",
      icon: AlertTriangle,
      spin: false,
    },
  }[state];

  const Icon = meta.icon;

  return (
    <div className="flex items-center gap-2">
      <Badge
        variant="outline"
        data-testid="support-connection-status"
        className={`gap-1.5 text-xs font-medium ${meta.className}`}
      >
        <Icon className={`h-3 w-3 ${meta.spin ? "animate-spin" : ""}`} />
        <span>{meta.label}</span>
      </Badge>
      {onReconnect && state !== "revoked" && (
        <Button
          type="button"
          variant="ghost"
          size="sm"
          onClick={onReconnect}
          className="h-7 px-2 text-xs text-muted-foreground hover:text-foreground"
          title="Sync and reconnect live updates"
        >
          <RefreshCw className="mr-1 h-3 w-3" />
          Sync
        </Button>
      )}
    </div>
  );
}

export function SupportStatusBadge({
  status,
  isEscalated,
}: {
  status: SupportConversationStatus;
  isEscalated?: boolean;
}) {
  const statusConfig = {
    AwaitingSupport: {
      label: "Awaiting Support",
      className: "border-amber-200 bg-amber-50 text-amber-800",
      icon: Clock,
    },
    AwaitingRestaurant: {
      label: "Awaiting Restaurant",
      className: "border-blue-200 bg-blue-50 text-blue-700",
      icon: Clock,
    },
    Resolved: {
      label: "Resolved",
      className: "border-emerald-200 bg-emerald-50 text-emerald-700",
      icon: CheckCircle2,
    },
  }[status];

  const Icon = statusConfig.icon;

  return (
    <div className="flex flex-wrap items-center gap-1.5">
      <Badge variant="outline" className={`gap-1 text-[11px] font-semibold ${statusConfig.className}`}>
        <Icon className="h-3 w-3" />
        {statusConfig.label}
      </Badge>
      {isEscalated && status === "AwaitingSupport" && (
        <Badge
          variant="outline"
          className="gap-1 border-rose-300 bg-rose-50 text-[11px] font-bold text-rose-700"
        >
          <AlertTriangle className="h-3 w-3" />
          Escalated (10m+)
        </Badge>
      )}
    </div>
  );
}

export function LiveWaitingTimer({ sinceUtc }: { sinceUtc?: string | null }) {
  const [nowMs, setNowMs] = useState(() => Date.now());

  useEffect(() => {
    if (!sinceUtc) return;
    const id = window.setInterval(() => setNowMs(Date.now()), 1000);
    return () => window.clearInterval(id);
  }, [sinceUtc]);

  if (!sinceUtc) return null;
  return <span>{formatWaitingDuration(sinceUtc, nowMs)}</span>;
}

export function formatSupportTimestamp(iso: string): string {
  try {
    return new Date(iso).toLocaleString("en-IN", {
      timeZone: "Asia/Kolkata",
      day: "2-digit",
      month: "short",
      hour: "2-digit",
      minute: "2-digit",
    });
  } catch {
    return iso;
  }
}
