import { createFileRoute } from "@tanstack/react-router";
import { useAuth } from "@/hooks/use-auth";
import { useNavigationAccess } from "@/hooks/use-navigation-access";
import { useEffectiveAccess } from "@/hooks/use-effective-access";
import { CAPABILITIES, getEmployeeNavItems } from "@/config/navigation";
import { AppLayout } from "@/components/app-layout";
import { ServiceDesk } from "@/components/service-desk";
import { WorkspaceStatus } from "@/components/workspace-status";
import { Button } from "@/components/ui/button";

export const Route = createFileRoute("/cashier")({
  head: () => ({ meta: [{ title: "Cashier — Tablo" }] }),
  validateSearch: (search: Record<string, unknown>) => ({
    table: typeof search.table === "string" ? search.table : undefined,
    group: typeof search.group === "string" ? search.group : undefined,
  }),
  component: CashierPage,
});
function CashierPage() {
  const { user, roles, loading } = useAuth();
  const permissions = useNavigationAccess();
  const entitlement = useEffectiveAccess();
  const search = Route.useSearch();
  if (loading) return <WorkspaceStatus state="loading" title="Checking your session" />;
  if (!user)
    return (
      <WorkspaceStatus
        state="sign-in"
        title="Sign in to Cashier"
        description="Use your staff account to review bills, collect payments and print receipts."
      />
    );
  const role = roles[0] || user.role;
  const canCheckAccess = !!user.restaurantId && user.role !== "SuperAdmin";
  const allowedItems = getEmployeeNavItems(role, permissions.data ? permissions.data.allowedMenuIds : []);
  const canCashier =
    !!user.restaurantId &&
    allowedItems.some((item) => item.id === "cashier-billing") &&
    entitlement.hasCapability(CAPABILITIES.StandardBilling);
  return (
    <AppLayout>
      {canCheckAccess && (permissions.isPending || entitlement.isLoading) ? (
        <WorkspaceStatus state="loading" title="Loading Cashier" />
      ) : canCheckAccess && (permissions.isError || entitlement.isError) ? (
        <WorkspaceStatus
          state="error"
          title="Cashier could not load"
          description="We couldn’t verify your access. Try again."
          action={(
            <Button variant="outline" onClick={() => {
              if (permissions.isError) void permissions.refetch();
              if (entitlement.isError) void entitlement.refetch();
            }}>
              Retry
            </Button>
          )}
        />
      ) : canCashier && user.restaurantId ? (
        <div className="mx-auto max-w-7xl space-y-6 p-4 sm:p-8">
          <header>
            <h1 className="text-2xl font-semibold">Cashier</h1>
            <p className="mt-2 text-sm text-muted-foreground">
              Review table bills, collect payments and print receipts.
            </p>
          </header>
          <ServiceDesk
            restaurantId={user.restaurantId}
            employeeId={user.id}
            initialTableId={search.table}
            initialGroupId={search.group}
            mode="bills-only"
          />
        </div>
      ) : (
        <WorkspaceStatus
          state="restricted"
          title="Cashier access required"
          description="Ask your restaurant administrator to check your Cashier access."
        />
      )}
    </AppLayout>
  );
}
