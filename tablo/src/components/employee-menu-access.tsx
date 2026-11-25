import { ApiError } from "@/services/apiClient";
import { Switch } from "@/components/ui/switch";
import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/hooks/use-auth";
import { useEffectiveAccess } from "@/hooks/use-effective-access";
import { getEmployeeMenuAccess, saveEmployeeMenuAccess } from "@/hooks/use-navigation-access";
import { getEmployeeNavItems } from "@/config/navigation";
import type { Employee } from "@/services/api";
import { Button } from "@/components/ui/button";

export function EmployeeMenuAccess({ employee }: { employee: Employee }) {
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
  const [saved, setSaved] = useState(false);
  const value = draft === undefined ? query.data?.allowedMenuIds : draft;
  const recovery = ["Admin", "Owner"].includes(employee.role);
  const isProtected = (id: string) =>
    recovery && ["staff-management", "staff-menu-access"].includes(id);
  const items = getEmployeeNavItems(employee.role, null).filter(
    (item) =>
      !item.requiredCapabilities?.length ||
      item.requiredCapabilities.some(entitlement.hasCapability),
  );
  const checkedIds = value === null ? items.map((item) => item.id) : value || [];
  function change(next: string[] | null) {
    setDraft(next);
    setSaved(false);
    setError("");
  }
  async function save() {
    if (value === undefined) return;
    setBusy(true);
    setError("");
    setSaved(false);
    try {
      const normalized =
        value === null
          ? null
          : Array.from(
              new Set([
                ...value.filter((id) => items.some((item) => item.id === id)),
                ...(recovery ? ["staff-management", "staff-menu-access"] : []),
              ]),
            );
      await saveEmployeeMenuAccess(employee.id, normalized);
      await Promise.all([
        client.invalidateQueries({ queryKey: ["employee-menu-access"] }),
        client.invalidateQueries({ queryKey: ["navigation-access"] }),
      ]);
      setDraft(undefined);
      setSaved(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unable to save menu access.");
    } finally {
      setBusy(false);
    }
  }
  return (
    <section
      className="space-y-5 rounded-2xl border border-border bg-card p-6"
      aria-label="Employee menu permissions"
    >
      <div>
        <h2 className="text-xl font-semibold">
          {employee.firstName} {employee.lastName}
        </h2>
        <p className="text-sm text-muted-foreground">
          {employee.email} · {employee.role}
        </p>
      </div>
      {query.isPending || entitlement.isLoading ? (
        <p role="status">Loading access…</p>
      ) : query.isError || entitlement.isError ? (
        <div role="alert">
          <p>
            {query.error instanceof ApiError && query.error.status === 404
              ? "Staff menu access requires the latest server update. Existing role menus remain available."
              : "Unable to load menu access."}
          </p>
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
          <p className="text-sm text-muted-foreground">
            {value === null
              ? "Using role defaults. All menus available to this employee’s role and restaurant plan are enabled."
              : "Using custom menu access."}{" "}
            Toggle a menu to customize access.
            {recovery &&
              " Staff & Roles and Staff Menu Access always remain enabled for administrators."}
          </p>
          <fieldset disabled={busy} className="divide-y divide-border">
            <legend className="mb-3 font-medium">Visible menus</legend>
            {items.map((item) => (
              <label key={item.id} className="flex items-center justify-between gap-4 py-3 text-sm">
                <span>{item.label}</span>
                <Switch
                  aria-label={item.label}
                  checked={checkedIds.includes(item.id) || isProtected(item.id)}
                  disabled={isProtected(item.id)}
                  onCheckedChange={(checked) =>
                    change(
                      checked
                        ? [...checkedIds, item.id]
                        : checkedIds.filter((id) => id !== item.id),
                    )
                  }
                />
              </label>
            ))}
          </fieldset>
          {items.length === 0 && (
            <p className="text-sm text-muted-foreground">
              No menus are available for this role and plan.
            </p>
          )}
          {error && (
            <p role="alert" className="text-sm text-destructive">
              {error}
            </p>
          )}
          {saved && (
            <p role="status" className="text-sm text-success">
              Menu access saved.
            </p>
          )}
          <div className="flex flex-wrap justify-end gap-2">
            <Button
              variant="outline"
              disabled={busy || value === null}
              onClick={() => change(null)}
            >
              Reset to role defaults
            </Button>
            <Button disabled={busy || value === undefined} onClick={() => void save()}>
              {busy ? "Saving…" : "Save access"}
            </Button>
          </div>
        </>
      )}
    </section>
  );
}
