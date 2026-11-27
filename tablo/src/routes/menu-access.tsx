import { createFileRoute } from "@tanstack/react-router";
import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useAuth } from "@/hooks/use-auth";
import { useEmployeeCounts } from "@/hooks/use-staff";
import { getEmployees, type EmployeeStatusFilter } from "@/services/api";
import { AppLayout } from "@/components/app-layout";
import { EmployeeMenuAccess } from "@/components/employee-menu-access";
import { Button } from "@/components/ui/button";
import { WorkspaceStatus } from "@/components/workspace-status";

export const Route = createFileRoute("/menu-access")({
  head: () => ({ meta: [{ title: "Staff Menu Access — Tablo admin" }] }),
  component: MenuAccessPage,
});
function MenuAccessPage() {
  const { user, loading } = useAuth();
  if (loading) return <WorkspaceStatus state="loading" title="Checking your session" />;
  if (!user)
    return (
      <WorkspaceStatus
        state="sign-in"
        title="Sign in to Staff Menu Access"
        description="Use your restaurant admin account to manage which menus employees can open."
      />
    );
  if (!["Admin", "Owner"].includes(user.role))
    return (
      <AppLayout>
        <WorkspaceStatus
          state="restricted"
          title="Restaurant admin access required"
          description="Your restaurant administrator manages staff menu access."
        />
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
  const [statusFilter, setStatusFilter] = useState<EmployeeStatusFilter>("active");
  const [search, setSearch] = useState("");
  const [selectedId, setSelectedId] = useState<string | null>(null);

  const query = useQuery({
    queryKey: ["menu-access-employees", user?.restaurantId, user?.id, statusFilter],
    queryFn: () => getEmployees(statusFilter),
  });
  const countsQuery = useEmployeeCounts();

  const employees = (query.data || []).filter((employee) => employee.role !== "SuperAdmin");
  const normalizedSearch = search.trim().toLowerCase();
  const filtered = employees.filter((employee) =>
    `${employee.firstName} ${employee.lastName} ${employee.email} ${employee.role}`
      .toLowerCase()
      .includes(normalizedSearch),
  );
  const selected = filtered.find((employee) => employee.id === selectedId);

  useEffect(() => {
    if (selectedId && !query.isPending && !filtered.some((employee) => employee.id === selectedId)) {
      setSelectedId(null);
    }
  }, [selectedId, query.isPending, filtered]);

  const filterOptions: { value: EmployeeStatusFilter; label: string; count?: number }[] = [
    { value: "active", label: "Active", count: countsQuery.data?.active },
    { value: "inactive", label: "Inactive", count: countsQuery.data?.inactive },
    { value: "all", label: "All", count: countsQuery.data?.all },
  ];

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
        <div className="grid items-start gap-6 md:grid-cols-[300px_1fr]">
          <section
            aria-label="Employees"
            className="space-y-3 rounded-2xl border border-border bg-card p-4"
          >
            <div
              role="group"
              aria-label="Filter employees by status"
              className="grid grid-cols-3 gap-1 rounded-xl border border-border bg-secondary/40 p-1"
            >
              {filterOptions.map((opt) => {
                const isSelected = statusFilter === opt.value;
                return (
                  <button
                    key={opt.value}
                    type="button"
                    aria-pressed={isSelected}
                    data-status-filter={opt.value}
                    onClick={() => setStatusFilter(opt.value)}
                    className={`flex items-center justify-center gap-1 rounded-lg px-2 py-1.5 text-xs font-semibold transition ${
                      isSelected
                        ? "bg-primary text-primary-foreground shadow-sm"
                        : "text-muted-foreground hover:bg-background hover:text-foreground"
                    }`}
                  >
                    <span>{opt.label}</span>
                    {typeof opt.count === "number" && (
                      <span
                        className={`rounded-full px-1 text-[10px] font-bold ${
                          isSelected ? "bg-white/20 text-primary-foreground" : "bg-background text-foreground"
                        }`}
                      >
                        {opt.count}
                      </span>
                    )}
                  </button>
                );
              })}
            </div>

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
                <div className="py-4 text-sm text-muted-foreground">
                  <p>
                    {normalizedSearch
                      ? `No ${statusFilter === "all" ? "" : `${statusFilter} `}employees match "${search.trim()}".`
                      : statusFilter === "active"
                        ? "No active employees found."
                        : statusFilter === "inactive"
                          ? "No inactive employees found."
                          : "No employees found."}
                  </p>
                  {normalizedSearch && (
                    <button
                      type="button"
                      onClick={() => setSearch("")}
                      className="mt-2 text-xs font-semibold text-primary underline"
                    >
                      Clear search
                    </button>
                  )}
                </div>
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
