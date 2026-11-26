import { confirmAction } from "@/lib/confirmation";
import { createFileRoute, Link } from "@tanstack/react-router";
import { useState } from "react";
import { Eye, EyeOff, Loader2, ShieldAlert, LogIn, LogOut, Plus, Pencil, Power, PowerOff, Trash2, Search, AlertTriangle, X } from "lucide-react";
import { toast } from "sonner";
import { SiteHeader } from "@/components/site-header";
import { AppLayout } from "@/components/app-layout";
import { useAuth } from "@/hooks/use-auth";
import { useStaff, useEmployeeCounts, useCreateEmployee, useUpdateEmployee, useUpdateEmployeeStatus, useDeleteEmployee } from "@/hooks/use-staff";
import { Employee, type EmployeeStatusFilter } from "@/services/api";

export const Route = createFileRoute("/staff")({
  head: () => ({
    meta: [
      { title: "Staff Management — Tablo admin" },
      { name: "description", content: "Manage staff accounts." },
    ],
  }),
  component: StaffPage,
});

function StaffPage() {
  const { user, roles, loading: authLoading, signOut } = useAuth();
  
  if (authLoading) {
    return (
      <div className="grid min-h-screen place-items-center bg-secondary/40">
        <Loader2 className="h-6 w-6 animate-spin text-primary" />
      </div>
    );
  }

  if (!user) return <SignInGate />;
  
  const isAdmin = roles.includes("Admin") || roles.includes("Owner");
  if (!isAdmin) return <NoAccessGate roles={roles} onSignOut={signOut} />;

  return <StaffInner currentUserEmail={user.email} />;
}

function SignInGate() {
  return (
    <div className="min-h-screen bg-secondary/40">
      <SiteHeader />
      <div className="mx-auto max-w-md px-4 py-16 text-center">
        <div className="rounded-3xl border border-border bg-card p-10 shadow-card">
          <span className="mx-auto grid h-12 w-12 place-items-center rounded-2xl bg-primary-soft text-primary">
            <LogIn className="h-5 w-5" />
          </span>
          <h1 className="mt-4 font-display text-2xl font-bold">Admin sign in required</h1>
          <p className="mt-2 text-sm text-muted-foreground">
            The staff panel is private. Sign in with an admin account to continue.
          </p>
          <Link to="/auth" className="mt-6 inline-flex items-center justify-center gap-2 rounded-full bg-gradient-primary px-5 py-3 text-sm font-semibold text-primary-foreground shadow-glow">
            Go to sign in
          </Link>
        </div>
      </div>
    </div>
  );
}

function NoAccessGate({ roles, onSignOut }: { roles: string[]; onSignOut: () => void }) {
  return (
    <div className="min-h-screen bg-secondary/40">
      <SiteHeader />
      <div className="mx-auto max-w-md px-4 py-16 text-center">
        <div className="rounded-3xl border border-border bg-card p-10 shadow-card">
          <span className="mx-auto grid h-12 w-12 place-items-center rounded-2xl bg-warning/15 text-warning">
            <ShieldAlert className="h-5 w-5" />
          </span>
          <h1 className="mt-4 font-display text-2xl font-bold">Access Denied</h1>
          <p className="mt-2 text-sm text-muted-foreground">
            Your account is signed in as <span className="font-semibold">{roles.join(", ") || "staff"}</span>. You must be an <span className="font-semibold">Admin</span> or <span className="font-semibold">Owner</span> to access this page.
          </p>
          <div className="mt-6 flex flex-col gap-3">
            <Link to="/dashboard" className="inline-flex items-center justify-center gap-2 rounded-full bg-primary px-4 py-2 text-sm font-semibold text-primary-foreground">
              Go to my Dashboard
            </Link>
            <button onClick={onSignOut} className="inline-flex items-center justify-center gap-2 rounded-full border border-border px-4 py-2 text-sm font-semibold">
              <LogOut className="h-4 w-4" /> Sign out
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

function StaffInner({ currentUserEmail }: { currentUserEmail: string }) {
  const [statusFilter, setStatusFilter] = useState<EmployeeStatusFilter>("active");
  const [search, setSearch] = useState("");

  const { data: employees, isLoading } = useStaff(statusFilter);
  const countsQuery = useEmployeeCounts();
  const { mutateAsync: updateStatus } = useUpdateEmployeeStatus();
  const { mutateAsync: deleteEmp, isPending: isDeleting } = useDeleteEmployee();

  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<Employee | null>(null);
  const [deleting, setDeleting] = useState<Employee | null>(null);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  const normalizedSearch = search.trim().toLowerCase();
  const filteredEmployees = (employees || []).filter((emp) => {
    if (!normalizedSearch) return true;
    return `${emp.firstName} ${emp.lastName} ${emp.email} ${emp.role}`
      .toLowerCase()
      .includes(normalizedSearch);
  });

  const filterOptions: { value: EmployeeStatusFilter; label: string; count?: number }[] = [
    { value: "active", label: "Active", count: countsQuery.data?.active },
    { value: "inactive", label: "Inactive", count: countsQuery.data?.inactive },
    { value: "all", label: "All", count: countsQuery.data?.all },
  ];

  const toggleStatus = async (employee: Employee) => {
    if (employee.email.toLowerCase() === currentUserEmail.toLowerCase()) {
      toast.error("You cannot deactivate your own account.");
      return;
    }

    const newStatus = !employee.isActive;
    if (!newStatus) {
      if (!await confirmAction(`Are you sure you want to deactivate ${employee.firstName} ${employee.lastName}? They will no longer be able to log in.`)) {
        return;
      }
    }

    try {
      await updateStatus({ id: employee.id, isActive: newStatus });
      toast.success(newStatus ? "Employee activated" : "Employee deactivated");
    } catch (err: any) {
      toast.error(err.message || "Failed to update status");
    }
  };

  const requestDelete = (employee: Employee) => {
    if (employee.email.toLowerCase() === currentUserEmail.toLowerCase()) {
      toast.error("You cannot delete your own account.");
      return;
    }
    setDeleteError(null);
    setDeleting(employee);
  };

  const confirmDelete = async () => {
    if (!deleting) return;
    setDeleteError(null);
    try {
      await deleteEmp(deleting.id);
      toast.success(`${deleting.firstName} ${deleting.lastName} deleted from staff lists`);
      setDeleting(null);
    } catch (err: any) {
      const message = err?.message || "Failed to delete employee";
      setDeleteError(message);
      toast.error(message);
    }
  };

  return (
    <AppLayout>
      <div className="min-h-screen bg-secondary/40">
        <main className="mx-auto max-w-7xl px-4 py-8 sm:px-6">
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div>
            <p className="text-xs font-semibold uppercase tracking-wider text-primary">Manager workspace</p>
            <h1 className="mt-1 font-display text-3xl font-bold">Staff Management</h1>
            <p className="mt-1 text-sm text-muted-foreground">Manage employee accounts and roles.</p>
          </div>
          <button onClick={() => setCreating(true)} className="inline-flex items-center gap-2 rounded-full bg-gradient-primary px-5 py-2.5 text-sm font-semibold text-primary-foreground shadow-glow">
            <Plus className="h-4 w-4" /> Add Employee
          </button>
        </div>

        <div className="mt-6 flex flex-wrap items-center justify-between gap-3">
          <div
            role="group"
            aria-label="Filter employees by status"
            className="inline-flex rounded-full border border-border bg-card p-1 shadow-soft"
          >
            {filterOptions.map((opt) => {
              const selected = statusFilter === opt.value;
              return (
                <button
                  key={opt.value}
                  type="button"
                  aria-pressed={selected}
                  data-status-filter={opt.value}
                  onClick={() => setStatusFilter(opt.value)}
                  className={`inline-flex items-center gap-1.5 rounded-full px-3.5 py-1.5 text-xs font-semibold transition ${
                    selected
                      ? "bg-primary text-primary-foreground shadow-sm"
                      : "text-muted-foreground hover:bg-secondary hover:text-foreground"
                  }`}
                >
                  <span>{opt.label}</span>
                  {typeof opt.count === "number" && (
                    <span
                      className={`inline-flex min-w-5 items-center justify-center rounded-full px-1.5 text-[11px] font-bold ${
                        selected ? "bg-white/20 text-primary-foreground" : "bg-secondary text-foreground"
                      }`}
                    >
                      {opt.count}
                    </span>
                  )}
                </button>
              );
            })}
          </div>

          <label className="relative block w-full sm:w-72">
            <span className="sr-only">Search employees</span>
            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" aria-hidden="true" />
            <input
              type="search"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Search name, email, or role..."
              aria-label="Search employees"
              className="w-full rounded-full border border-border bg-card py-2 pl-9 pr-4 text-sm outline-none transition focus:border-primary"
            />
          </label>
        </div>

        <div className="mt-4">
          <div className="overflow-hidden rounded-3xl border border-border bg-card shadow-soft">
            <table className="w-full text-sm">
              <thead className="hidden border-b border-border bg-secondary/50 text-left text-xs uppercase tracking-wider text-muted-foreground md:table-header-group">
                <tr>
                  <th className="p-4">Name</th>
                  <th className="p-4">Email</th>
                  <th className="p-4">Role</th>
                  <th className="p-4">Status</th>
                  <th className="p-4 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {isLoading && (
                  <tr><td colSpan={5} className="p-10 text-center"><Loader2 className="mx-auto h-5 w-5 animate-spin text-primary" /></td></tr>
                )}
                {!isLoading && filteredEmployees.length === 0 && (
                  <tr>
                    <td colSpan={5} className="p-10 text-center">
                      <p className="font-semibold text-foreground">
                        {normalizedSearch
                          ? `No ${statusFilter === "all" ? "" : `${statusFilter} `}staff match "${search.trim()}".`
                          : statusFilter === "active"
                            ? "No active employees found."
                            : statusFilter === "inactive"
                              ? "No inactive employees found."
                              : "No employees found."}
                      </p>
                      <p className="mt-1 text-xs text-muted-foreground">
                        {normalizedSearch
                          ? "Try clearing the search query or switching the status filter."
                          : statusFilter === "active"
                            ? "Switch to Inactive or All to review deactivated staff accounts."
                            : "Deleted accounts are permanently archived from staff lists while historical records stay intact."}
                      </p>
                      {normalizedSearch && (
                        <button
                          type="button"
                          onClick={() => setSearch("")}
                          className="mt-3 rounded-full border border-border bg-background px-4 py-1.5 text-xs font-semibold hover:bg-secondary"
                        >
                          Clear search
                        </button>
                      )}
                    </td>
                  </tr>
                )}
                {!isLoading && filteredEmployees.map((emp) => {
                  const isSelf = emp.email.toLowerCase() === currentUserEmail.toLowerCase();
                  return (
                  <tr key={emp.id} data-employee-id={emp.id} className="block p-4 md:table-row md:p-0">
                    <td className="md:p-4">
                      <p className="font-semibold">{emp.firstName} {emp.lastName}</p>
                    </td>
                    <td className="mt-1 md:mt-0 md:p-4 text-muted-foreground">
                      {emp.email}
                    </td>
                    <td className="mt-1 md:mt-0 md:p-4">
                      <span className="inline-flex rounded-full bg-secondary px-2.5 py-0.5 text-xs font-semibold text-foreground">
                        {emp.role}
                      </span>
                    </td>
                    <td className="mt-2 md:mt-0 md:p-4">
                      {emp.isActive ? (
                        <span className="inline-flex rounded-full bg-success/15 px-2.5 py-0.5 text-xs font-semibold text-success">
                          Active
                        </span>
                      ) : (
                        <span className="inline-flex rounded-full bg-destructive/15 px-2.5 py-0.5 text-xs font-semibold text-destructive">
                          Inactive
                        </span>
                      )}
                    </td>
                    <td className="mt-4 flex flex-wrap justify-end gap-2 md:mt-0 md:p-4 text-right">
                      <button onClick={() => setEditing(emp)} className="inline-flex items-center gap-1.5 rounded-full border border-border bg-card px-3 py-1.5 text-xs font-semibold transition hover:bg-secondary">
                        <Pencil className="h-3.5 w-3.5" /> Edit
                      </button>
                      <Link
                        to="/menu-access"
                        className="inline-flex items-center gap-1.5 rounded-full border border-border bg-card px-3 py-1.5 text-xs font-semibold transition hover:bg-secondary"
                        title="Configure custom menu category and item access"
                      >
                        Menu Access
                      </Link>
                      <button 
                        onClick={() => toggleStatus(emp)} 
                        className={`inline-flex items-center justify-center w-[110px] gap-1.5 rounded-full px-3 py-1.5 text-xs font-semibold transition ${emp.isActive ? 'bg-destructive/10 text-destructive hover:bg-destructive hover:text-destructive-foreground' : 'bg-success/10 text-success hover:bg-success hover:text-success-foreground'}`}
                      >
                        {emp.isActive ? (
                          <><PowerOff className="h-3.5 w-3.5" /> Deactivate</>
                        ) : (
                          <><Power className="h-3.5 w-3.5" /> Activate</>
                        )}
                      </button>
                      <button
                        type="button"
                        disabled={isSelf}
                        aria-label={`Delete ${emp.firstName} ${emp.lastName}`}
                        title={isSelf ? "You cannot delete your own account" : `Delete ${emp.firstName} ${emp.lastName}`}
                        onClick={() => requestDelete(emp)}
                        className="inline-flex items-center gap-1.5 rounded-full border border-destructive/30 bg-card px-3 py-1.5 text-xs font-semibold text-destructive transition hover:bg-destructive hover:text-destructive-foreground disabled:cursor-not-allowed disabled:opacity-40 disabled:hover:bg-card disabled:hover:text-destructive"
                      >
                        <Trash2 className="h-3.5 w-3.5" /> Delete
                      </button>
                    </td>
                  </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      </main>
      {(creating || editing) && (
        <EmployeeModal 
          employee={editing} 
          onClose={() => { setCreating(false); setEditing(null); }} 
        />
      )}
      {deleting && (
        <DeleteEmployeeDialog
          employee={deleting}
          busy={isDeleting}
          error={deleteError}
          onCancel={() => { if (!isDeleting) { setDeleting(null); setDeleteError(null); } }}
          onConfirm={() => void confirmDelete()}
        />
      )}
      </div>
    </AppLayout>
  );
}

function DeleteEmployeeDialog({
  employee,
  busy,
  error,
  onCancel,
  onConfirm,
}: {
  employee: Employee;
  busy: boolean;
  error: string | null;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="delete-employee-title"
      className="fixed inset-0 z-50 grid place-items-center p-4"
    >
      <div className="absolute inset-0 bg-foreground/40 backdrop-blur-sm" onClick={onCancel} />
      <div className="relative w-full max-w-md animate-float-up rounded-3xl bg-card p-6 shadow-glow">
        <div className="flex items-start justify-between gap-3">
          <div className="flex items-start gap-3">
            <span className="grid h-10 w-10 shrink-0 place-items-center rounded-2xl bg-destructive/10 text-destructive">
              <AlertTriangle className="h-5 w-5" aria-hidden="true" />
            </span>
            <div>
              <p className="text-xs font-semibold uppercase tracking-wider text-destructive">Archive Staff Account</p>
              <h2 id="delete-employee-title" className="font-display text-xl font-bold">
                Delete {employee.firstName} {employee.lastName}?
              </h2>
            </div>
          </div>
          <button
            type="button"
            disabled={busy}
            aria-label="Close delete dialog"
            onClick={onCancel}
            className="grid h-8 w-8 shrink-0 place-items-center rounded-full bg-secondary transition hover:bg-border disabled:opacity-50"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        <div className="mt-4 rounded-2xl border border-border bg-secondary/40 p-3.5 text-sm">
          <p className="font-semibold text-foreground">{employee.firstName} {employee.lastName}</p>
          <p className="break-all text-xs text-muted-foreground">{employee.email}</p>
          <span className="mt-1.5 inline-flex rounded-full bg-card px-2 py-0.5 text-[11px] font-semibold text-foreground">
            {employee.role}
          </span>
        </div>

        <p className="mt-3 text-sm leading-relaxed text-muted-foreground">
          Deleting this account removes it from active and inactive staff lists and prevents sign-in immediately.
          Existing orders, bills, payments, support messages, and audit history remain attributed to this employee record.
        </p>

        {error && (
          <div role="alert" className="mt-3 rounded-xl border border-destructive/30 bg-destructive/10 px-3 py-2 text-xs font-medium text-destructive">
            {error}
          </div>
        )}

        <div className="mt-6 flex justify-end gap-2">
          <button
            type="button"
            disabled={busy}
            onClick={onCancel}
            className="rounded-full border border-border bg-card px-4 py-2 text-xs font-semibold transition hover:bg-secondary disabled:opacity-50"
          >
            Cancel
          </button>
          <button
            type="button"
            disabled={busy}
            onClick={onConfirm}
            className="inline-flex items-center gap-1.5 rounded-full bg-destructive px-4 py-2 text-xs font-semibold text-destructive-foreground transition hover:opacity-90 disabled:opacity-60"
          >
            {busy && <Loader2 className="h-3.5 w-3.5 animate-spin" />}
            Delete Staff Account
          </button>
        </div>
      </div>
    </div>
  );
}

function EmployeeModal({ employee, onClose }: { employee: Employee | null; onClose: () => void }) {
  const { mutateAsync: createEmp } = useCreateEmployee();
  const { mutateAsync: updateEmp } = useUpdateEmployee();

  const [firstName, setFirstName] = useState(employee?.firstName || "");
  const [lastName, setLastName] = useState(employee?.lastName || "");
  const [email, setEmail] = useState(employee?.email || "");
  const [password, setPassword] = useState("");
  const [roleName, setRoleName] = useState(employee?.role || "Waiter");
  const [busy, setBusy] = useState(false);

  const save = async () => {
    if (!firstName || !lastName || !email || !roleName || (!employee && !password)) {
      toast.error("Please fill in all required fields");
      return;
    }
    
    // Basic email validation
    if (!/^\S+@\S+\.\S+$/.test(email)) {
      toast.error("Invalid email address format");
      return;
    }

    setBusy(true);
    
    try {
      if (employee) {
        const payload: any = { firstName, lastName, email, roleName };
        if (password.trim()) {
          if (password.length < 6) {
            toast.error("New password must be at least 6 characters");
            setBusy(false);
            return;
          }
          payload.newPassword = password;
        }
        await updateEmp({ 
          id: employee.id, 
          data: payload
        });
        toast.success(password.trim() ? "Profile details & password updated" : "Employee updated successfully");
      } else {
        await createEmp({ firstName, lastName, email, password, roleName });
        toast.success("Employee created successfully");
      }
      onClose();
    } catch (err: any) {
      toast.error(err.message || "Failed to save employee");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 grid place-items-center p-4">
      <div className="absolute inset-0 bg-foreground/40 backdrop-blur-sm" onClick={() => onClose()} />
      <div className="relative w-full max-w-md animate-float-up rounded-3xl bg-card p-6 shadow-glow">
        <div className="flex items-start justify-between">
          <div>
            <p className="text-xs uppercase tracking-wider text-muted-foreground">{employee ? "Edit employee account" : "New employee"}</p>
            <h2 className="font-display text-xl font-bold">{employee ? `${employee.firstName} ${employee.lastName}` : "Add Staff"}</h2>
          </div>
          <button onClick={() => onClose()} className="grid h-8 w-8 place-items-center rounded-full bg-secondary transition hover:bg-border"><X className="h-4 w-4" /></button>
        </div>
        
        <div className="mt-5 space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <Field label="First Name" value={firstName} onChange={setFirstName} />
            <Field label="Last Name" value={lastName} onChange={setLastName} />
          </div>
          
          <Field label="Login email" value={email} onChange={setEmail} type="email" />

          <label className="block">
            <span className="text-xs font-semibold text-muted-foreground">Role</span>
            <select value={roleName} onChange={e => setRoleName(e.target.value)} className="mt-1 w-full rounded-xl border border-border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring">
              <option value="Admin">Admin</option>
              <option value="Kitchen">Kitchen</option>
              <option value="Waiter">Waiter</option>
              <option value="Cashier">Cashier</option>
            </select>
          </label>

          {employee ? (
            <div className="rounded-2xl border border-border/80 bg-secondary/30 p-3.5 space-y-2">
              <div className="flex items-center justify-between">
                <span className="text-xs font-semibold text-foreground">Password Management</span>
                <span className="text-[10px] text-muted-foreground">Optional — leave blank to keep current</span>
              </div>
              <Field
                label="New Staff Password"
                value={password}
                onChange={setPassword}
                type="password"
              />
            </div>
          ) : (
            <Field label="Initial Password" value={password} onChange={setPassword} type="password" />
          )}

          <div className="rounded-xl bg-secondary/40 p-3 text-xs space-y-1 border border-border/60">
            <span className="font-semibold text-foreground">
              {roleName === "Admin" ? "Full Admin Access" : roleName === "Cashier" ? "Cashier Billing Permissions" : roleName === "Waiter" ? "Floor Waiter Permissions" : "Kitchen Display Permissions"}
            </span>
            <p className="text-muted-foreground text-[11px]">
              {roleName === "Admin"
                ? "Full access to menu items, categories, pricing, table QRs, inventory, purchase orders, financial reports, staff administration, and floor billing."
                : roleName === "Cashier"
                ? "Billing only: review table bills, collect payments, settle bills and print receipts in the Cashier workspace."
                : roleName === "Waiter"
                ? "Fast staff order taking, table selection, multi-draft orders, hold/fire, refills, dining groups, bill reviews, and collections."
                : "Live KDS kitchen ticket display, preparation status progression, fire course management, and manager-approved item cancellations."}
            </p>
          </div>
        </div>

        <div className="mt-6 flex justify-end gap-2">
          <button onClick={() => onClose()} className="rounded-full border border-border px-4 py-2 text-sm font-semibold transition hover:bg-secondary">Cancel</button>
          <button onClick={save} disabled={busy} className="inline-flex items-center gap-2 rounded-full bg-gradient-primary px-5 py-2 text-sm font-semibold text-primary-foreground shadow-soft disabled:opacity-60 transition hover:opacity-90">
            {busy && <Loader2 className="h-4 w-4 animate-spin" />} {employee ? "Save changes" : "Create employee"}
          </button>
        </div>
      </div>
    </div>
  );
}

function Field({ label, value, onChange, type = "text" }: { label: string; value: string; onChange: (v: string) => void; type?: string }) {
  const [showPassword, setShowPassword] = useState(false);
  const isPassword = type === "password";

  return (
    <label className="block">
      <span className="text-xs font-semibold text-muted-foreground">{label}</span>
      <div className="relative">
        <input
          type={isPassword ? (showPassword ? "text" : "password") : type}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          className={`mt-1 w-full rounded-xl border border-border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring ${isPassword ? "pr-10" : ""}`}
        />
        {isPassword && (
          <button
            type="button"
            onClick={() => setShowPassword(!showPassword)}
            className="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground transition p-1"
            aria-label={showPassword ? "Hide password" : "Show password"}
          >
            {showPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
          </button>
        )}
      </div>
    </label>
  );
}
