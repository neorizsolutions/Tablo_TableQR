import { createFileRoute, Link } from "@tanstack/react-router";
import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useAuth } from "@/hooks/use-auth";
import { getEmployees } from "@/services/api";
import { AppLayout } from "@/components/app-layout";
import { EmployeeMenuAccess } from "@/components/employee-menu-access";
import { Button } from "@/components/ui/button";

export const Route = createFileRoute("/menu-access")({
  head: () => ({ meta: [{ title: "Staff Menu Access — Tablo admin" }] }),
  component: MenuAccessPage,
});
function MenuAccessPage() {
  const { user, loading } = useAuth();
  if (loading)
    return (
      <p className="p-8" role="status">
        Loading…
      </p>
    );
  if (!user)
    return (
      <div className="p-8">
        <h1 className="text-xl font-semibold">Admin sign in required</h1>
        <Link to="/auth" className="underline">
          Sign in
        </Link>
      </div>
    );
  if (!["Admin", "Owner"].includes(user.role))
    return (
      <AppLayout>
        <div className="p-8">
          <h1 className="text-xl font-semibold">Restaurant admin access required</h1>
          <p>Staff menu access is managed by your restaurant administrator.</p>
        </div>
      </AppLayout>
    );
  return (
    <AppLayout>
      <MenuAccessWorkspace />
    </AppLayout>
  );
}
function MenuAccessWorkspace() {
  const { user } = useAuth();
  const query = useQuery({
    queryKey: ["menu-access-employees", user?.restaurantId, user?.id],
    queryFn: getEmployees,
  });
  const [search, setSearch] = useState("");
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const employees = (query.data || []).filter((employee) => employee.role !== "SuperAdmin");
  const selected = employees.find((employee) => employee.id === selectedId);
  const filtered = employees.filter((employee) =>
    `${employee.firstName} ${employee.lastName} ${employee.email} ${employee.role}`
      .toLowerCase()
      .includes(search.toLowerCase()),
  );
  return (
    <div className="mx-auto max-w-6xl space-y-6 p-4 sm:p-8">
      <header>
        <h1 className="text-2xl font-semibold">Staff Menu Access</h1>
        <p className="mt-2 text-sm text-muted-foreground">
          Choose an employee and control which workspace menus they can see and open.
        </p>
      </header>
      {query.isPending ? (
        <p role="status">Loading employees…</p>
      ) : query.isError ? (
        <div role="alert">
          <p>Unable to load employees.</p>
          <Button variant="outline" onClick={() => void query.refetch()}>
            Retry
          </Button>
        </div>
      ) : (
        <div className="grid items-start gap-6 md:grid-cols-[280px_1fr]">
          <section
            aria-label="Employees"
            className="space-y-3 rounded-2xl border border-border bg-card p-4"
          >
            <label className="block text-sm font-medium">
              Search employees
              <input
                type="search"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                className="mt-2 w-full rounded-lg border border-border bg-background px-3 py-2 text-sm"
                placeholder="Name, email or role"
              />
            </label>
            <div className="max-h-[60vh] space-y-2 overflow-y-auto">
              {filtered.map((employee) => (
                <button
                  key={employee.id}
                  type="button"
                  aria-pressed={selectedId === employee.id}
                  onClick={() => setSelectedId(employee.id)}
                  className={`w-full rounded-xl border p-3 text-left text-sm ${selectedId === employee.id ? "border-primary bg-primary/10" : "border-border hover:bg-secondary"}`}
                >
                  <span className="block font-semibold">
                    {employee.firstName} {employee.lastName}
                  </span>
                  <span className="block break-all text-xs text-muted-foreground">
                    {employee.email}
                  </span>
                  <span className="text-xs text-muted-foreground">
                    {employee.role}
                    {!employee.isActive && " · Inactive"}
                  </span>
                </button>
              ))}
              {filtered.length === 0 && (
                <p className="py-4 text-sm text-muted-foreground">No employees found.</p>
              )}
            </div>
          </section>
          {selected ? (
            <EmployeeMenuAccess key={selected.id} employee={selected} />
          ) : (
            <div className="rounded-2xl border border-dashed border-border p-8 text-center text-muted-foreground">
              Select an employee to manage their menu access.
            </div>
          )}
        </div>
      )}
    </div>
  );
}
