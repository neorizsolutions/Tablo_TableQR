import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/hooks/use-auth";
import { useEffectiveAccess } from "@/hooks/use-effective-access";
import { getEmployeeMenuAccess, saveEmployeeMenuAccess } from "@/hooks/use-navigation-access";
import { getEmployeeNavItems } from "@/config/navigation";
import type { Employee } from "@/services/api";
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";

export function EmployeeMenuAccess({
  employee,
  onClose,
}: {
  employee: Employee;
  onClose: () => void;
}) {
  const { user } = useAuth();
  const client = useQueryClient();
  const entitlement = useEffectiveAccess();
  const query = useQuery({
    queryKey: ["employee-menu-access", user?.restaurantId, user?.id, employee.id],
    queryFn: () => getEmployeeMenuAccess(employee.id),
    staleTime: 0,
  });
  const [draft, setDraft] = useState<string[] | null | undefined>(undefined);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const value = draft === undefined ? query.data?.allowedMenuIds : draft;
  const recovery = ["Admin", "Owner"].includes(employee.role);
  const items = getEmployeeNavItems(employee.role, null).filter(
    (item) =>
      !item.requiredCapabilities?.length ||
      item.requiredCapabilities.some(entitlement.hasCapability),
  );
  async function save() {
    if (value === undefined) return;
    setBusy(true);
    setError("");
    try {
      const normalized =
        value === null
          ? null
          : Array.from(
              new Set([
                ...value.filter((id) => items.some((item) => item.id === id)),
                ...(recovery ? ["staff-management"] : []),
              ]),
            );
      await saveEmployeeMenuAccess(employee.id, normalized);
      await Promise.all([
        client.invalidateQueries({ queryKey: ["employee-menu-access"] }),
        client.invalidateQueries({ queryKey: ["navigation-access"] }),
      ]);
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unable to save menu access.");
    } finally {
      setBusy(false);
    }
  }
  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open && !busy) onClose();
      }}
    >
      <DialogContent className="max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>
            Menu access · {employee.firstName} {employee.lastName}
          </DialogTitle>
        </DialogHeader>
        {query.isPending || entitlement.isLoading ? (
          <p role="status">Loading access…</p>
        ) : query.isError || entitlement.isError ? (
          <div role="alert">
            <p>Unable to load menu access.</p>
            <Button
              variant="outline"
              onClick={() => {
                void query.refetch();
                void entitlement.refetch();
              }}
            >
              Retry
            </Button>
          </div>
        ) : (
          <>
            <label className="flex gap-2 text-sm">
              <input
                type="checkbox"
                disabled={busy}
                checked={value === null}
                onChange={(e) => setDraft(e.target.checked ? null : items.map((item) => item.id))}
              />
              Use role defaults
            </label>
            <p className="text-sm text-muted-foreground">
              Custom access can only include menus available to this role and restaurant plan. Clear
              all optional menus to deny access.
              {recovery && " Staff & Roles always remains available to administrators."}
            </p>
            {value !== null && (
              <fieldset disabled={busy} className="space-y-2">
                <legend className="mb-2 font-medium">Menu access</legend>
                {items.map((item) => (
                  <label key={item.id} className="flex gap-2 text-sm">
                    <input
                      type="checkbox"
                      checked={
                        value?.includes(item.id) || (recovery && item.id === "staff-management")
                      }
                      disabled={recovery && item.id === "staff-management"}
                      onChange={(e) =>
                        setDraft(
                          e.target.checked
                            ? [...(value || []), item.id]
                            : (value || []).filter((id) => id !== item.id),
                        )
                      }
                    />
                    {item.label}
                  </label>
                ))}
              </fieldset>
            )}
            {error && (
              <p role="alert" className="text-sm text-destructive">
                {error}
              </p>
            )}
            <div className="flex justify-end gap-2">
              <Button variant="outline" disabled={busy} onClick={onClose}>
                Cancel
              </Button>
              <Button disabled={busy || value === undefined} onClick={() => void save()}>
                {busy ? "Saving…" : "Save access"}
              </Button>
            </div>
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}
