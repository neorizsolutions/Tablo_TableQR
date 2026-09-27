import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { useState, useEffect } from "react";
import { toast } from "sonner";
import { useAuth } from "@/hooks/use-auth";
import { apiClient } from "@/services/apiClient";
import { Loader2, QrCode } from "lucide-react";

export const Route = createFileRoute("/super-admin")({
  component: SuperAdminPage,
  head: () => ({
    meta: [
      { title: "Super Admin Onboarding — Tablo" }
    ]
  })
});

function SuperAdminPage() {
  const { user, signIn, signOut } = useAuth();
  
  if (user && user.role === "SuperAdmin") {
    return <OnboardingForm onLogout={() => signOut("/super-admin")} />;
  }

  if (user) {
    return (
      <div className="min-h-screen bg-secondary/40 px-4 py-10 flex items-center justify-center">
        <div className="text-center">
          <p className="text-destructive mb-4 font-bold">You are logged in as a normal user. Please sign out first.</p>
          <button onClick={() => signOut("/super-admin")} className="rounded-md bg-foreground px-4 py-2 text-background font-bold">Sign Out</button>
        </div>
      </div>
    );
  }

  return <SuperAdminLogin onLogin={signIn} />;
}

function SuperAdminLogin({ onLogin }: { onLogin: (token: string) => void }) {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [busy, setBusy] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    try {
      const res = await apiClient<{ token: string }>("/sysadmin/login", {
        method: "POST",
        body: JSON.stringify({ username, password }),
      });
      onLogin(res.token);
      toast.success("Super Admin Authenticated");
    } catch (err: any) {
      toast.error(err.message || "Authentication failed");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-secondary/40 via-background to-primary-soft/30 px-4 py-10">
      <div className="mx-auto max-w-md pt-10">
        <div className="mb-6 flex items-center gap-2 font-display text-lg font-bold">
          <span className="grid h-9 w-9 place-items-center rounded-xl bg-gradient-primary text-primary-foreground shadow-soft">
            <QrCode className="h-4 w-4" />
          </span>
          Tablo <span className="text-primary font-normal text-sm ml-2">System Onboarding</span>
        </div>
        <div className="rounded-2xl border border-border bg-card p-6 shadow-soft sm:p-8">
          <h2 className="font-display text-2xl font-bold">System Authentication</h2>
          <p className="mt-2 text-sm text-muted-foreground mb-6">Enter system credentials to provision new tenants.</p>
          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <label className="text-sm font-semibold">System Username</label>
              <input type="text" value={username} onChange={e => setUsername(e.target.value)} required className="w-full mt-1 rounded-md border bg-background px-3 py-2" />
            </div>
            <div>
              <label className="text-sm font-semibold">System Password</label>
              <input type="password" value={password} onChange={e => setPassword(e.target.value)} required className="w-full mt-1 rounded-md border bg-background px-3 py-2" />
            </div>
            <button disabled={busy} className="w-full rounded-md bg-foreground text-background py-2 font-bold mt-4">
              {busy ? "Authenticating..." : "Authenticate"}
            </button>
          </form>
        </div>
      </div>
    </div>
  );
}

function OnboardingForm({ onLogout }: { onLogout: () => void }) {
  const [restaurantName, setRestaurantName] = useState("");
  const [adminFirstName, setAdminFirstName] = useState("");
  const [adminLastName, setAdminLastName] = useState("");
  const [adminEmail, setAdminEmail] = useState("");
  const [adminPassword, setAdminPassword] = useState("");
  const [busy, setBusy] = useState(false);
  const [successData, setSuccessData] = useState<any>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    try {
      const res = await apiClient<any>("/sysadmin/onboard", {
        method: "POST",
        body: JSON.stringify({
          restaurantName,
          adminFirstName,
          adminLastName,
          adminEmail,
          adminPassword
        }),
      });
      setSuccessData({ restaurantName: res.restaurantName, adminEmail });
      toast.success("Tenant Provisioned Successfully");
    } catch (err: any) {
      toast.error(err.message || "Failed to provision tenant");
    } finally {
      setBusy(false);
    }
  };

  if (successData) {
    return (
      <div className="min-h-screen bg-secondary/40 px-4 py-10 flex items-center justify-center">
        <div className="max-w-md w-full bg-card p-8 rounded-2xl shadow-soft border text-center">
          <div className="h-16 w-16 bg-success/20 text-success rounded-full flex items-center justify-center mx-auto mb-4 text-3xl">✓</div>
          <h2 className="text-2xl font-bold mb-2">Tenant Provisioned</h2>
          <p className="text-muted-foreground mb-6">The restaurant has been created securely.</p>
          <div className="text-left bg-secondary p-4 rounded-lg mb-6">
            <p><strong>Restaurant:</strong> {successData.restaurantName}</p>
            <p><strong>Admin Email:</strong> {successData.adminEmail}</p>
          </div>
          <p className="text-sm text-muted-foreground mb-6">You can now hand these credentials to the client. They can log in at the main /auth page to configure their restaurant.</p>
          <div className="flex gap-4 justify-center">
             <button onClick={() => setSuccessData(null)} className="px-4 py-2 border rounded-md font-semibold">Provision Another</button>
             <button onClick={onLogout} className="px-4 py-2 bg-foreground text-background rounded-md font-semibold">Sign Out</button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-secondary/40 px-4 py-10">
      <div className="mx-auto max-w-2xl">
        <div className="flex items-center justify-between mb-8">
          <h1 className="text-2xl font-bold">Tenant Onboarding</h1>
          <button onClick={onLogout} className="px-4 py-2 text-sm font-bold bg-card border rounded-md">Sign Out</button>
        </div>
        <form onSubmit={handleSubmit} className="bg-card p-6 rounded-2xl shadow border space-y-6">
          <div>
            <h3 className="font-bold text-lg mb-1">Restaurant Details</h3>
            <p className="text-sm text-muted-foreground mb-4">The core tenant workspace.</p>
            <div>
              <label className="text-sm font-semibold">Restaurant Name</label>
              <input type="text" value={restaurantName} onChange={e => setRestaurantName(e.target.value)} required className="w-full mt-1 rounded-md border bg-background px-3 py-2" />
            </div>
          </div>
          
          <div className="border-t pt-6">
            <h3 className="font-bold text-lg mb-1">Initial Administrator</h3>
            <p className="text-sm text-muted-foreground mb-4">The owner/admin who will setup the restaurant.</p>
            <div className="grid grid-cols-2 gap-4 mb-4">
              <div>
                <label className="text-sm font-semibold">First Name</label>
                <input type="text" value={adminFirstName} onChange={e => setAdminFirstName(e.target.value)} required className="w-full mt-1 rounded-md border bg-background px-3 py-2" />
              </div>
              <div>
                <label className="text-sm font-semibold">Last Name</label>
                <input type="text" value={adminLastName} onChange={e => setAdminLastName(e.target.value)} required className="w-full mt-1 rounded-md border bg-background px-3 py-2" />
              </div>
            </div>
            <div className="space-y-4">
              <div>
                <label className="text-sm font-semibold">Email Address (Login)</label>
                <input type="email" value={adminEmail} onChange={e => setAdminEmail(e.target.value)} required className="w-full mt-1 rounded-md border bg-background px-3 py-2" />
              </div>
              <div>
                <label className="text-sm font-semibold">Temporary Password</label>
                <input type="text" value={adminPassword} onChange={e => setAdminPassword(e.target.value)} required minLength={6} className="w-full mt-1 rounded-md border bg-background px-3 py-2" />
              </div>
            </div>
          </div>
          
           <div className="pt-4">
             <button disabled={busy} className="w-full flex items-center justify-center gap-2 rounded-md bg-primary text-primary-foreground py-3 font-bold">
               {busy && <Loader2 className="h-4 w-4 animate-spin" />} {busy ? "Provisioning..." : "Provision Tenant"}
             </button>
          </div>
        </form>
        <TenantList />
      </div>
    </div>
  );
}

function TenantList() {
  const [tenants, setTenants] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);

  const fetchTenants = async () => {
    try {
      const data = await apiClient<any[]>("/sysadmin/tenants");
      setTenants(data);
    } catch (err: any) {
      toast.error(err.message || "Failed to load tenants");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchTenants();
  }, []);

  const toggleStatus = async (id: string, currentStatus: boolean) => {
    try {
      const res = await apiClient<any>(`/sysadmin/tenants/${id}/toggle-status`, {
        method: "PUT"
      });
      toast.success(res.message);
      setTenants(prev => prev.map(t => t.id === id ? { ...t, isActive: res.isActive } : t));
    } catch (err: any) {
      toast.error(err.message || "Failed to toggle status");
    }
  };

  if (loading) return <div className="text-center py-8"><Loader2 className="h-6 w-6 animate-spin mx-auto text-muted-foreground" /></div>;

  return (
    <div className="mt-8 bg-card rounded-2xl shadow border overflow-hidden">
      <div className="p-6 border-b">
        <h3 className="font-bold text-lg">Provisioned Tenants Report</h3>
        <p className="text-sm text-muted-foreground">Manage active restaurants and their access.</p>
      </div>
      <div className="divide-y">
        {tenants.length === 0 ? (
          <div className="p-6 text-center text-muted-foreground">No tenants provisioned yet.</div>
        ) : (
          tenants.map(t => (
            <div key={t.id} className="p-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="font-bold">{t.name}</p>
                  <p className="text-sm text-muted-foreground">{t.contactEmail}</p>
                  <p className="text-xs text-muted-foreground mt-1">Created: {new Date(t.createdAt).toLocaleDateString()}</p>
                </div>
                <div className="flex items-center gap-4">
                  <span className={`px-2 py-1 rounded text-xs font-bold ${t.isActive ? 'bg-success/10 text-success' : 'bg-destructive/10 text-destructive'}`}>
                    {t.isActive ? "Active" : "Disabled"}
                  </span>
                  <button
                    onClick={() => toggleStatus(t.id, t.isActive)}
                    className="px-3 py-1.5 text-sm font-semibold border rounded-md hover:bg-secondary transition"
                  >
                    {t.isActive ? "Disable" : "Enable"}
                  </button>
                </div>
              </div>
              
              {t.activeUsers && t.activeUsers.length > 0 && (
                <div className="mt-4 pt-4 border-t border-border/40">
                  <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-2">
                    Active Users ({t.activeUsers.length})
                  </p>
                  <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-2">
                    {t.activeUsers.map((user: any, i: number) => (
                      <div key={i} className="flex items-start gap-2 p-2 rounded-md bg-secondary/30">
                        <div className="flex-1 min-w-0">
                          <p className="text-sm font-medium truncate">{user.firstName} {user.lastName}</p>
                          <p className="text-xs text-muted-foreground truncate">{user.email}</p>
                        </div>
                        <span className="shrink-0 inline-flex items-center rounded-full bg-primary/10 px-2 py-0.5 text-[10px] font-medium text-primary">
                          {user.role}
                        </span>
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </div>
          ))
        )}
      </div>
    </div>
  );
}
