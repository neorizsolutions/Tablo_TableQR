import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { CheckCircle2, ShieldCheck, Sparkles, Building2 } from "lucide-react";
import type { EffectiveAccess } from "@/services/subscription";

interface SubscriptionDetailsModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  access?: EffectiveAccess | null;
  restaurantName?: string;
}

export function SubscriptionDetailsModal({
  open,
  onOpenChange,
  access,
  restaurantName,
}: SubscriptionDetailsModalProps) {
  if (!access) return null;

  const { plan, capabilities, limits } = access;
  const status = plan?.status || "Active";
  const planName = plan?.name || "Standard";
  const planCode = plan?.code || "basic";
  const statusColor =
    status === "Active"
      ? "bg-success/15 text-success border-success/30"
      : status === "Trial"
      ? "bg-warning/15 text-warning border-warning/30"
      : "bg-destructive/15 text-destructive border-destructive/30";

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader className="text-left">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <span className="grid h-10 w-10 place-items-center rounded-xl bg-primary/10 text-primary">
                <ShieldCheck className="h-5 w-5" />
              </span>
              <div>
                <DialogTitle className="text-lg font-bold">Restaurant Subscription</DialogTitle>
                <DialogDescription className="text-xs text-muted-foreground flex items-center gap-1.5 mt-0.5">
                  <Building2 className="h-3.5 w-3.5" /> {restaurantName || "My Restaurant"}
                </DialogDescription>
              </div>
            </div>
            <Badge variant="outline" className={`font-semibold capitalize ${statusColor}`}>
              {status}
            </Badge>
          </div>
        </DialogHeader>

        <div className="space-y-4 py-2">
          {/* Plan Summary Card */}
          <div className="rounded-xl border border-border bg-card p-4 shadow-sm">
            <div className="flex items-center justify-between">
              <div>
                <span className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                  Current Tier
                </span>
                <h3 className="text-2xl font-black text-foreground capitalize flex items-center gap-2">
                  {planName}
                  {planCode === "premium" && (
                    <span className="rounded-full bg-primary/10 px-2 py-0.5 text-[10px] font-bold text-primary">
                      All Features
                    </span>
                  )}
                </h3>
              </div>
              <div className="text-right">
                <span className="text-xs text-muted-foreground">Pricing</span>
                <p className="text-lg font-bold text-foreground">
                  ₹{plan?.monthlyPriceInr?.toLocaleString("en-IN") || 0}
                  <span className="text-xs font-normal text-muted-foreground">/mo</span>
                </p>
              </div>
            </div>
            <p className="mt-2 text-xs text-muted-foreground">{plan?.description}</p>

            {plan && (
              <div className="mt-3 grid grid-cols-2 gap-2 border-t border-border/60 pt-3 text-xs">
                <div>
                  <span className="text-muted-foreground">Started:</span>{" "}
                  <span className="font-medium text-foreground">
                    {plan.startDateUtc ? new Date(plan.startDateUtc).toLocaleDateString() : "Active"}
                  </span>
                </div>
                <div>
                  <span className="text-muted-foreground">Renewal / End:</span>{" "}
                  <span className="font-medium text-foreground">
                    {plan.endDateUtc
                      ? new Date(plan.endDateUtc).toLocaleDateString()
                      : "Continuous"}
                  </span>
                </div>
              </div>
            )}
          </div>

          {/* Limits */}
          {limits && (
            <div className="grid grid-cols-2 gap-3 text-xs">
              <div className="rounded-lg border border-border bg-secondary/30 p-3">
                <p className="text-muted-foreground">Max Outlets Allowed</p>
                <p className="text-base font-bold text-foreground">{limits.maxOutlets}</p>
              </div>
              <div className="rounded-lg border border-border bg-secondary/30 p-3">
                <p className="text-muted-foreground">Max Tables Allowed</p>
                <p className="text-base font-bold text-foreground">{limits.maxTables}</p>
              </div>
            </div>
          )}

          {/* Entitled Capabilities */}
          <div>
            <p className="text-xs font-bold uppercase tracking-wider text-muted-foreground mb-2">
              Active Entitlements ({capabilities.length})
            </p>
            <div className="max-h-44 overflow-y-auto space-y-1.5 rounded-lg border border-border/60 bg-secondary/20 p-2.5">
              {capabilities.map((cap) => (
                <div key={cap} className="flex items-center gap-2 text-xs font-medium text-foreground">
                  <CheckCircle2 className="h-3.5 w-3.5 text-primary shrink-0" />
                  <span className="capitalize">{cap.replace(/_/g, " ")}</span>
                </div>
              ))}
            </div>
          </div>
        </div>

        <div className="flex justify-between items-center pt-2 border-t">
          <p className="text-[11px] text-muted-foreground">Need changes? Contact your Super Administrator.</p>
          <Button variant="outline" size="sm" onClick={() => onOpenChange(false)}>
            Dismiss
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
