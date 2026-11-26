import { confirmClose } from "@/lib/confirmation";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Lock, Sparkles, CheckCircle2, ArrowRight } from "lucide-react";

interface FeatureLockedModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  featureName: string;
  requiredPlan: "Plus" | "Premium";
  description?: string;
  benefits?: string[];
}

export function FeatureLockedModal({
  open,
  onOpenChange,
  featureName,
  requiredPlan,
  description,
  benefits = [],
}: FeatureLockedModalProps) {
  const defaultBenefits =
    requiredPlan === "Premium"
      ? [
          "Real-time ingredient inventory & stock tracking",
          "Automated recipe costing & Thalassery biryani yields",
          "Purchase orders, receiving & invoice reconciliation",
          "Lots, expiry dates & multi-outlet transfers",
          "Automated gross profit & valuation reports",
        ]
      : [
          "Staff table ordering & real-time floor ops",
          "Dining groups & shared bill splitting",
          "Mixed payment modes (Cash, UPI, Card)",
          "Seamless table transfers & live recovery",
          "Advanced payment reconciliation reports",
        ];

  const items = benefits.length > 0 ? benefits : defaultBenefits;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader className="text-left">
          <div className="mb-2 flex h-12 w-12 items-center justify-center rounded-2xl bg-primary/10 text-primary">
            <Lock className="h-6 w-6" />
          </div>
          <DialogTitle className="flex items-center gap-2 text-xl font-bold">
            <span>{featureName} is locked</span>
            <span className="rounded-full bg-primary/15 px-2.5 py-0.5 text-xs font-semibold text-primary">
              Requires {requiredPlan}
            </span>
          </DialogTitle>
          <DialogDescription className="text-sm text-muted-foreground pt-1">
            {description ||
              `This feature is part of Tablo's ${requiredPlan} tier. Upgrade your restaurant subscription to unlock this capability.`}
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-3 py-2">
          <p className="text-xs font-bold uppercase tracking-wider text-muted-foreground">
            What you get with {requiredPlan}:
          </p>
          <ul className="space-y-2">
            {items.map((b, i) => (
              <li key={i} className="flex items-start gap-2 text-xs font-medium text-foreground">
                <CheckCircle2 className="mt-0.5 h-3.5 w-3.5 shrink-0 text-primary" />
                <span>{b}</span>
              </li>
            ))}
          </ul>
        </div>

        <DialogFooter className="flex flex-col gap-2 sm:flex-row sm:justify-end">
          <Button variant="outline" onClick={() => void confirmClose(() => onOpenChange(false))}>
            Close
          </Button>
          <Button
            onClick={() => {
              onOpenChange(false);
              window.open("mailto:support@tablo.com?subject=Upgrade%20to%20" + requiredPlan, "_blank");
            }}
            className="flex items-center gap-1.5"
          >
            <Sparkles className="h-4 w-4" />
            Contact to Upgrade
            <ArrowRight className="h-3.5 w-3.5" />
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
