import { createFileRoute, Link } from "@tanstack/react-router";
import { useState } from "react";
import { Loader2, ShieldAlert, LogIn, LogOut, Plus, Pencil, Power, PowerOff, X } from "lucide-react";
import { toast } from "sonner";
import { SiteHeader } from "@/components/site-header";
import { useAuth } from "@/hooks/use-auth";
import { useStaff, useCreateEmployee, useUpdateEmployee, useUpdateEmployeeStatus } from "@/hooks/use-staff";
import { Employee } from "@/services/api";

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
  const { data: employees, isLoading } = useStaff();
  const { mutateAsync: updateStatus } = useUpdateEmployeeStatus();

  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<Employee | null>(null);

  const toggleStatus = async (employee: Employee) => {
    if (employee.email === currentUserEmail) {
      toast.error("You cannot deactivate your own account.");
      return;
    }

    const newStatus = !employee.isActive;
    if (!newStatus) {
      if (!confirm(`Are you sure you want to deactivate ${employee.firstName} ${employee.lastName}? They will no longer be able to log in.`)) {
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

  return (
    <div className="min-h-screen bg-secondary/40">
      <SiteHeader />
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

        <div className="mt-6">
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
                {!isLoading && employees?.map((emp) => (
                  <tr key={emp.id} className="block p-4 md:table-row md:p-0">
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
                    </td>
                  </tr>
                ))}
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
        await updateEmp({ 
          id: employee.id, 
          data: { firstName, lastName, email, roleName } 
        });
        toast.success("Employee updated");
      } else {
        await createEmp({ firstName, lastName, email, password, roleName });
        toast.success("Employee created");
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
      <div className="absolute inset-0 bg-foreground/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative w-full max-w-md animate-float-up rounded-3xl bg-card p-6 shadow-glow">
        <div className="flex items-start justify-between">
          <div>
            <p className="text-xs uppercase tracking-wider text-muted-foreground">{employee ? "Edit employee" : "New employee"}</p>
            <h2 className="font-display text-xl font-bold">{employee ? `${employee.firstName} ${employee.lastName}` : "Add Staff"}</h2>
          </div>
          <button onClick={onClose} className="grid h-8 w-8 place-items-center rounded-full bg-secondary transition hover:bg-border"><X className="h-4 w-4" /></button>
        </div>
        
        <div className="mt-5 space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <Field label="First Name" value={firstName} onChange={setFirstName} />
            <Field label="Last Name" value={lastName} onChange={setLastName} />
          </div>
          
          <Field label="Email" value={email} onChange={setEmail} type="email" />
          
          {!employee && (
            <Field label="Password" value={password} onChange={setPassword} type="password" />
          )}

          <label className="block">
            <span className="text-xs font-semibold text-muted-foreground">Role</span>
            <select value={roleName} onChange={e => setRoleName(e.target.value)} className="mt-1 w-full rounded-xl border border-border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring">
              <option value="Admin">Admin</option>
              <option value="Kitchen">Kitchen</option>
              <option value="Waiter">Waiter</option>
            </select>
          </label>
        </div>

        <div className="mt-6 flex justify-end gap-2">
          <button onClick={onClose} className="rounded-full border border-border px-4 py-2 text-sm font-semibold transition hover:bg-secondary">Cancel</button>
          <button onClick={save} disabled={busy} className="inline-flex items-center gap-2 rounded-full bg-gradient-primary px-5 py-2 text-sm font-semibold text-primary-foreground shadow-soft disabled:opacity-60 transition hover:opacity-90">
            {busy && <Loader2 className="h-4 w-4 animate-spin" />} {employee ? "Save changes" : "Create employee"}
          </button>
        </div>
      </div>
    </div>
  );
}

function Field({ label, value, onChange, type = "text" }: { label: string; value: string; onChange: (v: string) => void; type?: string }) {
  return (
    <label className="block">
      <span className="text-xs font-semibold text-muted-foreground">{label}</span>
      <input
        type={type}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="mt-1 w-full rounded-xl border border-border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
      />
    </label>
  );
}
