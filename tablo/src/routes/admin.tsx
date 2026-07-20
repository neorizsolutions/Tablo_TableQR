import { createFileRoute, Link } from "@tanstack/react-router";
import { useEffect, useMemo, useState } from "react";
import { Eye, Languages, Loader2, Pencil, Plus, QrCode, Search, Sparkles, X, Trash2, FolderPlus, LogIn, ShieldAlert, LogOut } from "lucide-react";
import { toast } from "sonner";
import { SiteHeader } from "@/components/site-header";
import { useAuth } from "@/hooks/use-auth";
import { formatINR } from "@/lib/mock-data";
import { Category, MenuItem } from "@/types";
import { useRestaurant } from "@/hooks/use-restaurant";
import { useAdminMenu, useUpdateMenuItem, useCreateMenuItem, useDeleteMenuItem, useCreateCategory, useUpdateCategory, useDeleteCategory } from "@/hooks/use-menu";
import { useTables, useCreateTable, useUpdateTable, useRegenerateQR } from "@/hooks/use-tables";
import { QRCodeSVG } from "qrcode.react";

export const Route = createFileRoute("/admin")({
  head: () => ({
    meta: [
      { title: "Admin panel — Tablo demo" },
      { name: "description", content: "Manage menu items, availability and table QR codes." },
    ],
  }),
  component: AdminPage,
});

function AdminPage() {
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

  return <AdminInner />;
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
            The admin panel is private. Sign in with an admin account to continue.
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

function AdminInner() {
  const { data: restaurant } = useRestaurant("saffron-sage");
  const { data: menuData, isLoading: loadingMenu } = useAdminMenu(restaurant?.id || null);
  
  const categoriesList = menuData?.categories || [];
  const items = menuData?.flatMenu || [];

  const { mutateAsync: updateItem } = useUpdateMenuItem();
  const { mutateAsync: createItem } = useCreateMenuItem();
  const { mutateAsync: deleteItem } = useDeleteMenuItem();
  const { mutateAsync: createCat } = useCreateCategory();
  const { mutateAsync: updateCat } = useUpdateCategory();
  const { mutateAsync: deleteCat } = useDeleteCategory();

  const [query, setQuery] = useState("");
  const [cat, setCat] = useState<string>("All");
  
  const [editing, setEditing] = useState<MenuItem | null>(null);
  const [creating, setCreating] = useState(false);
  const [manageCategories, setManageCategories] = useState(false);
  const [manageTables, setManageTables] = useState(false);

  const { data: tables = [] } = useTables(restaurant?.id || null);
  const activeTables = tables.filter((t: any) => t.isActive);
  const activeTablesCount = activeTables.length;
  const previewLink = activeTables.length > 0 ? `/t/${activeTables[0].qrToken}` : "#";

  const visible = useMemo(() => 
    items.filter((i: any) => (cat === "All" || i.category === cat) && i.name.toLowerCase().includes(query.toLowerCase())), 
    [items, query, cat]
  );

  const toggle = async (id: string, currentAvailable: boolean) => {
    try {
      await updateItem({ itemId: id, updates: { available: !currentAvailable } });
    } catch (err: any) {
      toast.error(err.message || "Failed to update availability");
    }
  };

  const saveItem = async (updated: Partial<MenuItem>, categoryId: string) => {
    if (editing) {
      try {
        await updateItem({ itemId: editing.id, categoryId, updates: updated });
        setEditing(null);
        toast.success("Item updated");
      } catch (err: any) {
        toast.error(err.message || "Failed to update item");
      }
    } else if (creating) {
      try {
        await createItem({ restaurantId: restaurant!.id, categoryId, item: updated });
        setCreating(false);
        toast.success("Item created");
      } catch (err: any) {
        toast.error(err.message || "Failed to create item");
      }
    }
  };

  const removeMenuItem = async (id: string) => {
    if (!confirm("Are you sure you want to delete this menu item?")) return;
    try {
      await deleteItem(id);
      toast.success("Item deleted");
    } catch (err: any) {
      toast.error(err.message || "Failed to delete item");
    }
  };

  const handleCreateCategory = async (name: string) => {
    try {
      await createCat({ restaurantId: restaurant!.id, name });
      toast.success("Category created");
    } catch (err: any) {
      toast.error(err.message || "Failed to create category");
    }
  };

  const handleUpdateCategory = async (id: string, name: string) => {
    try {
      await updateCat({ categoryId: id, name });
      toast.success("Category updated");
    } catch (err: any) {
      toast.error(err.message || "Failed to update category");
    }
  };

  const handleDeleteCategory = async (id: string) => {
    if (!confirm("Are you sure you want to delete this category?")) return;
    try {
      await deleteCat(id);
      toast.success("Category deleted");
    } catch (err: any) {
      toast.error(err.message || "Failed to delete category");
    }
  };

  return (
    <div className="min-h-screen bg-secondary/40">
      <SiteHeader />
      <main className="mx-auto max-w-7xl px-4 py-8 sm:px-6">
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div>
            <p className="text-xs font-semibold uppercase tracking-wider text-primary">Manager workspace</p>
            <h1 className="mt-1 font-display text-3xl font-bold">Menu & availability</h1>
            <p className="mt-1 text-sm text-muted-foreground">Changes here update the customer menu instantly.</p>
          </div>
          <div className="flex gap-2">
            <button onClick={() => setManageCategories(true)} className="inline-flex items-center gap-2 rounded-full border border-border bg-card px-5 py-2.5 text-sm font-semibold hover:bg-secondary transition">
              <FolderPlus className="h-4 w-4" /> Categories
            </button>
            <button onClick={() => setCreating(true)} className="inline-flex items-center gap-2 rounded-full bg-gradient-primary px-5 py-2.5 text-sm font-semibold text-primary-foreground shadow-glow">
              <Plus className="h-4 w-4" /> Add new item
            </button>
          </div>
        </div>

        <div className="mt-6 grid gap-4 lg:grid-cols-[1fr_320px]">
          <div className="space-y-4">
            <div className="flex flex-wrap items-center gap-3 rounded-2xl border border-border bg-card p-3 shadow-soft">
              <div className="relative flex-1 min-w-[200px]">
                <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                <input value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Search dishes…" className="w-full rounded-full border border-border bg-background py-2 pl-9 pr-3 text-sm focus:outline-none focus:ring-2 focus:ring-ring" />
              </div>
              <div className="flex flex-wrap gap-1.5">
                <button onClick={() => setCat("All")} className={`rounded-full px-3 py-1.5 text-xs font-semibold transition ${cat === "All" ? "bg-foreground text-background" : "bg-secondary text-muted-foreground hover:text-foreground"}`}>
                  All
                </button>
                {categoriesList.map((c: any) => (
                  <button key={c.id} onClick={() => setCat(c.name)} className={`rounded-full px-3 py-1.5 text-xs font-semibold transition ${cat === c.name ? "bg-foreground text-background" : "bg-secondary text-muted-foreground hover:text-foreground"}`}>
                    {c.name}
                  </button>
                ))}
              </div>
            </div>

            <div className="overflow-hidden rounded-3xl border border-border bg-card shadow-soft">
              <table className="w-full text-sm">
                <thead className="hidden border-b border-border bg-secondary/50 text-left text-xs uppercase tracking-wider text-muted-foreground md:table-header-group">
                  <tr>
                    <th className="p-4">Item</th>
                    <th className="p-4">Category</th>
                    <th className="p-4">Price</th>
                    <th className="p-4">Status</th>
                    <th className="p-4 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {loadingMenu && (
                    <tr><td colSpan={5} className="p-10 text-center"><Loader2 className="mx-auto h-5 w-5 animate-spin text-primary" /></td></tr>
                  )}
                  {!loadingMenu && visible.map((i: any) => (
                    <tr key={i.id} className="block p-4 md:table-row md:p-0">
                      <td className="flex items-center gap-3 md:table-cell md:p-4">
                        <img src={i.image} alt="" loading="lazy" className={`h-12 w-12 rounded-xl object-cover ${!i.available ? "grayscale opacity-60" : ""}`} />
                        <div>
                          <p className="font-semibold">{i.name}</p>
                          <p className="line-clamp-1 max-w-xs text-xs text-muted-foreground">{i.description}</p>
                          <div className="mt-1 flex gap-1.5 md:hidden">
                            <span className="rounded-full bg-secondary px-2 py-0.5 text-[10px]">{i.category}</span>
                            <span className="rounded-full bg-secondary px-2 py-0.5 text-[10px] font-bold">{formatINR(i.price)}</span>
                          </div>
                        </div>
                      </td>
                      <td className="hidden text-muted-foreground md:table-cell md:p-4">{i.category}</td>
                      <td className="hidden font-semibold md:table-cell md:p-4">{formatINR(i.price)}</td>
                      <td className="mt-3 flex items-center gap-2 md:mt-0 md:table-cell md:p-4">
                        <Switch checked={i.available} onChange={() => toggle(i.id, i.available)} />
                        <span className={`text-xs font-semibold ${i.available ? "text-primary" : "text-destructive"}`}>{i.available ? "Available" : "Sold out"}</span>
                      </td>
                      <td className="mt-3 flex justify-end gap-2 md:mt-0 md:table-cell md:p-4 md:text-right">
                        <button onClick={() => setEditing(i)} className="inline-flex items-center gap-1 rounded-full border border-border px-3 py-1.5 text-xs font-semibold hover:bg-secondary">
                          <Pencil className="h-3 w-3" /> Edit
                        </button>
                        <button onClick={() => removeMenuItem(i.id)} className="inline-flex items-center gap-1 rounded-full border border-border px-3 py-1.5 text-xs font-semibold text-destructive hover:bg-destructive/10">
                          <Trash2 className="h-3 w-3" />
                        </button>
                      </td>
                    </tr>
                  ))}
                  {!loadingMenu && visible.length === 0 && (
                    <tr><td colSpan={5} className="p-10 text-center text-sm text-muted-foreground">No dishes found.</td></tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>

          {/* Sidebar */}
          <aside className="space-y-4">
            <div className="rounded-3xl border border-border bg-card p-5 shadow-soft">
              <div className="flex items-center gap-2">
                <span className="grid h-9 w-9 place-items-center rounded-xl bg-primary-soft text-primary"><QrCode className="h-4 w-4" /></span>
                <h3 className="font-display font-semibold">Table QR codes</h3>
              </div>
              <p className="mt-2 text-xs text-muted-foreground">{activeTablesCount} active tables · download or reprint anytime.</p>
              <div className="mt-4 grid grid-cols-4 gap-2">
                {tables.slice(0, 8).map((t) => (
                  <div key={t.id} className={`aspect-square rounded-xl ${t.isActive ? 'bg-[conic-gradient(from_0deg,_oklch(0.92_0.012_150)_0_25%,_oklch(0.66_0.14_152)_0_50%,_oklch(0.92_0.012_150)_0_75%,_oklch(0.22_0.03_160)_0_100%)]' : 'bg-border'} p-1`}>
                    <div className="grid h-full w-full place-items-center rounded-md bg-card text-[10px] font-bold overflow-hidden text-ellipsis whitespace-nowrap p-1" title={t.nameOrNumber}>
                      {t.nameOrNumber}
                    </div>
                  </div>
                ))}
              </div>
              <button onClick={() => setManageTables(true)} className="mt-4 w-full rounded-full border border-border py-2 text-xs font-semibold hover:bg-secondary transition">Manage all tables</button>
            </div>

            <div className="rounded-3xl border border-border bg-gradient-hero p-5 shadow-soft">
              <div className="flex items-center gap-2">
                <Sparkles className="h-4 w-4 text-primary" />
                <h3 className="font-display font-semibold">Customer preview</h3>
              </div>
              <p className="mt-2 text-xs text-muted-foreground">See how diners view your menu after every change.</p>
              {activeTablesCount > 0 ? (
                <a href={previewLink} target="_blank" rel="noreferrer" className="mt-3 inline-flex items-center gap-1.5 rounded-full bg-foreground px-4 py-2 text-xs font-semibold text-background hover:opacity-90 transition">
                  <Eye className="h-3.5 w-3.5" /> Open preview
                </a>
              ) : (
                <div className="mt-3 inline-flex flex-col gap-1">
                  <button disabled className="inline-flex items-center gap-1.5 rounded-full bg-secondary px-4 py-2 text-xs font-semibold text-muted-foreground cursor-not-allowed">
                    <Eye className="h-3.5 w-3.5" /> Open preview
                  </button>
                  <span className="text-[10px] text-muted-foreground">Requires an active table.</span>
                </div>
              )}
            </div>

            <div className="rounded-3xl border border-border bg-card p-5 shadow-soft">
              <div className="flex items-center gap-2">
                <Languages className="h-4 w-4 text-primary" />
                <h3 className="font-display font-semibold">Languages</h3>
              </div>
              <p className="mt-2 text-xs text-muted-foreground">Menu translations are live in:</p>
              <div className="mt-3 flex gap-2">
                <span className="rounded-full bg-primary-soft px-3 py-1 text-xs font-semibold text-primary">English</span>
                <span className="rounded-full bg-primary-soft px-3 py-1 text-xs font-semibold text-primary">മലയാളം</span>
              </div>
            </div>
          </aside>
        </div>
      </main>

      {editing && <ItemModal item={editing} categories={categoriesList} onClose={() => setEditing(null)} onSaved={saveItem} />}
      {creating && <ItemModal categories={categoriesList} onClose={() => setCreating(false)} onSaved={saveItem} />}
      {manageCategories && <CategoriesModal categories={categoriesList} onClose={() => setManageCategories(false)} onCreate={handleCreateCategory} onUpdate={handleUpdateCategory} onDelete={handleDeleteCategory} />}
      {manageTables && <TablesModal restaurantId={restaurant!.id} tables={tables} onClose={() => setManageTables(false)} />}
    </div>
  );
}

function TablesModal({ restaurantId, tables, onClose }: { restaurantId: string; tables: any[]; onClose: () => void }) {
  const [newName, setNewName] = useState("");
  const { mutateAsync: create } = useCreateTable();
  const { mutateAsync: update } = useUpdateTable();
  const { mutateAsync: regen } = useRegenerateQR();
  const [busy, setBusy] = useState(false);

  const handleCreate = async () => {
    if (!newName.trim()) return;
    setBusy(true);
    await create({ restaurantId, nameOrNumber: newName });
    setNewName("");
    setBusy(false);
  };

  const handleToggleActive = async (t: any) => {
    setBusy(true);
    await update({ restaurantId, tableId: t.id, data: { nameOrNumber: t.nameOrNumber, isActive: !t.isActive } });
    setBusy(false);
  };

  const handleRegenerate = async (t: any) => {
    if (!confirm("Regenerate QR? The old QR code will immediately stop working.")) return;
    setBusy(true);
    await regen({ restaurantId, tableId: t.id });
    setBusy(false);
  };

  const getQRUrl = (token: string) => {
    const baseUrl = window.location.origin;
    return `${baseUrl}/t/${token}`;
  };

  // The downloadQR function is removed, as it is now handled by the QRDownloader component.

  return (
    <div className="fixed inset-0 z-50 grid place-items-center p-4">
      <div className="absolute inset-0 bg-foreground/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative w-full max-w-2xl animate-float-up rounded-3xl bg-card p-6 shadow-glow max-h-[85vh] flex flex-col">
        <div className="flex items-start justify-between shrink-0">
          <div>
            <p className="text-xs uppercase tracking-wider text-muted-foreground">Manage</p>
            <h2 className="font-display text-xl font-bold">Tables & QR Codes</h2>
          </div>
          <button onClick={onClose} className="grid h-8 w-8 place-items-center rounded-full bg-secondary"><X className="h-4 w-4" /></button>
        </div>
        
        <div className="mt-4 flex gap-2 shrink-0">
          <input value={newName} onChange={e => setNewName(e.target.value)} placeholder="New table name or number (e.g. Table 5, Patio 2)" className="flex-1 rounded-xl border border-border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring" />
          <button onClick={handleCreate} disabled={busy || !newName} className="rounded-xl bg-gradient-primary px-4 py-2 text-sm font-semibold text-primary-foreground disabled:opacity-60">Add Table</button>
        </div>

        <div className="mt-6 flex-1 overflow-y-auto min-h-[200px]">
          {tables.length === 0 && <p className="text-sm text-muted-foreground text-center py-4">No tables yet.</p>}
          <div className="space-y-3">
            {tables.map((t: any) => (
              <div key={t.id} className="flex flex-wrap items-center gap-4 p-4 rounded-2xl border border-border bg-background shadow-sm">
                <div className="hidden sm:block shrink-0 bg-white p-1 rounded-lg">
                  <QRCodeSVG id={`qr-${t.id}`} value={getQRUrl(t.qrToken)} size={60} />
                </div>
                <div className="flex-1 min-w-[200px]">
                  <div className="flex items-center gap-2">
                    <span className="font-semibold">{t.nameOrNumber}</span>
                    <span className={`text-[10px] font-bold px-2 py-0.5 rounded-full ${t.isActive ? "bg-primary-soft text-primary" : "bg-destructive/10 text-destructive"}`}>
                      {t.isActive ? "Active" : "Inactive"}
                    </span>
                  </div>
                  <div className="mt-1 flex items-center gap-2 text-xs text-muted-foreground">
                    Token: <code className="bg-secondary px-1 py-0.5 rounded">{t.qrToken}</code>
                  </div>
                  <div className="mt-1 text-[10px] text-muted-foreground max-w-[200px] truncate" title={getQRUrl(t.qrToken)}>
                    {getQRUrl(t.qrToken)}
                  </div>
                </div>
                <div className="flex items-center gap-2 shrink-0">
                  <QRDownloader table={t} />
                  <button onClick={() => handleRegenerate(t)} disabled={busy} className="p-2 text-muted-foreground hover:bg-secondary hover:text-foreground rounded-lg transition" title="Regenerate QR Token">
                    <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8"/><path d="M3 3v5h5"/></svg>
                  </button>
                  <button onClick={() => handleToggleActive(t)} disabled={busy} className={`px-3 py-1.5 text-xs font-semibold rounded-lg transition ${t.isActive ? "bg-secondary text-muted-foreground hover:bg-destructive/10 hover:text-destructive" : "bg-primary-soft text-primary hover:bg-primary/20"}`}>
                    {t.isActive ? "Deactivate" : "Activate"}
                  </button>
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}

function QRDownloader({ table }: { table: any }) {
  const [dataUrl, setDataUrl] = useState<string>("");

  useEffect(() => {
    // We use a timeout to let the SVG render first
    setTimeout(() => {
      const svg = document.getElementById(`qr-${table.id}`);
      if (!svg) return;
      const svgData = new XMLSerializer().serializeToString(svg);
      const svgWithXmlns = svgData.includes('xmlns=') ? svgData : svgData.replace('<svg ', '<svg xmlns="http://www.w3.org/2000/svg" ');
      
      const canvas = document.createElement("canvas");
      // Create high resolution PNG (500x500)
      canvas.width = 500;
      canvas.height = 500;
      const ctx = canvas.getContext("2d");
      const img = new Image();
      img.onload = () => {
        if (ctx) {
          ctx.fillStyle = "white";
          ctx.fillRect(0, 0, canvas.width, canvas.height);
          // Disable smoothing for sharp QR codes
          ctx.imageSmoothingEnabled = false;
          ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
        }
        setDataUrl(canvas.toDataURL("image/png"));
      };
      img.src = "data:image/svg+xml;base64," + btoa(unescape(encodeURIComponent(svgWithXmlns)));
    }, 100);
  }, [table.id, table.qrToken]);

  const safeName = (table.nameOrNumber || 'unknown').replace(/[^a-z0-9]/gi, '-').toLowerCase();

  return (
    <a 
      href={dataUrl || "#"} 
      download={`tablo-table-${safeName}-qr.png`}
      className={`p-2 text-muted-foreground hover:bg-secondary hover:text-foreground rounded-lg transition ${!dataUrl ? 'opacity-50 cursor-not-allowed pointer-events-none' : ''}`}
      title="Download QR PNG"
    >
      <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" x2="12" y1="15" y2="3"/></svg>
    </a>
  );
}

function Switch({ checked, onChange }: { checked: boolean; onChange: () => void }) {
  return (
    <button onClick={onChange} className={`relative h-6 w-11 rounded-full transition ${checked ? "bg-gradient-primary" : "bg-border"}`}>
      <span className={`absolute top-0.5 h-5 w-5 rounded-full bg-card shadow transition-transform ${checked ? "translate-x-5" : "translate-x-0.5"}`} />
    </button>
  );
}

function ItemModal({ item, categories, onClose, onSaved }: { item?: any; categories: any[]; onClose: () => void; onSaved: (i: Partial<MenuItem>, categoryId: string) => Promise<void> }) {
  const [name, setName] = useState(item?.name ?? "");
  const [price, setPrice] = useState(item ? String(item.price) : "");
  const [description, setDescription] = useState(item?.description ?? "");
  const [image, setImage] = useState(item?.image ?? "");
  const [categoryId, setCategoryId] = useState(item?.categoryId ?? categories[0]?.id ?? "");
  const [busy, setBusy] = useState(false);

  const save = async () => {
    if (!name || !price || !categoryId) {
      toast.error("Name, price, and category are required");
      return;
    }
    setBusy(true);
    const priceNum = Number(price);
    if (Number.isNaN(priceNum)) {
      toast.error("Price must be a number");
      setBusy(false);
      return;
    }
    await onSaved({ name, price: priceNum, description, image, available: item?.available ?? true }, categoryId);
    setBusy(false);
  };

  return (
    <div className="fixed inset-0 z-50 grid place-items-center p-4">
      <div className="absolute inset-0 bg-foreground/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative w-full max-w-lg animate-float-up rounded-3xl bg-card p-6 shadow-glow">
        <div className="flex items-start justify-between">
          <div>
            <p className="text-xs uppercase tracking-wider text-muted-foreground">{item ? "Edit item" : "New item"}</p>
            <h2 className="font-display text-xl font-bold">{item ? item.name : "Create Menu Item"}</h2>
          </div>
          <button onClick={onClose} className="grid h-8 w-8 place-items-center rounded-full bg-secondary"><X className="h-4 w-4" /></button>
        </div>
        
        <div className="mt-4 grid gap-3 sm:grid-cols-2">
          <Field label="Name" value={name} onChange={setName} />
          <Field label="Price (₹)" value={price} onChange={setPrice} />
          <div className="sm:col-span-2">
            <label className="block">
              <span className="text-xs font-semibold text-muted-foreground">Category</span>
              <select value={categoryId} onChange={e => setCategoryId(e.target.value)} className="mt-1 w-full rounded-xl border border-border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring">
                {categories.map((c: any) => (
                  <option key={c.id} value={c.id}>{c.name}</option>
                ))}
              </select>
            </label>
          </div>
          <div className="sm:col-span-2">
            <Field label="Description" value={description} onChange={setDescription} />
          </div>
          <div className="sm:col-span-2">
            <Field label="Image URL (optional)" value={image} onChange={setImage} />
          </div>
        </div>
        <div className="mt-5 flex justify-end gap-2">
          <button onClick={onClose} className="rounded-full border border-border px-4 py-2 text-sm font-semibold">Cancel</button>
          <button onClick={save} disabled={busy} className="inline-flex items-center gap-2 rounded-full bg-gradient-primary px-5 py-2 text-sm font-semibold text-primary-foreground shadow-soft disabled:opacity-60">
            {busy && <Loader2 className="h-4 w-4 animate-spin" />} {item ? "Save changes" : "Create item"}
          </button>
        </div>
      </div>
    </div>
  );
}

function CategoriesModal({ categories, onClose, onCreate, onUpdate, onDelete }: { categories: any[]; onClose: () => void; onCreate: (n: string) => Promise<void>; onUpdate: (id: string, n: string) => Promise<void>; onDelete: (id: string) => Promise<void> }) {
  const [newCat, setNewCat] = useState("");
  const [editingCatId, setEditingCatId] = useState("");
  const [editingName, setEditingName] = useState("");
  const [busy, setBusy] = useState(false);

  const create = async () => {
    if (!newCat.trim()) return;
    setBusy(true);
    await onCreate(newCat);
    setNewCat("");
    setBusy(false);
  };

  const update = async () => {
    if (!editingName.trim()) return;
    setBusy(true);
    await onUpdate(editingCatId, editingName);
    setEditingCatId("");
    setEditingName("");
    setBusy(false);
  };

  const remove = async (id: string) => {
    setBusy(true);
    await onDelete(id);
    setBusy(false);
  };

  return (
    <div className="fixed inset-0 z-50 grid place-items-center p-4">
      <div className="absolute inset-0 bg-foreground/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative w-full max-w-md animate-float-up rounded-3xl bg-card p-6 shadow-glow max-h-[80vh] flex flex-col">
        <div className="flex items-start justify-between shrink-0">
          <div>
            <p className="text-xs uppercase tracking-wider text-muted-foreground">Manage</p>
            <h2 className="font-display text-xl font-bold">Categories</h2>
          </div>
          <button onClick={onClose} className="grid h-8 w-8 place-items-center rounded-full bg-secondary"><X className="h-4 w-4" /></button>
        </div>
        
        <div className="mt-4 flex gap-2 shrink-0">
          <input value={newCat} onChange={e => setNewCat(e.target.value)} placeholder="New category name..." className="flex-1 rounded-xl border border-border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring" />
          <button onClick={create} disabled={busy || !newCat} className="rounded-xl bg-gradient-primary px-4 py-2 text-sm font-semibold text-primary-foreground disabled:opacity-60">Add</button>
        </div>

        <div className="mt-6 flex-1 overflow-y-auto min-h-[200px]">
          {categories.length === 0 && <p className="text-sm text-muted-foreground text-center py-4">No categories yet.</p>}
          <div className="space-y-2">
            {categories.map((c: any) => (
              <div key={c.id} className="flex items-center gap-2 p-2 rounded-xl border border-border bg-background">
                {editingCatId === c.id ? (
                  <>
                    <input value={editingName} onChange={e => setEditingName(e.target.value)} className="flex-1 rounded border border-border bg-background px-2 py-1 text-sm focus:outline-none" />
                    <button onClick={update} disabled={busy || !editingName} className="text-xs font-semibold text-primary">Save</button>
                    <button onClick={() => setEditingCatId("")} className="text-xs text-muted-foreground">Cancel</button>
                  </>
                ) : (
                  <>
                    <span className="flex-1 text-sm font-semibold">{c.name}</span>
                    <button onClick={() => { setEditingCatId(c.id); setEditingName(c.name); }} className="p-1.5 text-muted-foreground hover:bg-secondary rounded-md transition"><Pencil className="h-4 w-4" /></button>
                    <button onClick={() => remove(c.id)} disabled={busy} className="p-1.5 text-destructive hover:bg-destructive/10 rounded-md transition"><Trash2 className="h-4 w-4" /></button>
                  </>
                )}
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}

function Field({ label, value, onChange, readOnly }: { label: string; value: string; onChange?: (v: string) => void; readOnly?: boolean }) {
  return (
    <label className="block">
      <span className="text-xs font-semibold text-muted-foreground">{label}</span>
      <input
        value={value}
        onChange={(e) => onChange?.(e.target.value)}
        readOnly={readOnly}
        className="mt-1 w-full rounded-xl border border-border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring read-only:bg-secondary read-only:text-muted-foreground"
      />
    </label>
  );
}