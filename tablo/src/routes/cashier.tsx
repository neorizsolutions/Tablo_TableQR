import { createFileRoute, Link } from "@tanstack/react-router";
import { useAuth } from "@/hooks/use-auth";
import { AppLayout } from "@/components/app-layout";
import { ServiceDesk } from "@/components/service-desk";

export const Route = createFileRoute("/cashier")({
  head: () => ({ meta: [{ title: "Cashier — Tablo" }] }),
  validateSearch: (search: Record<string, unknown>) => ({
    table: typeof search.table === "string" ? search.table : undefined,
    group: typeof search.group === "string" ? search.group : undefined,
  }),
  component: CashierPage,
});
function CashierPage() {
  const { user, loading } = useAuth();
  const search = Route.useSearch();
  if (loading)
    return (
      <p className="p-8" role="status">
        Loading…
      </p>
    );
  if (!user)
    return (
      <div className="p-8">
        <h1 className="text-xl font-semibold">Staff sign in required</h1>
        <Link to="/auth" className="underline">
          Sign in
        </Link>
      </div>
    );
  return (
    <AppLayout>
      {["Admin", "Owner", "Cashier"].includes(user.role) && user.restaurantId ? (
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
        <div className="p-8">
          <h1 className="text-xl font-semibold">Cashier access required</h1>
        </div>
      )}
    </AppLayout>
  );
}
