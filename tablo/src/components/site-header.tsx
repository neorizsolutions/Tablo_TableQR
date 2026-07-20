import { Link } from "@tanstack/react-router";
import { LogIn, LogOut, QrCode } from "lucide-react";
import { useAuth } from "@/hooks/use-auth";

export function SiteHeader() {
  const { user, roles, signOut } = useAuth();
  
  const navLinks = (() => {
    if (!user) {
      return [
        { to: "/", label: "Overview" },
      ];
    }
    const links = [];
    if (roles.includes("Admin") || roles.includes("Owner")) {
      links.push({ to: "/", label: "Overview" });
      links.push({ to: "/dashboard", label: "Kitchen / Waiter" });
      links.push({ to: "/admin", label: "Admin" });
      links.push({ to: "/staff", label: "Staff" });
    } else if (roles.includes("Kitchen")) {
      links.push({ to: "/dashboard", label: "Kitchen / Orders" });
    } else if (roles.includes("Waiter")) {
      links.push({ to: "/dashboard", label: "Waiter / Orders" });
    }
    return links;
  })();

  return (
    <header className="sticky top-0 z-40 border-b border-border/60 bg-background/80 backdrop-blur-xl">
      <div className="mx-auto flex h-16 max-w-7xl items-center justify-between px-4 sm:px-6">
        <Link to="/" className="flex items-center gap-2 font-display text-lg font-bold">
          <span className="grid h-9 w-9 place-items-center rounded-xl bg-gradient-primary text-primary-foreground shadow-soft">
            <QrCode className="h-4 w-4" />
          </span>
          <span>
            Tablo<span className="text-primary">.</span>
          </span>
        </Link>
        <nav className="hidden items-center gap-1 md:flex">
          {navLinks.map((l) => (
            <Link
              key={l.to}
              to={l.to}
              activeOptions={{ exact: true }}
              className="rounded-full px-4 py-2 text-sm font-medium text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground data-[status=active]:bg-primary-soft data-[status=active]:text-primary"
            >
              {l.label}
            </Link>
          ))}
        </nav>
        <div className="hidden items-center gap-2 sm:flex">
          {user ? (
            <button onClick={signOut} className="inline-flex items-center gap-1.5 rounded-full border border-border bg-card px-3 py-2 text-xs font-semibold">
              <LogOut className="h-3.5 w-3.5" /> Sign out
            </button>
          ) : (
            <>
              <Link to="/auth" className="inline-flex items-center gap-1.5 rounded-full border border-border bg-card px-3 py-2 text-xs font-semibold">
                <LogIn className="h-3.5 w-3.5" /> Staff sign in
              </Link>
            </>
          )}
        </div>
      </div>
      <nav className="flex items-center gap-1 overflow-x-auto border-t border-border/60 px-3 py-2 md:hidden">
        {navLinks.map((l) => (
          <Link
            key={l.to}
            to={l.to}
            activeOptions={{ exact: true }}
            className="shrink-0 rounded-full px-3 py-1.5 text-xs font-medium text-muted-foreground data-[status=active]:bg-primary-soft data-[status=active]:text-primary"
          >
            {l.label}
          </Link>
        ))}
      </nav>
    </header>
  );
}