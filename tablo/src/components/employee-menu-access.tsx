import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Lock, RotateCcw, Save, ShieldCheck } from "lucide-react";
import { toast } from "sonner";
import {
  MENU_SECTION_LABELS,
  MENU_SECTIONS,
  getAssignableMenuItems,
  getEmployeeNavItems,
  getMenuRequiredPlanLabel,
  isAdminRole,
  RECOVERY_MENU_IDS,
  type NavItem,
} from "@/config/navigation";
import { getEmployeeMenuAccess, saveEmployeeMenuAccess } from "@/hooks/use-navigation-access";
import { useEffectiveAccess } from "@/hooks/use-effective-access";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Checkbox } from "@/components/ui/checkbox";

export function EmployeeMenuAccess({
  employee,
  onClose,
}: {
  employee: { id: string; firstName: string; lastName: string; role: string };
  onClose?: () => void;
}) {
  const cache = useQueryClient();
  const entitlement = useEffectiveAccess();
  const query = useQuery({
    queryKey: ["employee-menu-access", employee.id],
    queryFn: () => getEmployeeMenuAccess(employee.id),
  });
  const [draft, setDraft] = useState<string[] | null | undefined>(undefined);
  const value = draft === undefined ? query.data?.allowedMenuIds : draft;
  const recovery = isAdminRole(employee.role);
  const isProtected = (id: string) => recovery && RECOVERY_MENU_IDS.has(id);

  const items = useMemo(() => getAssignableMenuItems(employee.role), [employee.role]);

  const groupedItems = useMemo(
    () =>
      MENU_SECTIONS.map((section) => ({
        section,
        label: MENU_SECTION_LABELS[section],
        items: items.filter((item) => item.section === section),
      })).filter((group) => group.items.length > 0),
    [items],
  );

  const isAvailableInPlan = (item: NavItem) =>
    !item.requiredCapabilities?.length ||
    item.requiredCapabilities.some((cap) => entitlement.hasCapability(cap));

  const effectiveDefaultIds = useMemo(
    () => getEmployeeNavItems(employee.role, null).map((item) => item.id),
    [employee.role],
  );

  const effectiveCheckedSet = useMemo(() => {
    const rawIds =
      value === null
        ? effectiveDefaultIds
        : value === undefined
          ? []
          : value;
    const result = new Set<string>(rawIds);
    if (recovery) {
      RECOVERY_MENU_IDS.forEach((id) => result.add(id));
    }
    return result;
  }, [effectiveDefaultIds, recovery, value]);

  const effectivePlanReadyCount = useMemo(
    () =>
      items.filter((item) => effectiveCheckedSet.has(item.id) && isAvailableInPlan(item)).length,
    [effectiveCheckedSet, items, entitlement.capabilities],
  );

  const mutation = useMutation({
    mutationFn: (next: string[] | null) => saveEmployeeMenuAccess(employee.id, next),
    onSuccess: (saved) => {
      cache.setQueryData(["employee-menu-access", employee.id], saved);
      void cache.invalidateQueries({ queryKey: ["navigation-access"] });
      void cache.invalidateQueries({ queryKey: ["effective_access"] });
      setDraft(undefined);
      toast.success(`Saved menu access for ${employee.firstName}`);
      onClose?.();
    },
    onError: (e: Error) => toast.error(e.message),
  });

  const buildSanitizedSelection = (baseIds: Iterable<string>) => {
    const availableSet = new Set(items.filter(isAvailableInPlan).map((item) => item.id));
    return [
      ...new Set([
        ...Array.from(baseIds).filter((id) => availableSet.has(id) || isProtected(id)),
        ...(recovery ? Array.from(RECOVERY_MENU_IDS) : []),
      ]),
    ];
  };

  const toggle = (item: NavItem) => {
    if (isProtected(item.id) || !isAvailableInPlan(item)) return;
    const base = new Set(effectiveCheckedSet);
    if (base.has(item.id)) base.delete(item.id);
    else base.add(item.id);
    setDraft(buildSanitizedSelection(base));
  };

  const reset = () => {
    setDraft(null);
  };

  const save = () => {
    if (value === undefined) return;
    const payload = value === null ? null : buildSanitizedSelection(value);
    mutation.mutate(payload);
  };

  const unavailableCount = items.filter((item) => !isAvailableInPlan(item)).length;
  const currentPlanLabel = entitlement.plan?.name || "Current plan";

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-border bg-secondary/40 px-3.5 py-2.5 text-xs">
        <div className="flex flex-wrap items-center gap-2">
          <Badge variant={value === null ? "secondary" : "default"}>
            {value === null ? `${employee.role} defaults active` : "Custom permissions"}
          </Badge>
          <span className="text-muted-foreground">
            {effectivePlanReadyCount} active menu{effectivePlanReadyCount === 1 ? "" : "s"} enabled
          </span>
          {unavailableCount > 0 && (
            <span className="text-muted-foreground">
              · {unavailableCount} gated by {currentPlanLabel}
            </span>
          )}
        </div>
        {!recovery && (
          <span className="inline-flex items-center gap-1 text-muted-foreground">
            <ShieldCheck className="h-3.5 w-3.5 text-primary" />
            Admin &amp; Support menus remain Admin-only
          </span>
        )}
      </div>

      {query.isPending || entitlement.isLoading ? (
        <p className="text-sm text-muted-foreground">Loading employee menu permissions...</p>
      ) : query.isError ? (
        <p className="text-sm text-destructive">Menu access failed to load. Close and try again.</p>
      ) : (
        <>
          <div className="max-h-[420px] space-y-5 overflow-y-auto pr-1">
            {groupedItems.map((group) => (
              <div key={group.section} className="space-y-2">
                <div className="flex items-center justify-between border-b border-border/60 pb-1">
                  <h3 className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                    {group.label}
                  </h3>
                  <span className="text-[11px] text-muted-foreground">
                    {group.items.filter((i) => effectiveCheckedSet.has(i.id) && isAvailableInPlan(i)).length}/{group.items.length}
                  </span>
                </div>
                <div className="grid gap-2 sm:grid-cols-2">
                  {group.items.map((item) => {
                    const available = isAvailableInPlan(item);
                    const checked = effectiveCheckedSet.has(item.id);
                    const lockedRecovery = isProtected(item.id);
                    const disabled = lockedRecovery || !available || mutation.isPending;
                    const planReq = !available ? getMenuRequiredPlanLabel(item) : null;

                    return (
                      <label
                        key={item.id}
                        data-menu-id={item.id}
                        data-plan-available={available ? "true" : "false"}
                        className={`flex items-start gap-2.5 rounded-xl border p-2.5 text-sm transition ${
                          !available
                            ? "cursor-not-allowed border-border/60 bg-secondary/40 opacity-70"
                            : lockedRecovery
                              ? "border-primary/30 bg-primary/5"
                              : checked
                                ? "cursor-pointer border-primary/50 bg-primary/5 hover:bg-primary/10"
                                : "cursor-pointer border-border bg-card hover:bg-secondary/40"
                        }`}
                      >
                        <Checkbox
                          checked={checked}
                          disabled={disabled}
                          aria-label={item.label}
                          onCheckedChange={() => toggle(item)}
                          className="mt-0.5"
                        />
                        <div className="min-w-0 flex-1">
                          <div className="flex flex-wrap items-center gap-1.5">
                            <span className="font-medium leading-tight">{item.label}</span>
                            {lockedRecovery && (
                              <Badge variant="outline" className="text-[10px] px-1.5 py-0">
                                Protected recovery
                              </Badge>
                            )}
                            {planReq && (
                              <Badge variant="secondary" className="inline-flex items-center gap-1 text-[10px] px-1.5 py-0">
                                <Lock className="h-2.5 w-2.5" />
                                {planReq}
                              </Badge>
                            )}
                          </div>
                        </div>
                      </label>
                    );
                  })}
                </div>
              </div>
            ))}
          </div>

          <div className="flex flex-wrap justify-between gap-2 border-t pt-3">
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={reset}
              disabled={value === null || mutation.isPending}
            >
              <RotateCcw className="mr-1.5 h-3.5 w-3.5" /> Reset to {employee.role} defaults
            </Button>
            <Button
              type="button"
              size="sm"
              onClick={save}
              disabled={draft === undefined || mutation.isPending}
            >
              <Save className="mr-1.5 h-3.5 w-3.5" /> Save menu access
            </Button>
          </div>
        </>
      )}
    </div>
  );
}
